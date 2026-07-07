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
            Console.Out.WriteLine(JsonSerializer.Serialize<IReadOnlyList<PipelineReport>>(report, CliJson.Default.IReadOnlyListPipelineReport));
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

        if (settings.Silent)
            return Task.FromResult(0);

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
            if (settings.Json)
                Console.Out.WriteLine($$"""{"error":"unknown-mode","mode":{{JsonSerializer.Serialize(settings.Mode)}}}""");
            else
                settings.Warn($"[red]Unknown mode '{Markup.Escape(settings.Mode)}'.[/] Options: {string.Join(", ", registry.All.Select(p => p.Id))}.");
            return 1;
        }

        var paths = TailwindPipelineSupport.PathsFor(settings.Output);

        if (!pipeline.CanSetup)
        {
            if (settings.Json)
            {
                var manual = new SetupReport(pipeline.Id, [], [pipeline.Summary], pipeline.BuildHint(project, paths));
                Console.Out.WriteLine(JsonSerializer.Serialize(manual, CliJson.Default.SetupReport));
                return 0;
            }
            settings.Line($"[yellow]{Markup.Escape(pipeline.Title)}[/] is detect-and-report only.");
            settings.Line($"[grey]{Markup.Escape(pipeline.Summary)}[/]");
            settings.Line($"Your input is [white]{paths.Input}[/]; build with [white]{Markup.Escape(pipeline.BuildHint(project, paths))}[/].");
            return 0;
        }

        var result = await pipeline.SetupAsync(project, paths);
        return Report(result, settings.Json, settings.Silent);
    }

    internal static int Report(PipelineSetupResult result, bool json, bool silent = false)
    {
        if (json)
        {
            var dto = new SetupReport(result.PipelineId, result.ChangedFiles, result.Notes, result.BuildHint);
            Console.Out.WriteLine(JsonSerializer.Serialize(dto, CliJson.Default.SetupReport));
            return 0;
        }

        if (silent)
            return 0;

        foreach (var file in result.ChangedFiles)
            AnsiConsole.MarkupLine($"  [green]~[/] {Markup.Escape(file)}");
        foreach (var note in result.Notes)
            AnsiConsole.MarkupLine($"  [grey]›[/] {Markup.Escape(note)}");
        AnsiConsole.MarkupLine($"[green]Pipeline[/] [cyan]{result.PipelineId}[/] ready. Build: [white]{Markup.Escape(result.BuildHint)}[/]");
        return 0;
    }
}

/// <summary>Settings for <c>tailwind fetch</c>.</summary>
public sealed class TailwindFetchSettings : GlobalSettings
{
    /// <summary>Release to fetch: <c>latest</c> or a tag like <c>v4.1.11</c>.</summary>
    [CommandOption("--tw-version <VERSION>")]
    [Description("Tailwind release to fetch (latest or a tag like v4.1.11).")]
    [DefaultValue("latest")]
    public string Version { get; init; } = "latest";

    /// <summary>Use the musl (Alpine) Linux build. Auto-detected when omitted.</summary>
    [CommandOption("--musl")]
    [Description("Use the musl (Alpine) Linux build.")]
    public bool Musl { get; init; }

    /// <summary>Re-download even if the binary already exists.</summary>
    [CommandOption("-f|--force")]
    [Description("Re-download even if the binary already exists.")]
    public bool Force { get; init; }
}

/// <summary>Downloads the Tailwind standalone binary for this OS/arch into <c>.blaizio/</c>.</summary>
public sealed class TailwindFetchCommand : AsyncCommand<TailwindFetchSettings>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, TailwindFetchSettings settings)
    {
        var cwd = settings.ResolvedCwd;
        var musl = settings.Musl || TailwindBinary.IsMusl();
        string asset;
        try
        {
            asset = TailwindBinary.AssetName(musl);
        }
        catch (PlatformNotSupportedException ex)
        {
            settings.Warn($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        var target = TailwindBinary.LocalPath(cwd);
        var alreadyPresent = File.Exists(target) && !settings.Force;

        if (!alreadyPresent)
        {
            if (settings.Json || settings.Silent)
            {
                await TailwindBinary.FetchAsync(cwd, settings.Version, musl, settings.Force, Http);
            }
            else
            {
                await AnsiConsole.Progress()
                    .Columns(
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new DownloadedColumn(),
                        new SpinnerColumn())
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask($"Fetching {asset}", maxValue: 100);
                        var progress = new Progress<DownloadProgress>(p =>
                        {
                            if (p.TotalBytes is > 0)
                            {
                                task.MaxValue = p.TotalBytes.Value;
                                task.Value = p.BytesRead;
                            }
                        });
                        await TailwindBinary.FetchAsync(cwd, settings.Version, musl, settings.Force, Http, progress);
                        task.Value = task.MaxValue;
                    });
            }

            EnsureGitignored(cwd);
        }

        var bytes = new FileInfo(target).Length;

        if (settings.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(
                new FetchReport(TailwindBinary.LocalPath(cwd).Replace('\\', '/'), asset, bytes, alreadyPresent),
                CliJson.Default.FetchReport));
            return 0;
        }

        if (alreadyPresent)
            settings.Line($"[grey]Already present:[/] {Markup.Escape(target)} [grey](use --force to re-download)[/]");
        else
            settings.Line($"[green]Fetched[/] {Markup.Escape(asset)} → {Markup.Escape(target)} ({bytes / 1_000_000.0:0.0} MB)");
        settings.Line("[grey]CSS now compiles on 'dotnet build' / 'dotnet watch'.[/]");
        return 0;
    }

    /// <summary>Add the binary to the project's .gitignore so the large per-OS file is never committed.</summary>
    private static void EnsureGitignored(string projectDir)
    {
        var gitignore = Path.Combine(projectDir, ".gitignore");
        const string pattern = ".blaizio/tailwindcss*";
        var lines = File.Exists(gitignore) ? File.ReadAllLines(gitignore).ToList() : [];
        if (lines.Any(l => l.Trim() == pattern))
            return;

        if (lines.Count > 0 && lines[^1].Trim().Length > 0)
            lines.Add(string.Empty);
        lines.Add("# Blaizio: platform-specific Tailwind standalone binary");
        lines.Add(pattern);
        File.WriteAllLines(gitignore, lines);
    }
}
