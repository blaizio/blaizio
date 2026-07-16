using System.ComponentModel;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Core.Styling.Accessibility;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>contrast</c>.</summary>
public sealed class ContrastSettings : GlobalSettings
{
    /// <summary>Tokens file to audit (defaults to the recorded input, else Styles/app.css).</summary>
    [CommandOption("--css <path>")]
    [Description("Tokens file to audit (default: blaizio.json \"css\", else Styles/app.css)")]
    public string? Css { get; init; }

    /// <summary>Show every measured pair, not just the failures.</summary>
    [CommandOption("-a|--all")]
    [Description("List every measured pair, not only the failures (default: false)")]
    public bool All { get; init; }
}

/// <summary>
/// Audits the tokens file's colors for WCAG AA contrast — the safety net for a re-themed
/// project. Measures the pairs the components actually paint (foreground-on-surface, the tokens
/// used as text on the page background, the focus ring, the button's derived dark fill) in both
/// light and dark mode, and exits 1 when anything lands under its minimum — CI-friendly, like
/// <c>add --diff</c>.
/// </summary>
public sealed class ContrastCommand : AsyncCommand<ContrastSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ContrastSettings settings)
    {
        var cwd = settings.ResolvedCwd;
        var ct = CliCancellation.Token;
        var config = await ConfigStore.LoadAsync(cwd, ct);

        var inputRel = settings.Css ?? config?.Css ?? Path.Combine("Styles", "app.css");
        var inputAbs = Path.GetFullPath(Path.Combine(cwd, inputRel));
        if (!File.Exists(inputAbs))
        {
            CliOutput.Error.MarkupLine(
                $"[red]Error:[/] No tokens file at '{Markup.Escape(inputRel.Replace('\\', '/'))}'. " +
                $"Run [white]blaizio init[/] first, or point at yours with [white]--css <path>[/].");
            return 1;
        }

        var report = ContrastAudit.Run(await File.ReadAllTextAsync(inputAbs, ct));
        var failures = report.Findings.Where(f => !f.Pass && !f.Advisory).ToList();
        var advisories = report.Findings.Where(f => !f.Pass && f.Advisory).ToList();

        if (settings.Json)
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["file"] = inputRel.Replace('\\', '/'),
                ["pass"] = report.AllPass,
                ["findings"] = new JsonArray([.. report.Findings.Select(f => (JsonNode)new JsonObject
                {
                    ["mode"] = f.Mode,
                    ["label"] = f.Label,
                    ["foreground"] = f.Foreground,
                    ["background"] = f.Background,
                    ["ratio"] = Math.Round(f.Ratio, 2),
                    ["required"] = f.Required,
                    ["pass"] = f.Pass,
                    ["advisory"] = f.Advisory,
                })]),
                ["unparsed"] = new JsonArray([.. report.Unparsed.Select(u => (JsonNode)u)]),
            }.ToJsonString());
            return report.AllPass ? 0 : 1;
        }

        if (!settings.Silent)
        {
            IReadOnlyList<ContrastFinding> shown = settings.All ? report.Findings : [.. failures, .. advisories];
            if (shown.Count > 0)
            {
                var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
                table.AddColumn("Mode");
                table.AddColumn("Pair");
                table.AddColumn(new TableColumn("Ratio").RightAligned());
                table.AddColumn(new TableColumn("Min").RightAligned());
                table.AddColumn("");
                foreach (var f in shown.OrderBy(f => f.Mode).ThenBy(f => f.Pass || f.Advisory))
                {
                    var verdict = f.Pass ? "[green]pass[/]" : f.Advisory ? "[yellow]warn[/]" : "[red]FAIL[/]";
                    table.AddRow(
                        f.Mode,
                        Markup.Escape(f.Label),
                        f.Pass ? $"{f.Ratio:0.00}" : f.Advisory ? $"[yellow]{f.Ratio:0.00}[/]" : $"[red]{f.Ratio:0.00}[/]",
                        $"{f.Required:0.0}",
                        verdict);
                }
                AnsiConsole.Write(table);
            }

            foreach (var value in report.Unparsed)
                settings.Warn($"[yellow]Unchecked (couldn't parse):[/] {Markup.Escape(value)}");
            if (advisories.Count > 0)
                settings.Line(
                    "[grey]warn = primary's two roles (link text on the page vs a fill under its label) " +
                    "can't both clear AA with one value in every palette; the design trades one side. " +
                    "The default button derives an AA-safe dark fill regardless (checked above).[/]");

            AnsiConsole.MarkupLine(report.AllPass
                ? $"[green]All {report.Findings.Count} measured pairs clear WCAG AA[/] in {Markup.Escape(inputRel.Replace('\\', '/'))}."
                : $"[red]{failures.Count} of {report.Findings.Count} pairs fail WCAG AA[/] in {Markup.Escape(inputRel.Replace('\\', '/'))}. " +
                  "Darken the failing foreground (or lighten the surface) until the ratio clears the minimum.");
        }

        return report.AllPass ? 0 : 1;
    }
}
