using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Core.Styling.Pipelines;
using Blaizio.Cli.Core.Templates;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Project templates <c>init</c> can target. Full scaffolding lands with the registry templates.</summary>
public enum InitTemplate
{
    /// <summary>Full practical demo app (dashboard, forms, overlays, data, auth).</summary>
    Showcase,

    /// <summary>Blazor Web App (Server / WASM / Auto).</summary>
    WebApp,

    /// <summary>Standalone WebAssembly app.</summary>
    Wasm,

    /// <summary>Component class library (no host).</summary>
    Library,
}

/// <summary>Settings for <c>init</c>.</summary>
public sealed class InitSettings : GlobalSettings
{
    /// <summary>Components to add immediately after initialization.</summary>
    [CommandArgument(0, "[components...]")]
    [Description("Component names, URLs or local paths to add right after initializing")]
    public string[] Components { get; init; } = [];

    /// <summary>Project template to scaffold.</summary>
    [CommandOption("-t|--template <template>")]
    [Description("Project template: showcase, webapp, wasm, library")]
    public InitTemplate? Template { get; init; }

    /// <summary>New project name (for scaffolding templates).</summary>
    [CommandOption("-n|--name <name>")]
    [Description("Project name for a scaffolded template (default: the assembly/directory name)")]
    public string? Name { get; init; }

    /// <summary>Root namespace for copied components. Exposed as <c>-ns</c> too (rewritten to
    /// <c>--namespace</c> in Program; the help provider renders the alias).</summary>
    [CommandOption("--namespace <ns>")]
    [Description("Root namespace for copied components (default: blaizio.json, else RootNamespace + \".Components.Ui\")")]
    public string? Namespace { get; init; }

    /// <summary>Component output directory.</summary>
    [CommandOption("-o|--output <dir>")]
    [Description("Directory copied components are written to (default: Components/Ui)")]
    public string? Output { get; init; }

    /// <summary>Custom Tailwind input for bundler setups (recorded as blaizio.json <c>css</c>).</summary>
    [CommandOption("--css <path>")]
    [Description("Tailwind input file the Blaizio imports are wired into, for bundler setups (default: the CLI-managed Styles/app.css)")]
    public string? Css { get; init; }

    /// <summary>Overwrite an existing blaizio.json.</summary>
    [CommandOption("-f|--force")]
    [Description("Force overwrite of existing configuration blaizio.json (default: false)")]
    public bool Force { get; init; }

    /// <summary>Use defaults with no prompts (template=showcase).</summary>
    [CommandOption("-d|--defaults")]
    [Description("Use defaults without prompting (default: false)")]
    public bool Defaults { get; init; }

    /// <summary>Wire up RTL support.</summary>
    [CommandOption("--rtl")]
    [Description("Enable RTL support")]
    public bool Rtl { get; init; }

    /// <summary>Enable pointer cursor on buttons.</summary>
    [CommandOption("--pointer")]
    [Description("Use a pointer cursor for buttons")]
    public bool Pointer { get; init; }

    /// <summary>Component skin (style-*): ash, aura, ember, flint, forge, glow, spark, wisp.</summary>
    [CommandOption("--style <name>")]
    [Description("Component style (skin): ash, aura, ember, flint, forge, glow, spark, wisp (default: ember)")]
    public string? Style { get; init; }

    /// <summary>Tailwind compile pipeline to wire: auto, standalone, node, vite, rollup, postcss, none.</summary>
    [CommandOption("--tailwind <mode>")]
    [Description("Tailwind pipeline: auto, standalone, node, vite, rollup, postcss, none (default: auto)")]
    [DefaultValue("auto")]
    public string Tailwind { get; init; } = "auto";

