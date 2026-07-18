using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Core.Writing;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>add</c>.</summary>
public sealed class AddSettings : GlobalSettings
{
    /// <summary>Item names, URLs or local paths to add. Empty triggers the interactive picker.</summary>
    [CommandArgument(0, "[components...]")]
    [Description("Component names, @namespace/component references, URLs or local paths to add")]
    public string[] Components { get; init; } = [];

    /// <summary>Add every component in the registry.</summary>
    [CommandOption("-a|--all")]
    [Description("Add all available components (default: false)")]
    public bool All { get; init; }

    /// <summary>Overwrite files that already exist.</summary>
    [CommandOption("--overwrite")]
    [Description("Overwrite existing files (default: false)")]
    public bool Overwrite { get; init; }

    /// <summary>Delete orphaned files in the output directory (requires <c>--all</c>).</summary>
    [CommandOption("--prune")]
    [Description("Delete files in the output directory no registry item ships (requires --all)")]
    public bool Prune { get; init; }

    /// <summary>Destination directory override (defaults to the configured output). Named
    /// <c>-o|--output</c> to match <c>init</c>.</summary>
    [CommandOption("-o|--output <dir>")]
    [Description("Destination directory (defaults to the configured output)")]
    public string? Output { get; init; }

    /// <summary>Namespace override. Exposed as <c>-ns</c> (rewritten to <c>--namespace</c> in Program).</summary>
    [CommandOption("--namespace <ns>")]
    [Description("Root namespace for copied components (defaults to the configured namespace)")]
    public string? Namespace { get; init; }

    /// <summary>Custom Tailwind input for bundler setups (recorded as blaizio.json <c>css</c>).</summary>
    [CommandOption("--css <path>")]
    [Description("Tailwind input file the Blaizio imports are wired into, for bundler setups (default: the CLI-managed Styles/app.css)")]
    public string? Css { get; init; }

    /// <summary>Component skin, forwarded to the init wiring (see <see cref="InitSettings.Style"/>).
    /// On an initialized project this re-records the skin only - swapping the look of installed
    /// components is <c>apply</c>'s job.</summary>
    [CommandOption("--style <name>")]
    [Description("Component style (skin): ash, aura, ember, flint, forge, glow, spark, wisp (default: ember)")]
    public string? Style { get; init; }

    /// <summary>Tailwind pipeline to wire, forwarded to the init wiring. <see langword="null"/>
    /// (unset) means "auto" on a fresh project and "leave as wired" on an initialized one.</summary>
    [CommandOption("--tailwind <mode>")]
    [Description("Tailwind pipeline: auto, standalone, node, vite, rollup, postcss, none (default: auto)")]
    public string? Tailwind { get; init; }

    /// <summary>Wire up RTL support, forwarded to the init wiring.</summary>
    [CommandOption("--rtl")]
    [Description("Enable RTL support")]
    public bool Rtl { get; init; }

    /// <summary>Pointer cursor on buttons, forwarded to the init wiring.</summary>
    [CommandOption("--pointer")]
    [Description("Use a pointer cursor for buttons")]
    public bool Pointer { get; init; }

    /// <summary>Use defaults with no prompts in the init wiring leg.</summary>
    [CommandOption("-d|--defaults")]
    [Description("Use defaults without prompting (default: false)")]
    public bool Defaults { get; init; }

    /// <summary>Re-init from scratch (overwrite blaizio.json) before adding.</summary>
    [CommandOption("-f|--force")]
    [Description("Force overwrite of existing configuration blaizio.json (default: false)")]
    public bool Force { get; init; }

    /// <summary>Preset (name or /create code) folded into the add, so customizing doesn't need a
    /// follow-up <c>apply</c> run. Takes precedence over the preset recorded in blaizio.json.</summary>
    [CommandOption("-p|--preset <name|code>")]
    [Description("Color preset name or Create preset code to apply as part of the add (takes precedence over blaizio.json)")]
    public string? Preset { get; init; }

    /// <summary>Resolve and report without writing or installing anything.</summary>
    [CommandOption("--dry-run")]
    [Description("Preview changes without writing files (default: false)")]
    public bool DryRun { get; init; }

    /// <summary>Skip NuGet installs and transitive registry dependencies.</summary>
    [CommandOption("--no-deps")]
    [Description("Skip NuGet packages and registry dependencies")]
    public bool NoDeps { get; init; }

    /// <summary>Skip only the NuGet install, keeping transitive registry dependencies. For projects
    /// that reference Blaizio.Base/Icons another way (e.g. ProjectReference in a monorepo).</summary>
    [CommandOption("--no-nuget")]
    [Description("Skip NuGet installs but keep registry dependencies (e.g. ProjectReference setups)")]
    public bool NoNuget { get; init; }

