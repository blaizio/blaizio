using Blaizio.Cli.Core.Operations;
using Spectre.Console;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// The consent gate in front of an overwrite. <c>update</c> (and <c>add --overwrite</c>) replace
/// component files wholesale, so anything the user changed since install would be gone - this asks
/// first, with a checkbox list of exactly the components that carry local changes.
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
        AnsiConsole.MarkupLine(
            $"[yellow]{edited.Count} component(s) differ from the version Blaizio installed.[/] Replacing them discards those changes.");

        foreach (var item in edited)
        {
            AnsiConsole.MarkupLine($"  [yellow]~[/] [cyan]{Markup.Escape(item.Name)}[/]");
            foreach (var file in item.Files)
            {
                var note = file.Kind == LocalEditKind.Unknown ? "no baseline recorded" : "changed locally";
                AnsiConsole.MarkupLine($"      {Markup.Escape(file.Path)} [grey]({note})[/]");
            }
        }

        var names = edited.Select(e => e.Name).ToArray();
        var picked = ComponentPrompts.MultiSelect(
            "Select the components to [red]replace[/] with the upstream version (unselected keep yours):", names);

        return Task.FromResult<IReadOnlySet<string>>(picked.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Report the components whose local version survived the run, with the way to take upstream
    /// anyway. Silent when nothing was kept, or under <c>--json</c> (the result carries it).
    /// </summary>
    public static void ReportKept(GlobalSettings settings, AddResult result, string takeUpstream)
    {
        if (settings.Json || settings.Silent)
            return;

        if (result.KeptLocal.Count > 0)
        {
            var names = string.Join(", ", result.KeptLocal.Select(Markup.Escape));
            AnsiConsole.MarkupLine($"[yellow]Kept your version[/] of {names}.");
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
