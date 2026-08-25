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
    /// <summary>The compile input/output for a project: the recorded tokens file (blaizio.json
    /// <c>css</c>) when the project has one, else the default input <c>init</c> writes.</summary>
    public static TailwindPaths PathsFor(string? outputOverride, string? cssInput = null)
        => new(cssInput?.Replace('\\', '/') ?? "Styles/app.css", outputOverride ?? "wwwroot/app.css");
}

/// <summary>Reports which Tailwind pipelines are present and which is recommended.</summary>
public sealed class TailwindDetectCommand : ProjectCommand<GlobalSettings>
{
    /// <inheritdoc />
    protected override Task<int> ExecuteInProjectAsync(CommandContext context, GlobalSettings settings, CancellationToken cancellationToken)
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
    /// <summary>Pipeline to wire: auto, standalone, node, vite, rollup, postcss, none.</summary>
    [CommandOption("-m|--mode <mode>")]
    [Description("Pipeline: auto, standalone, node, vite, rollup, postcss, none.")]
    [DefaultValue("auto")]
    public string Mode { get; init; } = "auto";

    /// <summary>Compiled CSS output path (node/standalone).</summary>
    [CommandOption("-o|--output <path>")]
    [Description("Compiled CSS output path (default: wwwroot/app.css).")]
    public string? Output { get; init; }
}

/// <summary>Wires a chosen Tailwind pipeline into the project (or reports manual steps for bundlers).</summary>
public sealed class TailwindSetupCommand : ProjectCommand<TailwindSetupSettings>
{
    /// <inheritdoc />
    protected override async Task<int> ExecuteInProjectAsync(CommandContext context, TailwindSetupSettings settings, CancellationToken cancellationToken)
    {
        var project = ProjectContext.Discover(settings.ResolvedCwd);
        var config = await Core.Configuration.ConfigStore.LoadAsync(settings.ResolvedCwd, CliCancellation.Token);
        var registry = new TailwindPipelineRegistry();

        var pipeline = settings.Mode.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? registry.Recommend(project)
            : registry.Resolve(settings.Mode);

        if (pipeline is null)
        {
            if (settings.Json)
                Console.Out.WriteLine(new System.Text.Json.Nodes.JsonObject
                {
                    ["error"] = "unknown-mode",
                    ["mode"] = settings.Mode,
                }.ToJsonString());
            else
                settings.Warn($"[red]Unknown mode '{Markup.Escape(settings.Mode)}'.[/] Options: {string.Join(", ", registry.All.Select(p => p.Id))}.");
            return 1;
        }

        var paths = TailwindPipelineSupport.PathsFor(settings.Output, config?.Css);

        if (!pipeline.CanSetup)
        {
            if (settings.Json)
            {
                var manual = new SetupReport(pipeline.Id, [], [pipeline.Summary], pipeline.BuildHint(project, paths));
                Console.Out.WriteLine(JsonSerializer.Serialize(manual, CliJson.Default.SetupReport));
                return 0;
            }
            settings.Line($"[yellow]{Markup.Escape(pipeline.Title)}[/] is detect-and-report only.");
            settings.Line(Markup.Escape(pipeline.Summary));
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
public sealed class TailwindFetchSettings : ConfirmSettings
{
    /// <summary>Release to fetch: a tag like <c>v4.1.11</c>, or <c>latest</c>. Defaults to the
    /// pinned release this tool ships with (reproducible + checksum-safe).</summary>
    [CommandOption("--tw-version <version>")]
    [Description("Tailwind release to fetch (a tag, or 'latest'; default: the pinned release).")]
    [DefaultValue(TailwindBinary.DefaultVersion)]
    public string Version { get; init; } = TailwindBinary.DefaultVersion;

    /// <summary>Use the musl (Alpine) Linux build. Auto-detected when omitted.</summary>
    [CommandOption("--musl")]
    [Description("Use the musl (Alpine) Linux build.")]
    public bool Musl { get; init; }

    /// <summary>Re-download even if the binary already exists.</summary>
    [CommandOption("-f|--force")]
    [Description("Re-download even if the binary already exists.")]
    public bool Force { get; init; }
}

/// <summary>
/// Downloads the Tailwind standalone binary for this OS/arch into the per-user shared cache —
/// one download serves every project. Interactive runs show the size and confirm first
/// (<c>-y</c>/<c>--json</c>/<c>--silent</c> skip the prompt).
/// </summary>
public sealed class TailwindFetchCommand : AsyncCommand<TailwindFetchSettings>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, TailwindFetchSettings settings, CancellationToken cancellationToken)
    {
        var ct = CliCancellation.Token;
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

        var cached = TailwindBinary.CachedBinaryPath(settings.Version, musl);
        var willDownload = settings.Force || !File.Exists(cached);

        // A download is a real cost — show the size and ask first when a human is driving.
        // Non-TTY runs (CI, pipes) can't prompt; they proceed like -y rather than crashing.
        if (willDownload && !settings.NonInteractive && AnsiConsole.Profile.Capabilities.Interactive)
        {
            var size = await TailwindBinary.GetDownloadSizeAsync(settings.Version, musl, Http, ct);
            var sizeText = size is > 0 ? $"{size.Value / 1_000_000.0:0.0} MB" : "size unknown";
            if (!AnsiConsole.Confirm($"Download [cyan]{Markup.Escape(asset)}[/] ({sizeText}) into the shared cache?"))
            {
                // Declining a prompt is a choice, not a failure - every other confirm exits 0.
                settings.Warn("[yellow]Fetch cancelled.[/]");
                return 0;
            }
        }

        FetchResult fetch;
        if (!willDownload || settings.Json || settings.Silent)
        {
            fetch = await TailwindBinary.FetchAsync(settings.Version, musl, settings.Force, Http, ct: ct);
        }
        else
        {
            FetchResult captured = default;
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
                    captured = await TailwindBinary.FetchAsync(settings.Version, musl, settings.Force, Http, progress, ct: ct);
                    task.Value = task.MaxValue;
                });
            fetch = captured;
        }

        var bytes = new FileInfo(fetch.Path).Length;

        if (settings.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(
                new FetchReport(fetch.Path.Replace('\\', '/'), asset, bytes, fetch.FromCache, fetch.Verified),
                CliJson.Default.FetchReport));
            return 0;
        }

        if (fetch.FromCache)
        {
            settings.Line($"Already cached: {Markup.Escape(fetch.Path)} [grey](shared by all projects; --force re-downloads)[/]");
            if (fetch.Verified)
                settings.Line("[green]sha256 verified[/] against the cached checksum.");
        }
        else
        {
            settings.Line($"[green]Fetched[/] {Markup.Escape(asset)} → {Markup.Escape(fetch.Path)} ({bytes / 1_000_000.0:0.0} MB, shared cache)");
            if (fetch.Verified)
                settings.Line("[green]sha256 verified[/] against the release manifest.");
            else
                settings.Warn("[yellow]sha256 not verified[/] (checksum manifest unavailable for this release).");
        }
        settings.Line("CSS now compiles on 'dotnet build' / 'dotnet watch'.");
        return 0;
    }
}