    /// <summary>Re-pull installed components (all when none given) and refresh the managed styling.
    /// Absorbs the deprecated <c>update</c> command.</summary>
    [CommandOption("--update")]
    [Description("Re-pull installed components, overwriting local copies, and refresh the managed styling (default: false)")]
    public bool Update { get; init; }

    /// <summary>Bump the Blaizio packages then re-pull installed components. Absorbs the deprecated
    /// <c>upgrade</c> command.</summary>
    [CommandOption("--upgrade")]
    [Description("Bump the Blaizio packages to this tool's versions, then re-pull installed components (default: false)")]
    public bool Upgrade { get; init; }

    /// <summary>Show the upstream diff instead of writing; the optional value filters to one file path.</summary>
    [CommandOption("--diff [path]")]
    [Description("Show diff for a file")]
    public FlagValue<string?> Diff { get; init; } = new();

    /// <summary>Print file contents instead of writing; the optional value filters to one file path.</summary>
    [CommandOption("--view [path]")]
    [Description("Show file contents")]
    public FlagValue<string?> View { get; init; } = new();

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
        // -p/--preset folds the styling `apply` into the add: no more waiting for add to finish
        // just to run a second command. Applied first — before an --update/--upgrade re-pull — on
        // an initialized project; a project without blaizio.json gets the preset through the init
        // bootstrap below instead. Read-only modes must not re-style as a side effect.
        var applyPreset = settings.Preset is not null
            && !settings.Diff.IsSet && !settings.View.IsSet;
        if (applyPreset && settings.DryRun)
        {
            settings.Warn("[yellow]--preset is ignored with --dry-run (applying re-styles the project).[/]");
            applyPreset = false;
        }
        if (applyPreset && await ConfigStore.LoadAsync(settings.ResolvedCwd, CliCancellation.Token) is not null)
        {
            var exit = await new ApplyCommand().ExecuteAsync(context, new ApplySettings
            {
                Cwd = settings.Cwd,
                // The flag itself is the consent - no second confirm. A --json add must stay a
                // single clean AddResult document: run the apply leg silently.
                Yes = true,
                Silent = settings.Silent || settings.Json,
                Registry = settings.Registry,
                Preset = settings.Preset,
            });
            if (exit != 0)
                return exit;

            // `add --preset <x>` alone is a complete operation - don't fall into the "nothing to
            // add" warning when no component work was requested non-interactively.
            if (settings.Components.Length == 0 && !settings.All && !settings.Update && !settings.Upgrade
                && settings.Css is null && settings.NonInteractive && !settings.Json)
                return 0;
        }

        // The absorbed modes first: they own their whole flow (and their own --json shapes).
        if (settings.Upgrade)
            return await new UpgradeCommand().ExecuteAsync(context, new UpgradeSettings
            {
                Cwd = settings.Cwd,
                Yes = settings.Yes,
                Silent = settings.Silent,
                Json = settings.Json,
                Registry = settings.Registry,
            });

