using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Writing;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>add</c>.</summary>
public sealed class AddSettings : GlobalSettings
{
    /// <summary>Item names, URLs or local paths to add. Empty triggers the interactive picker.</summary>
    [CommandArgument(0, "[COMPONENTS]")]
    [Description("Component names, URLs or local paths to add.")]
    public string[] Components { get; init; } = [];

    /// <summary>Add every component in the registry.</summary>
    [CommandOption("-a|--all")]
    [Description("Add every registry component.")]
    public bool All { get; init; }

    /// <summary>Overwrite files that already exist.</summary>
    [CommandOption("--overwrite")]
    [Description("Overwrite existing files.")]
    public bool Overwrite { get; init; }

    /// <summary>Delete orphaned files in the output directory (requires <c>--all</c>).</summary>
    [CommandOption("--prune")]
    [Description("Delete files in the output directory no registry item ships (requires --all).")]
    public bool Prune { get; init; }

    /// <summary>Destination directory override (defaults to the configured output). Named
    /// <c>-o|--output</c> to match <c>init</c>.</summary>
    [CommandOption("-o|--output <DIR>")]
    [Description("Destination directory (defaults to the configured output).")]
    public string? Output { get; init; }

    /// <summary>Namespace override. Exposed as <c>-ns</c> (rewritten to <c>--namespace</c> in Program).</summary>
    [CommandOption("--namespace <NS>")]
    [Description("Root namespace for copied components (also -ns).")]
    public string? Namespace { get; init; }

    /// <summary>Resolve and report without writing or installing anything.</summary>
    [CommandOption("--dry-run")]
    [Description("Preview the plan without writing files.")]
    public bool DryRun { get; init; }

    /// <summary>Skip NuGet installs and transitive registry dependencies.</summary>
    [CommandOption("--no-deps")]
    [Description("Skip NuGet packages and registry dependencies.")]
    public bool NoDeps { get; init; }

    /// <summary>Skip only the NuGet install, keeping transitive registry dependencies. For projects
    /// that reference Blaizio.Base/Icons another way (e.g. ProjectReference in a monorepo).</summary>
    [CommandOption("--no-nuget")]
    [Description("Skip NuGet installs but keep registry dependencies (e.g. ProjectReference setups).")]
    public bool NoNuget { get; init; }

    /// <inheritdoc />
    public override ValidationResult Validate() =>
        Prune && !All
            ? ValidationResult.Error("--prune requires --all: a partial add can't know the full expected file set.")
            : base.Validate();
}

/// <summary>Adds one or more components (and their dependencies) into the project.</summary>
public sealed class AddCommand : AsyncCommand<AddSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, AddSettings settings)
    {
        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);
        var config = services.RequireConfig();

        if (services.Project.IsBareClassLibrary)
            settings.Warn(
                "[yellow]This looks like a bare class library (Microsoft.NET.Sdk)[/] — copied components won't compile without the Razor SDK and the ASP.NET Core framework reference. Run [white]blaizio init --force[/] to patch the csproj.");

        var components = await ResolveRequestedAsync(services, settings);
        if (components.Count == 0)
        {
            // --json callers still get a (empty) result document, never markup.
            if (settings.Json)
                return EmitJson(new AddResult
                {
                    Items = [],
                    NugetPackages = [],
                    Files = [],
                    Namespace = config.Namespace,
                    ImportsUpdated = false,
                    DryRun = settings.DryRun,
                });
            settings.Warn("[yellow]Nothing to add.[/]");
            return 0;
        }

        var request = new AddRequest
        {
            Components = components,
            Overwrite = settings.Overwrite,
            Prune = settings.Prune,
            DryRun = settings.DryRun,
            NoDeps = settings.NoDeps,
            NoNuget = settings.NoNuget,
            NamespaceOverride = settings.Namespace,
            PathOverride = settings.Output,
        };

        var service = new AddService(services.Registry, services.Project, config, services.Dotnet);

        AddResult result;
        if (settings.Json || settings.Silent)
        {
            result = await service.RunAsync(request, ct: ct);
        }
        else
        {
            AddResult? captured = null;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Resolving...", async ctx =>
                {
                    var progress = new Progress<string>(msg => ctx.Status(Markup.Escape(msg)));
                    captured = await service.RunAsync(request, progress, ct);
                });
            result = captured!;
        }

        if (settings.Json)
            return EmitJson(result);
        return settings.Silent ? 0 : Report(result);
    }

    /// <summary>Decide which components to install: <c>--all</c>, positional args, or an interactive picker.</summary>
    private static async Task<IReadOnlyList<string>> ResolveRequestedAsync(CliServices services, AddSettings settings)
    {
        if (settings.All)
        {
            var index = await services.Registry.GetIndexAsync();
            return [.. index.Items.Select(i => i.Name)];
        }

        if (settings.Components.Length > 0)
            return settings.Components;

        if (settings.NonInteractive)
            return [];

        return await ComponentPrompts.PickAsync(services.Registry, "Select components to [green]add[/]:");
    }

    private static int EmitJson(AddResult result)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(result, CoreJson.Default.AddResult));
        return 0;
    }

    private static int Report(AddResult result)
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
            AnsiConsole.MarkupLine($"  [{color}]{glyph}[/] {Markup.Escape(file.RelativePath)}");
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
