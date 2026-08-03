using System.Text.Json.Serialization;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Writing;

namespace Blaizio.Cli.Core.Operations;

/// <summary>Why a file is in the way of an overwrite.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LocalEditKind>))]
public enum LocalEditKind
{
    /// <summary>The local copy differs from the baseline this CLI recorded when it wrote the file.</summary>
    Edited,

    /// <summary>
    /// No baseline was recorded (installed before the ledger existed, or the file was already
    /// there when <c>add</c> ran), so an edit can neither be confirmed nor ruled out.
    /// </summary>
    Unknown,
}

/// <summary>One local file standing between an update and the upstream version.</summary>
/// <param name="Path">Path relative to the output directory, POSIX separators.</param>
/// <param name="Kind">Confirmed edit, or no baseline to judge by.</param>
public sealed record LocalEdit(string Path, LocalEditKind Kind);

/// <summary>One item with at least one file the user may have changed.</summary>
/// <param name="Name">Qualified registry item name.</param>
/// <param name="Files">The files in the way; every one carries an incoming upstream change.</param>
public sealed record EditedItem(string Name, IReadOnlyList<LocalEdit> Files);

/// <summary>
/// Finds the files an overwrite would destroy. For each file of a resolved item it compares three
/// things: the baseline recorded at install time (<c>blaizio.json</c> <c>installed[].hashes</c>),
/// the working copy on disk, and the upstream content after the namespace rewrite.
/// <list type="bullet">
///   <item>local missing - nothing to lose, the write just restores it.</item>
///   <item>local already equals upstream - no incoming change, nothing to decide.</item>
///   <item>local equals its baseline - untouched since install, safe to replace.</item>
///   <item>local differs from its baseline - <see cref="LocalEditKind.Edited"/>.</item>
///   <item>no baseline recorded - <see cref="LocalEditKind.Unknown"/>, treated as an edit.</item>
/// </list>
/// The baseline is what makes this useful: without it, "I changed this file" and "a new version
/// shipped" are the same observation, and every updated file would look like an edit.
/// </summary>
public static class LocalEdits
{
    /// <summary>
    /// Scan <paramref name="items"/> against disk. Purely local - the upstream content is already
    /// in hand, so no network and no extra fetch. Items with nothing in the way are omitted.
    /// </summary>
    /// <param name="items">The resolved items about to be written.</param>
    /// <param name="writerFor">Resolves the writer (and therefore paths + rewrite) for an item.</param>
    /// <param name="config">Config holding the recorded baselines.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<IReadOnlyList<EditedItem>> ScanAsync(
        IEnumerable<RegistryItem> items,
        Func<RegistryItem, ComponentWriter> writerFor,
        BlaizioConfig config,
        CancellationToken ct = default)
    {
        var edited = new List<EditedItem>();

        foreach (var item in items)
        {
            // Items that copy no files (fonts, themes) patch the tokens file instead - they own no
            // destination that could carry the user's edits.
            if (item.Files.Count == 0)
                continue;

            var writer = writerFor(item);
            var recorded = config.Installed.TryGetValue(item.QualifiedName, out var installed)
                ? installed.Hashes
                : [];

            var files = new List<LocalEdit>();
            foreach (var file in item.Files)
            {
                var (reported, absolute, content) = writer.Plan(item, file);
                var local = await ContentHash.OfFileAsync(absolute, ct);
                if (local is null)
                    continue;

                var upstream = ContentHash.Of(content);
                if (ContentHash.Matches(local, upstream))
                    continue;

                var baseline = recorded.GetValueOrDefault(reported);
                if (ContentHash.Matches(baseline, local))
                    continue;

                files.Add(new LocalEdit(
                    reported, baseline is null ? LocalEditKind.Unknown : LocalEditKind.Edited));
            }

            if (files.Count > 0)
                edited.Add(new EditedItem(item.QualifiedName, files));
        }

        return edited;
    }
}
