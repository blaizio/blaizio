using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Styling.Pipelines;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Shared helpers for the <c>tailwind</c> subcommands.</summary>
internal static class TailwindPipelineSupport
{
    /// <summary>The compile input/output for a project (input matches what <c>init</c> writes).</summary>
    public static TailwindPaths PathsFor(string? outputOverride)
        => new("Styles/app.css", outputOverride ?? "wwwroot/app.css");
}

/// <summary>Reports which Tailwind pipelines are present and which is recommended.</summary>
public sealed class TailwindDetectCommand : AsyncCommand<GlobalSettings>
{
    /// <inheritdoc />
    public override Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings)
    {
        var project = ProjectContext.Discover(settings.ResolvedCwd);
        var registry = new TailwindPipelineRegistry();
        var probes = registry.DetectAll(project);
        var recommended = registry.Recommend(project);

        if (settings.Json)
        {
            var report = probes.Select(p => new PipelineReport(
                p.Pipeline.Id, p.Pipeline.Title, p.Detection.Presence.ToString(),
                p.Detection.Evidence, p.Pipeline.CanSetup, p.Pipeline.Id == recommended.Id)).ToArray();
            AnsiConsole.WriteLine(JsonSerializer.Serialize<IReadOnlyList<PipelineReport>>(report, CliJson.Default.IReadOnlyListPipelineReport));
            return Task.FromResult(0);
        }

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        table.AddColumn("[cyan]Pipeline[/]");
        table.AddColumn("Status");
        table.AddColumn("Evidence");
        foreach (var p in probes)
        {
            var status = p.Detection.Presence switch
            {
                PipelinePresence.Present => "[green]present[/]",
                PipelinePresence.Partial => "[yellow]partial[/]",
                _ => "[grey]absent[/]",
            };
            var name = p.Pipeline.Id == recommended.Id ? $"[cyan]{p.Pipeline.Id}[/] [grey](recommended)[/]" : p.Pipeline.Id;
            table.AddRow(name, status, Markup.Escape(p.Detection.Evidence ?? ""));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"Run [white]blaizio tailwind setup --mode {recommended.Id}[/] to wire it.");
        return Task.FromResult(0);
    }
}

/// <summary>Settings for <c>tailwind setup</c>.</summary>
public sealed class TailwindSetupSettings : GlobalSettings
{
    /// <summary>Pipeline to wire: auto, standalone, node, vite, postcss, none.</summary>
    [CommandOption("-m|--mode <MODE>")]
    [Description("Pipeline: auto, standalone, node, vite, postcss, none.")]
    [DefaultValue("auto")]
    public string Mode { get; init; } = "auto";

    /// <summary>Compiled CSS output path (node/standalone).</summary>
    [CommandOption("-o|--output <PATH>")]
    [Description("Compiled CSS output path (default: wwwroot/app.css).")]
    public string? Output { get; init; }
}

/// <summary>Wires a chosen Tailwind pipeline into the project (or reports manual steps for bundlers).</summary>
public sealed class TailwindSetupCommand : AsyncCommand<TailwindSetupSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, TailwindSetupSettings settings)
    {
        var project = ProjectContext.Discover(settings.ResolvedCwd);
        var registry = new TailwindPipelineRegistry();

        var pipeline = settings.Mode.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? registry.Recommend(project)
            : registry.Resolve(settings.Mode);

        if (pipeline is null)
        {
            AnsiConsole.MarkupLine($"[red]Unknown mode '{Markup.Escape(settings.Mode)}'.[/] Options: {string.Join(", ", registry.All.Select(p => p.Id))}.");
            return 1;
        }

        var paths = TailwindPipelineSupport.PathsFor(settings.Output);

        if (!pipeline.CanSetup)
        {
            if (settings.Json)
            {
                var manual = new SetupReport(pipeline.Id, [], [pipeline.Summary], pipeline.BuildHint(project, paths));
                AnsiConsole.WriteLine(JsonSerializer.Serialize(manual, CliJson.Default.SetupReport));
                return 0;
            }
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(pipeline.Title)}[/] is detect-and-report only.");
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(pipeline.Summary)}[/]");
            AnsiConsole.MarkupLine($"Your input is [white]{paths.Input}[/]; build with [white]{Markup.Escape(pipeline.BuildHint(project, paths))}[/].");
            return 0;
        }

        var result = await pipeline.SetupAsync(project, paths);
        return Report(result, settings.Json);
    }

    internal static int Report(PipelineSetupResult result, bool json)
    {
        if (json)
        {
            var dto = new SetupReport(result.PipelineId, result.ChangedFiles, result.Notes, result.BuildHint);
            AnsiConsole.WriteLine(JsonSerializer.Serialize(dto, CliJson.Default.SetupReport));
            return 0;
        }

        foreach (var file in result.ChangedFiles)
            AnsiConsole.MarkupLine($"  [green]~[/] {Markup.Escape(file)}");
        foreach (var note in result.Notes)
            AnsiConsole.MarkupLine($"  [grey]›[/] {Markup.Escape(note)}");
        AnsiConsole.MarkupLine($"[green]Pipeline[/] [cyan]{result.PipelineId}[/] ready. Build: [white]{Markup.Escape(result.BuildHint)}[/]");
        return 0;
    }
}

/// <summary>Fetches the standalone Tailwind binary. Not implemented yet.</summary>
public sealed class TailwindFetchCommand : AsyncCommand<GlobalSettings>
{
    /// <inheritdoc />
    public override Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings)
    {
        AnsiConsole.MarkupLine("[yellow]'tailwind fetch' is not implemented yet.[/]");
        AnsiConsole.MarkupLine("[grey]Download the standalone binary from https://github.com/tailwindlabs/tailwindcss/releases[/]");
        AnsiConsole.MarkupLine($"[grey]and place it at {StandalonePipeline.Dir}/tailwindcss (or tailwindcss.exe on Windows).[/]");
        return Task.FromResult(0);
    }
}
