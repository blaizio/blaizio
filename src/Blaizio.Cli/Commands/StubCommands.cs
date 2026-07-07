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

/// <summary>Settings for <c>migrate</c>.</summary>
public sealed class MigrateSettings : GlobalSettings
{
    /// <summary>Migration name (e.g. rtl, icons).</summary>
    [CommandArgument(0, "<MIGRATION>")]
    [Description("Migration to run (rtl, icons).")]
    public string Migration { get; init; } = string.Empty;

    /// <summary>Optional path or glob to scope the migration.</summary>
    [CommandArgument(1, "[PATH]")]
    [Description("Optional path or glob to scope the migration.")]
    public string? Path { get; init; }
}

/// <summary>Runs a codemod migration. Not implemented yet.</summary>
public sealed class MigrateCommand : AsyncCommand<MigrateSettings>
{
    /// <inheritdoc />
    public override Task<int> ExecuteAsync(CommandContext context, MigrateSettings settings)
    {
        // Exit 3 = not implemented, so scripts can tell a stub from success.
        if (settings.Json)
            Console.Out.WriteLine("""{"error":"not-implemented","command":"migrate"}""");
        else
            settings.Warn($"[yellow]'migrate {Markup.Escape(settings.Migration)}' is not implemented yet.[/]");
        return Task.FromResult(3);
    }
}
