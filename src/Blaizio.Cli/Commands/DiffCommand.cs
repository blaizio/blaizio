using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>diff</c>.</summary>
public sealed class DiffSettings : GlobalSettings
{
    /// <summary>Component to diff; empty diffs all installed components.</summary>
    [CommandArgument(0, "[COMPONENT]")]
    [Description("Component to diff against the registry (default: all installed).")]
    public string? Component { get; init; }
}

/// <summary>
/// Compares installed components against their upstream registry versions. Exit code 0 when clean,
/// 1 when anything drifted (like <c>git diff --exit-code</c>).
/// </summary>
public sealed class DiffCommand : AsyncCommand<DiffSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, DiffSettings settings)
    {
        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);
        var config = services.RequireConfig();

        string[] components = settings.Component is { Length: > 0 } one ? [one] : [];
        if (components.Length == 0 && config.Installed.Count == 0)
        {
            settings.Warn("[yellow]No installed components recorded in blaizio.json.[/] Run [white]blaizio add <component>[/] first.");
            if (settings.Json)
                Console.Out.WriteLine("""{"items":[],"hasDrift":false}""");
            return 0;
        }

        var result = await new DiffService(services.Registry, services.Project, config)
            .RunAsync(components, ct);

        if (settings.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(result, CoreJson.Default.DiffResult));
            return result.HasDrift ? 1 : 0;
        }

        if (!settings.Silent)
        {
            foreach (var item in result.Items)
            {
                if (!item.Drifted)
                {
                    AnsiConsole.MarkupLine($"  [green]=[/] [cyan]{Markup.Escape(item.Name)}[/] up to date");
                    continue;
                }

                AnsiConsole.MarkupLine($"  [yellow]~[/] [cyan]{Markup.Escape(item.Name)}[/]");
                foreach (var file in item.Files.Where(f => f.Status is not DiffStatus.Unchanged))
                {
                    var (glyph, color, label) = file.Status switch
                    {
                        DiffStatus.Missing => ("-", "red", "missing"),
                        _ => ("~", "yellow", "changed"),
                    };
                    AnsiConsole.MarkupLine($"      [{color}]{glyph}[/] {Markup.Escape(file.Path)} [grey]({label})[/]");
                }
            }

            AnsiConsole.MarkupLine(result.HasDrift
                ? "[yellow]Drift found.[/] Re-pull with [white]blaizio update <component>[/] (overwrites local edits)."
                : "[green]Everything matches upstream.[/]");
        }

        return result.HasDrift ? 1 : 0;
    }
}