        if (settings.Update)
            return await new UpdateCommand().ExecuteAsync(context, new UpdateSettings
            {
                Cwd = settings.Cwd,
                Yes = settings.Yes,
                Silent = settings.Silent,
                Json = settings.Json,
                Registry = settings.Registry,
                Components = settings.Components,
            });

        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);

        // add adopts an existing project: no blaizio.json yet means run the config-only init
        // (packages, CSS, host wiring - never a scaffold) and carry on with the component work.
        // An init-only flag (--style/--tailwind/--rtl/--pointer/--force) on an INITIALIZED project
        // runs the same wiring as a top-up, so add is a full superset of init - init stays only as
        // the explicit/interactive entry point. Read-only modes (--diff/--view) and --dry-run must
        // not write a config as a side effect.
        var bootstrapped = false;
        var wiringRequested = settings.Force || settings.Rtl || settings.Pointer
            || settings.Style is not null || settings.Tailwind is not null;
        if ((services.Config is null || wiringRequested)
            && !settings.DryRun && !settings.Diff.IsSet && !settings.View.IsSet)
        {
            if (services.Config is null)
                settings.Line($"No blaizio.json - initializing this project first.");
            var exit = await new InitCommand().ExecuteAsync(context, new InitSettings
            {
                Cwd = settings.Cwd,
                Yes = settings.Yes,
                // A --json add must stay a single clean AddResult document: run the init leg silently.
                Silent = settings.Silent || settings.Json,
                Registry = settings.Registry,
                Namespace = settings.Namespace,
                Output = settings.Output,
                Css = settings.Css,
                Preset = settings.Preset,
                Style = settings.Style,
                Tailwind = settings.Tailwind ?? "auto",
                Rtl = settings.Rtl,
                Pointer = settings.Pointer,
                Defaults = settings.Defaults,
                Force = settings.Force,
                AdoptOnly = true,
            });
            if (exit != 0)
                return exit;
            services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);
            bootstrapped = true;

            // A wiring-only run (`add --rtl`, `add --tailwind standalone`, ...) is a complete
            // operation - don't fall into the picker/"nothing to add" flow non-interactively.
            // Exception: an --rtl run still owes the direction cascade component (below).
            if (wiringRequested && settings.Components.Length == 0 && !settings.All
                && settings.NonInteractive && !settings.Json
                && !(settings.Rtl && !services.RequireConfig().Installed.ContainsKey("direction-provider")))
                return 0;
        }

        var config = services.RequireConfig();

        // --css on an initialized project records the bundler input and syncs the managed imports
        // into it right away (the bootstrap leg above already did both for a fresh project).
        if (settings.Css is { } cssPath && !bootstrapped)
        {
            if (!File.Exists(Path.Combine(settings.ResolvedCwd, cssPath)))
            {
                CliOutput.Error.MarkupLine(
                    $"[red]Error:[/] The css input '{Markup.Escape(cssPath)}' (--css) does not exist. Pass the path of your bundler's Tailwind input file.");
                return 1;
            }

            config.Css = cssPath;
            await ConfigStore.SaveAsync(settings.ResolvedCwd, config, ct);
            if (config.Ejected)
            {
                // The ejected contract lives inline in the OLD input; syncing imports into the new
                // one would resurrect the dead .blaizio/ dependency. Re-pointing is recorded only.
                settings.Warn($"  [yellow]css[/] recorded {Markup.Escape(cssPath)} - the project is ejected, so no imports were synced. Move your ejected contract into the new input yourself.");
            }
            else
            {
                var synced = await new TailwindSetup(new EmbeddedCssAssets()).EnsureAsync(
                    settings.ResolvedCwd, config.Output,
                    new TailwindOptions(Rtl: config.Rtl), config.Preset, cssInput: cssPath,
                    chart: config.Chart ?? "default", radius: config.Radius ?? "default", ct: ct);
                settings.Line($"  [blue]css[/] recorded and synced {Markup.Escape(synced.InputPath)} (preset [cyan]{Markup.Escape(config.Preset)}[/])");
            }

            // `add --css <path>` alone is a complete operation - don't fall into the picker/"nothing
            // to add" flow when no component work was requested non-interactively.
            if (settings.Components.Length == 0 && !settings.All && settings.NonInteractive && !settings.Json)
                return 0;
        }

        if (settings.Diff.IsSet)
            return await ShowDiffAsync(services, config, settings, ct);
        if (settings.View.IsSet)
            return await ShowFilesAsync(services, settings, ct);

        if (services.Project.IsBareClassLibrary)
            settings.Warn(
                "[yellow]This looks like a bare class library (Microsoft.NET.Sdk)[/] - copied components won't compile without the Razor SDK and the ASP.NET Core framework reference. Run [white]blaizio add --force[/] to patch the csproj.");

        var components = await ResolveRequestedAsync(services, settings);

        // RTL projects need the direction cascade component: blaizio.json's rtl flag only readies
        // the skins (logical properties), but a layout flips direction via BzDirectionProvider -
        // auto-pull it when RTL is being ENABLED (the init bootstrap or an explicit --rtl), so
        // RTL=y is complete out of the box. Deliberately NOT on every later add: uninstalling the
        // provider must stick (undo-by-record) - only a fresh --rtl re-adds it. Registries that
        // don't ship the item (third-party/minimal) are left alone.
        if (config.Rtl
            && (bootstrapped || settings.Rtl)
            && !config.Installed.ContainsKey("direction-provider")
            && !components.Contains("direction-provider", StringComparer.OrdinalIgnoreCase))
        {
            var index = await services.Registry.GetIndexAsync();
            if (index.Items.Any(i => string.Equals(i.Name, "direction-provider", StringComparison.OrdinalIgnoreCase)))
            {
                components = [.. components, "direction-provider"];
                settings.Line("  [blue]rtl[/] adding [cyan]direction-provider[/] - the direction cascade RTL layouts flip with");
            }
            else if (components.Count == 0)
            {
                return 0; // the --rtl fall-through owed only the provider; this registry has none
            }
        }

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
            // "All" means all components - font items are styling choices, not a set to bulk-install
            // (two of them would just overwrite each other's half of the selection anyway).
            var index = await services.Registry.GetIndexAsync();
            return [.. index.Items.Where(i => i.Type != Core.Registry.ItemType.Font).Select(i => i.Name)];
        }

        if (settings.Components.Length > 0)
            return settings.Components;

        if (settings.NonInteractive)
            return [];

        return await ComponentPrompts.PickAsync(services.Registry, "Select components to [green]add[/]:");
    }

    /// <summary>
    /// <c>add --diff</c>: compare the requested components (default: all installed) against the
    /// registry without writing. The optional value filters to a single file path. Exit 1 on drift,
    /// like <c>git diff --exit-code</c>.
    /// </summary>
    private static async Task<int> ShowDiffAsync(
        CliServices services, Core.Configuration.BlaizioConfig config, AddSettings settings, CancellationToken ct)
    {
        if (settings.Components.Length == 0 && config.Installed.Count == 0)
        {
            settings.Warn("[yellow]No installed components recorded in blaizio.json.[/] Run [white]blaizio add <component>[/] first.");
            if (settings.Json)
                Console.Out.WriteLine("""{"items":[],"hasDrift":false}""");
            return 0;
        }

        var result = await new DiffService(services.Registry, services.Project, config)
            .RunAsync(settings.Components, ct);

        var pathFilter = settings.Diff.Value;
        bool Matches(string path) =>
            string.IsNullOrWhiteSpace(pathFilter) || path.Contains(pathFilter, StringComparison.OrdinalIgnoreCase);

        var drift = false;
        if (settings.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(result, CoreJson.Default.DiffResult));
            return result.HasDrift ? 1 : 0;
        }

        foreach (var item in result.Items)
        {
            var files = item.Files.Where(f => f.Status is not DiffStatus.Unchanged && Matches(f.Path)).ToArray();
            if (files.Length == 0)
            {
                if (string.IsNullOrWhiteSpace(pathFilter) && !settings.Silent)
                    AnsiConsole.MarkupLine($"  [green]=[/] [cyan]{Markup.Escape(item.Name)}[/] up to date");
                continue;
            }

            drift = true;
            if (settings.Silent)
                continue;
            AnsiConsole.MarkupLine($"  [yellow]~[/] [cyan]{Markup.Escape(item.Name)}[/]");
            foreach (var file in files)
            {
                var (glyph, color, label) = file.Status switch
                {
                    DiffStatus.Missing => ("-", "red", "missing"),
                    _ => ("~", "yellow", "changed"),
                };
                AnsiConsole.MarkupLine($"      [{color}]{glyph}[/] {Markup.Escape(file.Path)} [grey]({label})[/]");
            }
        }

        if (!settings.Silent)
            AnsiConsole.MarkupLine(drift
                ? "[yellow]Drift found.[/] Re-pull with [white]blaizio add <component> --overwrite[/] (overwrites local edits)."
                : "[green]Everything matches upstream.[/]");
        return drift ? 1 : 0;
    }

    /// <summary>
    /// <c>add --view</c>: print the requested components' registry files without writing. The
    /// optional value filters to a single file path.
    /// </summary>
    private static async Task<int> ShowFilesAsync(CliServices services, AddSettings settings, CancellationToken ct)
    {
        if (settings.Components.Length == 0)
        {
            settings.Warn("[yellow]Nothing to view - name a component:[/] [white]blaizio add <component> --view[/]");
            return 1;
        }

        var pathFilter = settings.View.Value;
        bool Matches(string path) =>
            string.IsNullOrWhiteSpace(pathFilter) || path.Contains(pathFilter, StringComparison.OrdinalIgnoreCase);

        if (settings.Json)
        {
            var nodes = new System.Text.Json.Nodes.JsonArray();
            foreach (var reference in settings.Components)
            {
                var fetched = await services.Registry.GetItemAsync(reference, ct);
                nodes.Add(JsonSerializer.SerializeToNode(fetched, CoreJson.Default.RegistryItem));
            }
            Console.Out.WriteLine(nodes.ToJsonString());
            return 0;
        }

        foreach (var reference in settings.Components)
        {
            var item = await services.Registry.GetItemAsync(reference, ct);
            if (settings.Silent)
                continue;

            AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(item.Name)}[/]").LeftJustified());
            foreach (var file in item.Files.Where(f => Matches(f.Path)))
            {
                AnsiConsole.Write(new Rule($"[grey]{Markup.Escape(file.Path)}[/]").LeftJustified().RuleStyle("grey"));
                AnsiConsole.WriteLine(file.Content ?? "(no content)");
            }
        }

        return 0;
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
