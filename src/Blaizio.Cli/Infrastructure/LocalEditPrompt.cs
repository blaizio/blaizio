using Blaizio.Cli.Core.Operations;
using Spectre.Console;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// The consent gate in front of an overwrite. <c>update</c> (and <c>add --overwrite</c>) replace
/// component files wholesale, so anything the user changed since install would be gone - this asks
/// first, with a checkbox list of exactly the files that carry local changes, grouped under their
/// component. The decision is per file: a component can keep one edited file and take upstream
/// for another.
/// <para>
/// Interactive runs pick. Unattended runs (<c>-y</c>, <c>--json</c>, <c>--silent</c>, a
/// non-interactive terminal) get no resolver at all, which the engine reads as "keep every edit":
/// a script must never destroy work nobody was asked about. <c>--force</c> is the explicit
/// opt-out and skips this entirely.
/// </para>
/// </summary>
internal static class LocalEditPrompt
{
    /// <summary>
    /// The resolver to hand <see cref="AddRequest.ResolveConflicts"/>, or <see langword="null"/>
    /// when nobody can be asked (which keeps local edits).
    /// </summary>
    public static Func<IReadOnlyList<EditedItem>, CancellationToken, Task<IReadOnlySet<string>>>? For(
        GlobalSettings settings) =>
        settings.NonInteractive || !AnsiConsole.Profile.Capabilities.Interactive ? null : AskAsync;

    /// <summary>True when a run might stop to ask, so the caller can keep a live spinner out of the way.</summary>
    public static bool MayPrompt(GlobalSettings settings, bool overwrite, bool force) =>
        overwrite && !force && For(settings) is not null;

    private static Task<IReadOnlySet<string>> AskAsync(IReadOnlyList<EditedItem> edited, CancellationToken ct)
    {
        var fileCount = edited.Sum(e => e.Files.Count);
        AnsiConsole.MarkupLine(
            $"[yellow]{fileCount} file(s) in {edited.Count} component(s) differ from the version Blaizio installed.[/] Replacing a file discards your changes to it.");

        // One row per FILE, labelled by its component, so the pick is the file and the component
        // is only the grouping. The label round-trips to the path through this map; a path is
        // unique across items (one file belongs to one item), so the set the engine gets is flat.
        var rows = new List<(string Label, string Path)>();
        foreach (var item in edited)
        {
            foreach (var file in item.Files)
            {
                var note = file.Kind == LocalEditKind.Unknown ? "no baseline recorded" : "changed locally";
                rows.Add(($"{item.Name}  {file.Path}  ({note})", file.Path));
            }
        }

        var picked = ComponentPrompts.MultiSelect(
            "Select the files to [red]replace[/] with the upstream version (unselected keep yours):",
            [.. rows.Select(r => r.Label)]);
        var byLabel = rows.ToDictionary(r => r.Label, r => r.Path, StringComparer.Ordinal);

        return Task.FromResult<IReadOnlySet<string>>(
            picked.Select(label => byLabel[label]).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// Report the files whose local version survived the run, grouped under their component, with
    /// the way to take upstream anyway. When the same component also had a file replaced, that is
    /// said too - the two outcomes side by side is the point of deciding per file. Silent when
    /// nothing was kept, or under <c>--json</c> (the result carries it).
    /// </summary>
    public static void ReportKept(GlobalSettings settings, AddResult result, string takeUpstream)
    {
        if (settings.Json || settings.Silent)
            return;

        if (result.KeptLocal.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Kept your version[/] of {result.Decisions.Count(d => d.Kept)} file(s):");
            foreach (var item in result.Decisions.GroupBy(d => d.Item, StringComparer.OrdinalIgnoreCase))
            {
                var kept = item.Where(d => d.Kept).Select(d => d.Path).ToList();
                if (kept.Count == 0)
                    continue;
                AnsiConsole.MarkupLine($"  [yellow]~[/] [cyan]{Markup.Escape(item.Key)}[/]");
                foreach (var path in kept)
                    AnsiConsole.MarkupLine($"      {Markup.Escape(path)}");
                var taken = item.Where(d => !d.Kept).Select(d => d.Path).ToList();
                if (taken.Count > 0)
                    AnsiConsole.MarkupLine($"      [grey]took upstream:[/] {Markup.Escape(string.Join(", ", taken))}");
            }
            AnsiConsole.MarkupLine(
                $"  Inspect with [white]blaizio add --diff <component>[/], take upstream with [white]{takeUpstream}[/].");
        }

        // Orphans that were not provably untouched: the stale file is still there, and the type it
        // declares still resolves, so say so plainly rather than let it surface as a puzzling
        // conversion error at some call site later.
        if (result.LeftBehind.Count > 0)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]No longer shipped, left on disk[/] ({result.LeftBehind.Count} file(s)) - they may still compile and shadow their replacements:");
            foreach (var path in result.LeftBehind)
                AnsiConsole.MarkupLine($"  [yellow]![/] {Markup.Escape(path)}");
            AnsiConsole.MarkupLine(
                $"  Delete them yourself once you have migrated, or run [white]{takeUpstream}[/] to have them removed.");
        }
    }
}
