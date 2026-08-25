using Blaizio.Cli.Core.Writing;

namespace Blaizio.Cli.Core.Operations;

/// <summary>The full outcome of an <c>add</c>, structured for both console reporting and <c>--json</c>.</summary>
public sealed class AddResult
{
    /// <summary>Item names installed, in dependency order.</summary>
    public required IReadOnlyList<string> Items { get; init; }

    /// <summary>NuGet packages that were (or would be) installed.</summary>
    public required IReadOnlyList<string> NugetPackages { get; init; }

    /// <summary>Development-only NuGet packages installed and marked <c>PrivateAssets="all"</c>.</summary>
    public IReadOnlyList<string> DevNugetPackages { get; init; } = [];

    /// <summary>
    /// The installed items' <c>docs</c> notes (setup steps, documentation links) - the registry
    /// author's one chance to say something at install time, so every surface shows them.
    /// </summary>
    public IReadOnlyList<ItemDoc> DocsNotes { get; init; } = [];

    /// <summary>Every file touched, with its per-file action.</summary>
    public required IReadOnlyList<WrittenFile> Files { get; init; }

    /// <summary>The component namespace used.</summary>
    public required string Namespace { get; init; }

    /// <summary>Whether <c>_Imports.razor</c> gained a new <c>@using</c>.</summary>
    public required bool ImportsUpdated { get; init; }

    /// <summary>True when this was a dry run (nothing written).</summary>
    public required bool DryRun { get; init; }

    /// <summary>
    /// Items carrying files that differ from the baseline recorded at install time AND from
    /// upstream - the ones an overwrite would have destroyed. Empty for a run that was not
    /// overwriting anything.
    /// </summary>
    public IReadOnlyList<EditedItem> Edited { get; init; } = [];

    /// <summary>
    /// Items from <see cref="Edited"/> whose local version was kept: not chosen at the prompt, or
    /// an unattended run with no resolver. Their untouched files report as
    /// <see cref="Writing.WriteAction.Skipped"/>.
    /// </summary>
    public IReadOnlyList<string> KeptLocal { get; init; } = [];

    /// <summary>
    /// Files the re-installed items no longer ship (an upstream rename or split) that were left on
    /// disk anyway, because their content no longer matches the baseline recorded for them - or no
    /// baseline was ever recorded. Deleting them would discard work; <c>--force</c> does it anyway.
    /// </summary>
    public IReadOnlyList<string> LeftBehind { get; init; } = [];

    /// <summary>
    /// Ledger entries that look like a SECOND record of an item this run installed: same bare
    /// item name under a different key (<c>editor</c> installed from a file while
    /// <c>@editor/editor</c> is also recorded). Two records of one item each maintain their own
    /// copy at their own paths - namespaced installs nest under the namespace folder - so every
    /// update writes both layouts and the duplicates read as corruption. Surfaced so the caller
    /// can tell the user to remove one.
    /// </summary>
    public IReadOnlyList<RivalRecord> RivalRecords { get; init; } = [];

    /// <summary>
    /// Requested items that could not be resolved and were skipped rather than failing the run -
    /// only when the request asked for that (<see cref="AddRequest.SkipMissing"/>). Each carries
    /// the reference as requested and the registry's own failure message.
    /// </summary>
    public IReadOnlyList<SkippedItem> Skipped { get; init; } = [];
}

/// <summary>One installed item's <c>docs</c> note.</summary>
public sealed record ItemDoc(string Item, string Note);

/// <summary>A requested item the run went on without, and the failure that made it skip.</summary>
public sealed record SkippedItem(string Reference, string Reason);

/// <summary>One item recorded twice: the key this run installed and the other key that shadows it.</summary>
public sealed record RivalRecord(string Installed, string Rival);
