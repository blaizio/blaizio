using System.Text.Json;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Writing;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;

namespace Blaizio.Cli.Commands;

/// <summary>Renders an <see cref="AddResult"/> - the JSON document or the per-file terminal
/// report. Presentation only; the command decides which shape a run gets.</summary>
internal static class AddOutput
{
    public static int EmitJson(AddResult result)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(result, CliJson.Default.AddResult));
        return 0;
    }

    public static int Report(AddResult result)
    {
        foreach (var file in result.Files)
        {
            var (glyph, color) = file.Action switch
            {
                WriteAction.Created => ("+", "green"),
                WriteAction.Overwritten => ("~", "yellow"),
                WriteAction.Skipped => ("=", "grey"),
                WriteAction.Deleted => ("-", "red"),
                _ => ("·", "blue"),
            };
            AnsiConsole.MarkupLine($"  [{color}]{glyph}[/] {Markup.Escape(file.Path)}");
        }

        if (result.NugetPackages.Count > 0)
            AnsiConsole.MarkupLine($"  [blue]nuget[/] {Markup.Escape(string.Join(", ", result.NugetPackages))}");

        if (result.ImportsUpdated)
            AnsiConsole.MarkupLine($"  [blue]using[/] {Markup.Escape(result.Namespace)} added to _Imports.razor");

        var verb = result.DryRun ? "Planned" : "Added";
        AnsiConsole.MarkupLine($"[green]{verb}[/] {result.Items.Count} item(s).");
        return 0;
    }
}
