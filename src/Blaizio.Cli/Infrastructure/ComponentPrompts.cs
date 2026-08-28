using Blaizio.Cli.Core.Registry;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Blaizio.Cli.Infrastructure;

/// <summary>The one interactive component picker shared by <c>init</c> and <c>add</c>.</summary>
internal static class ComponentPrompts
{
    /// <summary>Checkbox picker over the registry catalogue (space to toggle, a to toggle all, enter to confirm).</summary>
    public static async Task<string[]> PickAsync(IRegistryClient registry, string title, CancellationToken ct = default)
    {
        var index = await registry.GetIndexAsync(ct);
        // Fonts are styling choices, not components - they'd flood the checkbox list. Name one
        // explicitly (blaizio add font-inter) or pick it on /create instead.
        var names = index.Items
            // Fonts are styling choices and templates are whole apps - neither is a component to pick.
            .Where(i => i.Type is not ItemType.Font and not ItemType.Template)
            .Select(i => i.Name)
            .ToArray();
        if (names.Length == 0)
            return [];

        return MultiSelect(title, names);
    }

    /// <summary>
    /// A hand-rolled checkbox list. Spectre's MultiSelectionPrompt has no toggle-all key (and no
    /// extension point to add one), so this reimplements its look — cursor, checkboxes, paging —
    /// on a Live region and owns the key loop: space toggles, <c>a</c> toggles everything, enter
    /// confirms, escape cancels the command. Keys are read through <see cref="CliPrompts.ReadKey"/>,
    /// so Ctrl+C ends the picker instead of being swallowed by it.
    /// </summary>
    internal static string[] MultiSelect(string title, IReadOnlyList<string> choices, int pageSize = 15, bool preselectAll = false)
    {
        var console = AnsiConsole.Console;
        var selected = new bool[choices.Count];
        if (preselectAll) Array.Fill(selected, true);
        var cursor = 0;

        console.Cursor.Hide();
        try
        {
            var confirmed = false;
            console.Live(Render(title, choices, selected, cursor, pageSize))
                .AutoClear(true)
                .Start(ctx =>
                {
                    while (true)
                    {
                        ctx.UpdateTarget(Render(title, choices, selected, cursor, pageSize));
                        ctx.Refresh();

                        var key = CliPrompts.ReadKey(console);
                        if (key is not { } k)
                            return; // input closed (piped stdin ran dry) - treat as an empty pick
                        switch (k.Key)
                        {
                            case ConsoleKey.UpArrow or ConsoleKey.K:
                                cursor = cursor == 0 ? choices.Count - 1 : cursor - 1;
                                break;
                            case ConsoleKey.DownArrow or ConsoleKey.J:
                                cursor = cursor == choices.Count - 1 ? 0 : cursor + 1;
                                break;
                            case ConsoleKey.PageUp:
                                cursor = Math.Max(0, cursor - pageSize);
                                break;
                            case ConsoleKey.PageDown:
                                cursor = Math.Min(choices.Count - 1, cursor + pageSize);
                                break;
                            case ConsoleKey.Home:
                                cursor = 0;
                                break;
                            case ConsoleKey.End:
                                cursor = choices.Count - 1;
                                break;
                            case ConsoleKey.Spacebar:
                                selected[cursor] = !selected[cursor];
                                break;
                            case ConsoleKey.A:
                                // Toggle all: select everything, unless everything already is.
                                var all = selected.All(s => s);
                                Array.Fill(selected, !all);
                                break;
                            case ConsoleKey.Enter:
                                confirmed = true;
                                return;
                            case ConsoleKey.Escape:
                                throw new OperationCanceledException();
                        }
                    }
                });

            return confirmed
                ? [.. choices.Where((_, i) => selected[i])]
                : [];
        }
        finally
        {
            console.Cursor.Show();
        }
    }

    /// <summary>One frame: title, the visible page of checkbox rows, paging hint and key legend.</summary>
    private static IRenderable Render(
        string title, IReadOnlyList<string> choices, bool[] selected, int cursor, int pageSize)
    {
        // Keep the cursor inside the visible window, clamped to the list bounds.
        var window = Math.Min(pageSize, choices.Count);
        var first = Math.Clamp(cursor - window / 2, 0, choices.Count - window);

        var rows = new List<IRenderable> { new Markup(title) };
        for (var i = first; i < first + window; i++)
        {
            var pointer = i == cursor ? "[blue]>[/]" : " ";
            var box = selected[i] ? "[[[blue]x[/]]]" : "[[ ]]";
            var name = i == cursor ? $"[blue]{Markup.Escape(choices[i])}[/]" : Markup.Escape(choices[i]);
            rows.Add(new Markup($"{pointer} {box} {name}"));
        }

        if (choices.Count > window)
            rows.Add(new Markup("[grey](move up/down to reveal more)[/]"));
        rows.Add(new Markup("[grey](space to toggle, [/][white]a[/][grey] to toggle all, enter to confirm, esc to cancel)[/]"));
        return new Rows(rows);
    }
}