    /// <summary>Color preset (preset-*) by name - or a compact preset CODE from the docs /create
    /// page (e.g. <c>32r</c>), which expands to its style + preset + RTL parts.</summary>
    [CommandOption("-p|--preset <name|code>")]
    [Description("Color preset: nova (default), comet, eclipse, meteor, nebula, pulsar, quasar, solstice, zenith - or a /create preset code (e.g. 32r)")]
    public string? Preset { get; init; }

    /// <summary>Apply scope for an existing project: full re-init, theme tokens only, or font overlay
    /// only. Hidden back-compat for older docs /create snippets — `blaizio apply` is the command now.</summary>
    [CommandOption("--scope <scope>", IsHidden = true)]
    [Description("Apply scope for an existing project: full (default), theme, fonts")]
    public StyleScope Scope { get; init; } = StyleScope.Full;

    /// <summary>
    /// Set (programmatically, not a flag) when <c>add</c> adopts an uninitialized project: config +
    /// wiring only — never scaffold, never prompt for a template or components; <c>add</c> itself
    /// carries on with the component work.
    /// </summary>
    public bool AdoptOnly { get; init; }
}

/// <summary>Initializes a project: writes <c>blaizio.json</c>, installs packages, optionally adds components.</summary>
public sealed class InitCommand : AsyncCommand<InitSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, InitSettings settings)
    {
        var cwd = settings.ResolvedCwd;
        var ct = CliCancellation.Token;

        // An already-initialized project isn't an error: init tops up whatever is missing (packages,
        // CSS, host wiring, pipeline) around the recorded config, keeping its values (explicit flags
        // still override) and its installed map. --force starts over from scratch instead.
        var existing = settings.Force ? null : await ConfigStore.LoadAsync(cwd, ct);
        var topUp = existing is not null;
        if (topUp)
            settings.Line($"[grey]{BlaizioConfig.FileName} exists — adding missing Blaizio pieces (use [white]--force[/] to re-init).[/]");

        // Bundler mode's input must exist before anything is written: erroring later would leave a
        // half-applied init (config saved, packages installed) with a bad path recorded in it.
        var cssInput = settings.Css ?? existing?.Css;
        if (cssInput is not null && !File.Exists(Path.Combine(cwd, cssInput)))
        {
            var origin = settings.Css is not null ? "--css" : $"blaizio.json \"css\"";
            CliOutput.Error.MarkupLine(
                $"[red]Error:[/] The css input '{Markup.Escape(cssInput)}' ({origin}) does not exist. " +
                $"Pass the path of your bundler's Tailwind input file, e.g. [white]blaizio init --css path/to/tailwind.css[/]");
            return 1;
        }

        var project = ProjectContext.Discover(cwd);
        var interactive = !settings.NonInteractive && !settings.Defaults && !topUp;

        // No input recorded or passed: discover the project's own Tailwind input by content (any
        // file name - a .css importing tailwindcss). One hit adopts it as the bundler input; the
        // CLI-managed Styles/app.css, once present, pins the project to the default flow instead.
        if (cssInput is null && !File.Exists(Path.Combine(cwd, "Styles", "app.css")))
        {
            var found = TailwindInputLocator.Discover(cwd);
            if (found.Count == 1)
            {
                cssInput = found[0];
                settings.Line($"[grey]Found Tailwind input [cyan]{Markup.Escape(found[0])}[/] — wiring the Blaizio imports into it (bundler mode; [white]--css[/] overrides).[/]");
            }
            else if (found.Count > 1)
            {
                const string managedChoice = "Styles/app.css (CLI-managed input)";
                if (interactive)
                {
                    var pick = AnsiConsole.Prompt(new SelectionPrompt<string>()
                        .Title("Which Tailwind [green]input[/] should Blaizio wire into?")
                        .PageSize(10)
                        .AddChoices([.. found, managedChoice]));
                    if (pick != managedChoice)
                        cssInput = pick;
                }
                else
                {
                    settings.Warn($"[yellow]Multiple Tailwind inputs found[/] ({Markup.Escape(string.Join(", ", found))}) — pass [white]--css <path>[/] to pick one; using the CLI-managed Styles/app.css.");
                }
            }
        }

        // Non-interactive init without an explicit -t is config-only: scaffolding a whole app into
        // an arbitrary cwd is never a silent default. -d/--defaults opts into the Showcase default.
        // A top-up or an adopt (bootstrap from `add`) never scaffolds - the app already exists.
        InitTemplate? template = topUp || settings.AdoptOnly ? null : settings.Template
            ?? (settings.Defaults ? InitTemplate.Showcase : interactive ? PromptTemplate() : null);
        var projectName = settings.Name ?? project.AssemblyName;
        var scaffolded = template == InitTemplate.Showcase;
        var willScaffoldCsproj =
            template is InitTemplate.Showcase or InitTemplate.Library && project.CsprojPath is null;
        // A freshly scaffolded app roots at the project name, so derive the component namespace from
        // it (unless the user pinned one explicitly); an existing csproj keeps its own root namespace.
        var ns = willScaffoldCsproj && settings.Namespace is null
            ? $"{projectName}.Components.Ui"
            : NamespaceResolver.Resolve(settings.Namespace, config: existing, project);
        if (interactive && settings.Namespace is null)
            ns = AnsiConsole.Prompt(new TextPrompt<string>("Root [green]namespace[/]?").DefaultValue(ns));

        var output = settings.Output ?? existing?.Output ?? "Components/Ui";
        if (interactive && settings.Output is null)
            output = AnsiConsole.Prompt(new TextPrompt<string>("Output [green]directory[/]?").DefaultValue(output));

        var assets = new EmbeddedCssAssets();

        // --preset also accepts a compact code from the docs /create page ("32r"): expand it to
        // its style/preset/rtl parts. A real preset NAME always wins the (theoretical) ambiguity;
        // in practice no preset name decodes as a code. Explicit --style still overrides the
        // code's style. The code's font overlay is written to Styles/blaizio/fonts.css and its
        // chart/radius overlays to Styles/blaizio/tokens.css.
        PresetSelection? codeSelection = null;
        if (settings.Preset is { } presetArg
            && !string.Equals(presetArg, "nova", StringComparison.OrdinalIgnoreCase)
            && !assets.AvailablePresets.Any(p => string.Equals(p, presetArg, StringComparison.OrdinalIgnoreCase))
            && PresetCode.TryDecode(presetArg, out var decoded))
        {
            codeSelection = decoded;
            settings.Line($"[grey]Preset code [cyan]{Markup.Escape(presetArg.Trim())}[/] → style [cyan]{decoded.Style}[/], preset [cyan]{decoded.Preset}[/]{(decoded.Rtl ? ", [cyan]RTL[/]" : "")}.[/]");
        }

        var styleArg = settings.Style ?? codeSelection?.Style;
        var presetName = codeSelection?.Preset ?? settings.Preset;
        var skin = styleArg is null && existing is not null
            ? existing.Theme
            : ResolveSkin(styleArg, settings, interactive, assets);
        var preset = presetName is null && existing is not null
            ? existing.Preset
            : ResolvePreset(presetName, settings, interactive, assets);

        // A scoped apply (from the docs /create "Get Code" dialog) re-styles an existing project
        // without touching its host/packages/components: theme = skin+preset tokens, fonts = the
        // font overlay only.
        if (settings.Scope is StyleScope.Theme or StyleScope.Fonts)
            return await RunScopedAsync(cwd, settings, skin, preset, codeSelection, ct);

        var rtl = settings.Rtl || codeSelection?.Rtl == true || existing?.Rtl == true
            || (interactive && AnsiConsole.Confirm("Enable [green]RTL[/] support?", defaultValue: false));

        var config = existing ?? new BlaizioConfig { Namespace = ns };
        config.Namespace = ns;
        config.Output = output;
        config.Css = cssInput;
        config.Theme = skin;
        config.Preset = preset;
        config.Rtl = rtl;
        if (!string.IsNullOrWhiteSpace(settings.Registry))
            config.Registry = settings.Registry;
        config.Aliases["ui"] = ns;

        // Templates that ship a full app (Showcase) scaffold their project — writing a WASM csproj
        // with the package references when none exists, then the host/layout/page files.
        ScaffoldResult? scaffold = null;
        if (scaffolded)
        {
            if (willScaffoldCsproj)
            {
                await File.WriteAllTextAsync(Path.Combine(cwd, $"{projectName}.csproj"), ShowcaseCsproj(projectName), ct);
                project = ProjectContext.Discover(cwd);
            }

            var tokens = new TemplateTokens(
                willScaffoldCsproj ? projectName : project.RootNamespace, ns, projectName, skin);
            scaffold = await new TemplateScaffolder(new EmbeddedTemplates())
                .ScaffoldAsync(cwd, "showcase", tokens, overwrite: settings.Force, ct);
        }
        else if (template == InitTemplate.Library && willScaffoldCsproj)
        {
            // Component class library: a Razor-SDK csproj wired for Blazor component compilation.
            await File.WriteAllTextAsync(Path.Combine(cwd, $"{projectName}.csproj"), LibraryCsproj(projectName), ct);
            project = ProjectContext.Discover(cwd);
        }

        // A pre-existing bare class library (Microsoft.NET.Sdk) can't compile the copied components:
        // patch in the Razor SDK, the ASP.NET Core framework reference and implicit usings.
        IReadOnlyList<string> hardened = [];
        if (project.IsBareClassLibrary)
        {
            hardened = await ClassLibrarySupport.HardenCsprojAsync(project, ct);
            if (hardened.Count > 0)
                project = ProjectContext.Discover(cwd);
        }

        // Class libraries don't ship the standard Blazor _Imports a `dotnet new blazor` app has.
        if (template == InitTemplate.Library || hardened.Count > 0)
            await ClassLibrarySupport.EnsureStandardImportsAsync(cwd, ct);

        var svc = await CliServices.LoadAsync(cwd, config.Registry, ct);

        // Install the base NuGet layers (headless behavior, icons, class merger). Skipped in --json
        // mode, when no csproj exists, and when init just wrote the csproj (it already declares them).
        var packages = PackageVersions.BaseSet;
        if (project.CsprojPath is not null && !settings.Json && !willScaffoldCsproj)
        {
            // Ledger the ids this run introduces (pre-existing references are user-owned) so
            // deinit can undo exactly them. Recorded only when the install actually succeeded.
            var preExisting = PackageLedger.PreExisting(project.CsprojPath, packages.Select(p => p.Id));

            async Task InstallAsync(IProgress<string>? progress)
            {
                var install = await svc.Dotnet.AddPackagesAsync(packages, progress, ct);
                if (install.Success)
                    PackageLedger.Record(config, packages.Select(p => p.Id), preExisting);
                else
                    settings.Warn($"[yellow]Package install reported an error:[/] {Markup.Escape(install.ErrorText)}");
            }

            if (settings.Silent)
                await InstallAsync(null);
            else
                await AnsiConsole.Status().StartAsync("Installing packages...",
                    ctx => InstallAsync(new Progress<string>(msg => ctx.Status(Markup.Escape(msg)))));
        }

        await ConfigStore.SaveAsync(cwd, config, ct);

        // Wire Tailwind: write the managed CSS assets and generate/patch the input — the CLI's own
        // Styles/app.css, or (bundler mode) sync the imports inside the file `css` points at.
        var tailwind = await new TailwindSetup(assets)
            .EnsureAsync(cwd, output, skin, new TailwindOptions(settings.Pointer, rtl), preset, cssInput: config.Css, ct: ct);

        // A preset code carrying a heading/body font selection also writes the font overlay, and a
        // chart/radius selection the token overlay.
        if (codeSelection is { } cs)
        {
            if (FontStacks.Stack(cs.Heading) is not null || FontStacks.Stack(cs.Font) is not null)
                await new TailwindSetup(assets).EnsureFontsAsync(cwd, cs.Heading, cs.Font, config.Css, ct);
            if (TokenOverlays.Radius(cs.Radius) is not null || TokenOverlays.Chart(cs.Chart) is not null)
                await new TailwindSetup(assets).EnsureTokensAsync(cwd, cs.Chart, cs.Radius, config.Css, ct);
        }

        // Wire the compile pipeline (standalone/node/…). Skipped in --json mode (machine callers
        // decide) and when the user asked for 'none'.
        PipelineSetupResult? pipelineResult = null;
        if (!settings.Json && !settings.Tailwind.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            var registry = new TailwindPipelineRegistry();
            var pipeline = settings.Tailwind.Equals("auto", StringComparison.OrdinalIgnoreCase)
                ? registry.Recommend(project)
                : registry.Resolve(settings.Tailwind);

            if (pipeline is null)
                settings.Warn($"[yellow]Unknown --tailwind '{Markup.Escape(settings.Tailwind)}'; skipping pipeline setup.[/]");
            else if (pipeline.CanSetup)
                pipelineResult = await pipeline.SetupAsync(project, TailwindPipelineSupport.PathsFor(null));
            else
                settings.Line($"[grey]Detected [cyan]{pipeline.Id}[/]: add its Tailwind plugin, then import {tailwind.InputPath}. Build: {Markup.Escape(pipeline.BuildHint(project, TailwindPipelineSupport.PathsFor(null)))}[/]");
        }

        // Wire the host page (index.html / App.razor / _Host.cshtml, whichever this app has): the
        // style-<skin> class, the compiled stylesheet link, and the pre-paint boot.js. Idempotent -
        // re-runs only add what's missing. Class libraries have no host. Note dir="rtl" is never
        // set: the rtl flag means RTL *support*; page direction stays the app's decision.
        var host = template == InitTemplate.Library
            ? new HostPageResult()
            : await new HostPageSetup().EnsureAsync(cwd, skin, preset: preset, ct: ct);

        // The Showcase demo pages use this component set; otherwise honor args / an interactive pick.
        string[] showcaseComponents =
        [
            // shell
            "button", "kbd", "sheet", "command", "dialog", "theme-switcher",
            // dashboard
            "badge", "card", "alert", "separator", "tabs", "table", "avatar", "progress", "skeleton",
            // forms + auth
            "field", "label", "input-text", "input-number", "input-date", "select", "combobox",
            "checkbox", "radio-group", "switch", "slider",
            // overlays
            "alert-dialog", "popover", "tooltip", "dropdown-menu", "toast",
            // data
            "accordion", "collapsible", "tree", "carousel",
        ];
        var chosenComponents = settings.Components.Length > 0 ? settings.Components
            : scaffolded ? showcaseComponents
            : interactive && !settings.AdoptOnly
                ? await ComponentPrompts.PickAsync(svc.Registry, "Add components now? [grey](optional)[/]") : [];

        AddResult? added = null;
        if (chosenComponents.Length > 0)
        {
            // Reload services so the project context sees a freshly-scaffolded csproj.
            var addSvc = scaffolded ? await CliServices.LoadAsync(cwd, config.Registry, ct) : svc;
            var addService = new AddService(addSvc.Registry, addSvc.Project, config, addSvc.Dotnet);
            added = await addService.RunAsync(
                new AddRequest { Components = chosenComponents, NoNuget = willScaffoldCsproj }, ct: ct);
        }

        if (settings.Json)
        {
            // The full outcome, not just the config: what was scaffolded, styled and added.
            var payload = new JsonObject
            {
                ["config"] = JsonSerializer.SerializeToNode(config, CoreJson.Default.BlaizioConfig),
                ["template"] = template?.ToString().ToLowerInvariant(),
                ["scaffold"] = scaffold is null ? null : new JsonObject
                {
                    ["written"] = new JsonArray([.. scaffold.Written.Select(f => (JsonNode?)f)]),
                    ["skipped"] = new JsonArray([.. scaffold.Skipped.Select(f => (JsonNode?)f)]),
                },
                ["css"] = new JsonObject
                {
                    ["input"] = tailwind.InputPath,
                    ["created"] = tailwind.InputCreated,
                    ["skin"] = skin,
                    ["preset"] = preset,
                },
                ["host"] = host.HostPath is null ? null : new JsonObject
                {
                    ["path"] = host.HostPath,
                    ["changes"] = new JsonArray([.. host.Changes.Select(c => (JsonNode?)c)]),
                },
                ["added"] = added is null
                    ? null
                    : JsonSerializer.SerializeToNode(added, CoreJson.Default.AddResult),
                ["csprojHardened"] = new JsonArray([.. hardened.Select(h => (JsonNode?)h)]),
                // NuGet install and pipeline setup are intentionally skipped in --json mode.
                ["packagesInstalled"] = false,
                ["pipeline"] = null,
            };
            Console.Out.WriteLine(payload.ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        AnsiConsole.MarkupLine($"[green]{(topUp ? "Refreshed" : "Initialized")}[/] {BlaizioConfig.FileName} (namespace [cyan]{Markup.Escape(ns)}[/], template [cyan]{template?.ToString().ToLowerInvariant() ?? "none"}[/]).");
        if (scaffold is not null)
            AnsiConsole.MarkupLine($"  [blue]scaffold[/] {scaffold.Written.Count} file(s){(scaffold.Skipped.Count > 0 ? $", {scaffold.Skipped.Count} skipped" : "")}");
        foreach (var change in hardened)
            AnsiConsole.MarkupLine($"  [blue]csproj[/] {Markup.Escape(change)}");
        AnsiConsole.MarkupLine($"  [blue]css[/] {(tailwind.InputCreated ? "created" : "updated")} {Markup.Escape(tailwind.InputPath)} (skin [cyan]{Markup.Escape(skin)}[/], preset [cyan]{Markup.Escape(preset)}[/])");
        foreach (var change in host.Changes)
            AnsiConsole.MarkupLine($"  [blue]host[/] {Markup.Escape(host.HostPath!)}: {Markup.Escape(change)}");
        if (pipelineResult is not null)
        {
            foreach (var file in pipelineResult.ChangedFiles)
                AnsiConsole.MarkupLine($"  [blue]tw[/] {Markup.Escape(file)}");
        }
        if (template is not null and not InitTemplate.Library && !scaffolded)
            AnsiConsole.MarkupLine("[grey]Template scaffolding is not generated yet — config, packages and styling are ready.[/]");
        if (added is not null)
            AnsiConsole.MarkupLine($"[green]Added[/] {added.Items.Count} component(s).");

        // Next steps: the pipeline's build hint if one was wired, else the generic Tailwind command.
        var buildHint = pipelineResult?.BuildHint ?? "tailwindcss -i Styles/app.css -o wwwroot/app.css --watch";
        if (pipelineResult is not null)
            foreach (var note in pipelineResult.Notes)
                AnsiConsole.MarkupLine($"[grey]›[/] {Markup.Escape(note)}");

        if (scaffolded)
        {
            AnsiConsole.MarkupLine($"[grey]Next:[/] compile CSS ([white]{Markup.Escape(buildHint)}[/]) then [white]dotnet run[/].");
        }
        else if (host.HostPath is not null)
        {
            // The host page was wired automatically - only the build step is left.
            AnsiConsole.MarkupLine($"[grey]Next:[/] compile CSS with [white]{Markup.Escape(buildHint)}[/] then run the app.");
        }
        else
        {
            AnsiConsole.MarkupLine($"[grey]Next:[/] compile CSS with [white]{Markup.Escape(buildHint)}[/],");
            AnsiConsole.MarkupLine($"[grey]      add [white].style-{Markup.Escape(skin)}[/] (and optionally [white].dark[/]) to your <html>, and reference the compiled css.[/]");
        }
        // RTL support only readies the skins (logical properties); the page direction itself is
        // always the app's to set - init never stamps dir="rtl" on <html>.
        if (rtl)
            AnsiConsole.MarkupLine("[grey]      RTL: set [white]dir=\"rtl\"[/] on <html> (or wrap content in [white]<BlazeDirectionProvider Direction=\"Rtl\">[/]).[/]");
        return 0;
    }

    /// <summary>
    /// A scoped apply: re-style an existing project without touching its host, packages or
    /// components. <see cref="StyleScope.Theme"/> rewrites the skin + preset CSS tokens (and records
    /// them in <c>blaizio.json</c>); <see cref="StyleScope.Fonts"/> writes only the font overlay.
    /// </summary>
    private static async Task<int> RunScopedAsync(
        string cwd, InitSettings settings, string skin, string preset, PresetSelection? code, CancellationToken ct)
    {
        var assets = new EmbeddedCssAssets();
        var config = await ConfigStore.LoadAsync(cwd, ct);
        var output = config?.Output ?? settings.Output ?? "Components/Ui";
        var setup = new TailwindSetup(assets);

        if (settings.Scope is StyleScope.Theme)
        {
            await setup.EnsureAsync(
                cwd, output, skin, new TailwindOptions(settings.Pointer, settings.Rtl || code?.Rtl == true), preset,
                cssInput: config?.Css, ct: ct);
            // A theme apply from a /create code carries its chart/radius overlays too.
            if (code is { } c && (TokenOverlays.Radius(c.Radius) is not null || TokenOverlays.Chart(c.Chart) is not null))
                await setup.EnsureTokensAsync(cwd, c.Chart, c.Radius, config?.Css, ct);
            // The tokens activate through the style-*/preset-* classes on <html> — swap them or the
            // page keeps showing the old preset. Classes only: the host wiring is its own business.
            await new HostPageSetup().EnsureAsync(cwd, skin, preset: preset, attributesOnly: true, ct: ct);
            if (config is not null)
            {
                config.Theme = skin;
                config.Preset = preset;
                await ConfigStore.SaveAsync(cwd, config, ct);
            }

            if (!settings.Silent && !settings.Json)
                AnsiConsole.MarkupLine(
                    $"[green]Applied theme[/] (skin [cyan]{Markup.Escape(skin)}[/], preset [cyan]{Markup.Escape(preset)}[/]). Components untouched.");
            return 0;
        }

        // StyleScope.Fonts: the font overlay only.
        var heading = code?.Heading ?? "default";
        var font = code?.Font ?? "default";
        var result = await setup.EnsureFontsAsync(cwd, heading, font, config?.Css, ct);
        if (!settings.Silent && !settings.Json)
        {
            if (!result.HadSelection)
                settings.Warn("[yellow]No font selection in the preset code; nothing to apply.[/]");
            else if (!result.ImportWired)
                settings.Warn($"[yellow]Wrote {Markup.Escape(result.Path!)} but no Styles/app.css to import it — run 'blaizio init' first.[/]");
            else
                AnsiConsole.MarkupLine($"[green]Applied fonts[/] to {Markup.Escape(result.Path!)}. Theme and components untouched.");
        }

        return 0;
    }

    /// <summary>The WASM project file scaffolded for the Showcase template.</summary>
    private static string ShowcaseCsproj(string projectName) =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>{projectName}</RootNamespace>
            <AssemblyName>{projectName}</AssemblyName>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.8" />
            <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.8" PrivateAssets="all" />
            <PackageReference Include="Blaizio.Base" Version="{PackageVersions.Blaizio}" />
            <PackageReference Include="Blaizio.Icons" Version="{PackageVersions.Blaizio}" />
            <PackageReference Include="TailwindMerge.NET" Version="{PackageVersions.TailwindMerge}" />
          </ItemGroup>

        </Project>

        """;

    /// <summary>The Razor class library project file scaffolded for the Library template.</summary>
    private static string LibraryCsproj(string projectName) =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk.Razor">

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>{projectName}</RootNamespace>
            <AssemblyName>{projectName}</AssemblyName>
          </PropertyGroup>

          <ItemGroup>
            <FrameworkReference Include="Microsoft.AspNetCore.App" />
          </ItemGroup>

          <ItemGroup>
            <PackageReference Include="Blaizio.Base" Version="{PackageVersions.Blaizio}" />
            <PackageReference Include="Blaizio.Icons" Version="{PackageVersions.Blaizio}" />
            <PackageReference Include="TailwindMerge.NET" Version="{PackageVersions.TailwindMerge}" />
          </ItemGroup>

        </Project>

        """;

    /// <summary>Pick the skin: explicit <c>--style</c> (or a preset code's style), an interactive list, or the <c>ember</c> default.</summary>
    private static string ResolveSkin(string? style, InitSettings settings, bool interactive, EmbeddedCssAssets assets)
    {
        const string fallback = "ember";
        if (style is { } requested)
        {
            // Return the canonical casing — the embedded resource lookup is case-sensitive.
            var canonical = assets.AvailableSkins
                .FirstOrDefault(s => string.Equals(s, requested, StringComparison.OrdinalIgnoreCase));
            if (canonical is not null)
                return canonical;
            settings.Warn($"[yellow]Unknown skin '{Markup.Escape(requested)}'; using '{fallback}'. Available: {string.Join(", ", assets.AvailableSkins)}.[/]");
            return fallback;
        }

        if (!interactive)
            return fallback;

        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Component [green]skin[/]?")
                .PageSize(10)
                .AddChoices(assets.AvailableSkins));
    }

    /// <summary>Pick the color preset: explicit <c>--preset</c> (name or code-expanded), an interactive list, or the <c>nova</c> default.</summary>
    private static string ResolvePreset(string? preset, InitSettings settings, bool interactive, EmbeddedCssAssets assets)
    {
        const string fallback = "nova";
        if (preset is { } requested)
        {
            if (string.Equals(requested, fallback, StringComparison.OrdinalIgnoreCase))
                return fallback;
            // Return the canonical casing — the embedded resource lookup is case-sensitive.
            var canonical = assets.AvailablePresets
                .FirstOrDefault(p => string.Equals(p, requested, StringComparison.OrdinalIgnoreCase));
            if (canonical is not null)
                return canonical;
            settings.Warn($"[yellow]Unknown preset '{Markup.Escape(requested)}'; using '{fallback}'. Available: {fallback}, {string.Join(", ", assets.AvailablePresets)}.[/]");
            return fallback;
        }

        if (!interactive)
            return fallback;

        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Color [green]preset[/]?")
                .PageSize(10)
                .AddChoices([fallback, .. assets.AvailablePresets]));
    }

    private static InitTemplate PromptTemplate() => AnsiConsole.Prompt(
        new SelectionPrompt<InitTemplate>()
            .Title("Project [green]template[/]?")
            .UseConverter(t => t switch
            {
                InitTemplate.Showcase => "Showcase — full demo app (dashboard, forms, overlays, data, auth)",
                InitTemplate.WebApp => "Blazor Web App (Server / WASM / Auto)",
                InitTemplate.Wasm => "WASM standalone",
                InitTemplate.Library => "Class library (components only)",
                _ => t.ToString(),
            })
            .AddChoices(Enum.GetValues<InitTemplate>()));

}
