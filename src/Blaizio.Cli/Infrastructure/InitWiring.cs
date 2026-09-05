using Blaizio.Cli.Commands;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Core.Styling.Pipelines;
using Blaizio.Cli.Core.Templates;

namespace Blaizio.Cli.Infrastructure;

/// <summary>Everything the init pipeline resolved before wiring: the (possibly pre-existing)
/// config already carrying the chosen values, plus the decisions the wiring phases branch on.
/// Built by <see cref="Commands.InitInputs"/>; consumed by <see cref="InitWiring"/>.</summary>
internal sealed record InitPlan
{
    public required BlaizioConfig Config { get; init; }
    public required bool TopUp { get; init; }
    public required InitTemplate? Template { get; init; }
    /// <summary>A fetched <c>registry:template</c> item to scaffold, from <c>blaizio new &lt;ref&gt;</c>.</summary>
    public RegistryItem? RegistryTemplate { get; init; }
    public required string ProjectName { get; init; }
    public required string Skin { get; init; }
    public required string Preset { get; init; }
    public required string Chart { get; init; }
    public required string Radius { get; init; }
    public required bool Rtl { get; init; }
    public required bool Pointer { get; init; }
    public required bool Scrollbar { get; init; }
    public required string TailwindMode { get; init; }
    public required bool Force { get; init; }
    public required bool AdoptOnly { get; init; }
    public PresetSelection? CodeSelection { get; init; }
    /// <summary>The raw --preset argument, kept only for the fonts-skipped hint.</summary>
    public string? PresetArg { get; init; }
    public required string[] Components { get; init; }
    public required bool Interactive { get; init; }

    public bool Scaffolded => Template == InitTemplate.Showcase;

    public bool WillScaffoldCsproj(ProjectContext project) =>
        Template is InitTemplate.Showcase or InitTemplate.Library && project.CsprojPath is null;
}

/// <summary>A message a wiring phase produced, replayed by the command after the run.</summary>
internal sealed record InitNote(bool Warning, string Markup);

/// <summary>What the wiring did - everything the JSON payload and the human report render.</summary>
internal sealed class InitWiringResult
{
    public ScaffoldResult? Scaffold { get; set; }
    public IReadOnlyList<string> Hardened { get; set; } = [];
    public bool PackagesInstalled { get; set; }
    public TailwindResult Tailwind { get; set; } = default!;
    public PipelineSetupResult? Pipeline { get; set; }
    public HostPageResult Host { get; set; } = new();
    public ServicesResult Services { get; set; } = new();
    public AddResult? Added { get; set; }
    public List<InitNote> Notes { get; } = [];
}

/// <summary>
/// The init pipeline's wiring phases, in order: scaffold (template csproj + files), class-library
/// hardening, base package install (+ ledger), config save, Tailwind tokens + preset fonts,
/// compile-pipeline setup, host-page wiring, and the optional component add. No terminal I/O:
/// phase messages land in <see cref="InitWiringResult.Notes"/>, package progress flows through
/// the caller's <see cref="IProgress{T}"/>, and the interactive component pick comes in as a
/// delegate - the command stays the only place that talks to the console.
/// </summary>
internal static class InitWiring
{
    public static async Task<InitWiringResult> RunAsync(
        string cwd,
        InitPlan plan,
        Func<Func<IProgress<string>?, Task>, Task> installRunner,
        Func<IRegistryClient, Task<string[]>>? pickComponents,
        CancellationToken ct)
    {
        var result = new InitWiringResult();
        var config = plan.Config;
        var project = ProjectContext.Discover(cwd);
        var willScaffoldCsproj = plan.WillScaffoldCsproj(project);
        var assets = new EmbeddedCssAssets();

        // Templates that ship a full app (Showcase) scaffold their project - writing a WASM csproj
        // with the package references when none exists, then the host/layout/page files.
        if (plan.Scaffolded)
        {
            if (willScaffoldCsproj)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(cwd, $"{plan.ProjectName}.csproj"), ProjectTemplates.ShowcaseCsproj(plan.ProjectName), ct);
                project = ProjectContext.Discover(cwd);
            }

            var tokens = new TemplateTokens(
                willScaffoldCsproj ? plan.ProjectName : project.RootNamespace, config.Namespace, plan.ProjectName, plan.Skin);
            result.Scaffold = await new TemplateScaffolder(new EmbeddedTemplates())
                .ScaffoldAsync(cwd, "showcase", tokens, overwrite: plan.Force, ct);
        }
        else if (plan.Template == InitTemplate.Library && willScaffoldCsproj)
        {
            // Component class library: a Razor-SDK csproj wired for Blazor component compilation.
            await File.WriteAllTextAsync(
                Path.Combine(cwd, $"{plan.ProjectName}.csproj"), ProjectTemplates.LibraryCsproj(plan.ProjectName), ct);
            project = ProjectContext.Discover(cwd);
        }
        else if (plan.RegistryTemplate is { } registryTemplate)
        {
            // A registry-hosted template scaffolds through the same engine as the built-ins: its
            // files ARE the project layout (csproj included, when it ships one), with the same
            // {{Token}} substitution. The base package install below still runs - `dotnet add
            // package` is idempotent, so a template whose csproj already declares the Blaizio
            // packages loses nothing, and one that doesn't still ends up wired.
            var tokens = new TemplateTokens(
                project.CsprojPath is null ? plan.ProjectName : project.RootNamespace,
                config.Namespace, plan.ProjectName, plan.Skin);
            result.Scaffold = await new TemplateScaffolder(new RegistryItemTemplates(registryTemplate))
                .ScaffoldAsync(cwd, registryTemplate.Name, tokens, overwrite: plan.Force, ct);
            project = ProjectContext.Discover(cwd);
        }

        // A pre-existing bare class library (Microsoft.NET.Sdk) can't compile the copied components:
        // patch in the Razor SDK, the ASP.NET Core framework reference and implicit usings.
        if (project.IsBareClassLibrary)
        {
            result.Hardened = await ClassLibrarySupport.HardenCsprojAsync(project, ct);
            if (result.Hardened.Count > 0)
                project = ProjectContext.Discover(cwd);
        }

        // Class libraries don't ship the standard Blazor _Imports a `dotnet new blazor` app has.
        if (plan.Template == InitTemplate.Library || result.Hardened.Count > 0)
            await ClassLibrarySupport.EnsureStandardImportsAsync(cwd, ct);

        var svc = await CliServices.LoadAsync(cwd, config.Registry, ct, styleOverride: plan.Skin);

        // Install the base NuGet layers (headless behavior, icons, class merger). Skipped when no
        // csproj exists and when init just wrote the csproj (it already declares them).
        if (project.CsprojPath is not null && !willScaffoldCsproj)
        {
            // Ledger the ids this run introduces (pre-existing references are user-owned) so
            // uninstall can undo exactly them. Recorded only when the install actually succeeded.
            // The runner is the command's: it decides whether a status spinner wraps the install.
            var packages = PackageVersions.BaseSet;
            var preExisting = PackageLedger.PreExisting(project.CsprojPath, packages.Select(p => p.Id));
            await installRunner(async progress =>
            {
                var install = await svc.Dotnet.AddPackagesAsync(packages, progress, ct);
                if (install.Success)
                {
                    PackageLedger.Record(config, packages.Select(p => p.Id), preExisting);
                    result.PackagesInstalled = true;
                }
                else
                {
                    result.Notes.Add(new(true, $"[yellow]Package install reported an error:[/] {Spectre.Console.Markup.Escape(install.ErrorText)}"));
                }
            });
        }

        await ConfigStore.SaveAsync(cwd, config, ct);

        // Wire Tailwind: create the tokens file (imports + theme values, preset merged) or keep an
        // existing input's Blaizio imports in sync - the CLI's own Styles/app.css, or (bundler
        // mode) the file `css` points at. The contract sheets themselves materialize into
        // .blaizio/ when the project builds (Blaizio.Base targets).
        result.Tailwind = await new TailwindSetup(assets)
            .EnsureAsync(cwd, config.Output, new TailwindOptions(plan.Pointer, plan.Rtl), plan.Preset,
                cssInput: config.Css, chart: plan.Chart, radius: plan.Radius, ejected: config.Ejected, ct: ct);
        if (result.Tailwind.LegacyV1)
            result.Notes.Add(new(true, "[yellow]This project uses the old Styles/blaizio/ CSS layout.[/] Run [white]blaizio update[/] to migrate; styling was left untouched."));
        if (result.Tailwind.InputCreated)
        {
            config.CssCreated = true;
            await ConfigStore.SaveAsync(cwd, config, ct);
        }

        // The thin-scrollbar opt-in appends its scrollbar-thin redefinition to the tokens file,
        // lighting up the marks the components already carry. Idempotent (a file that has the
        // block keeps the user's version); skipped on v1 - `update` migrates the layout first.
        if (plan.Scrollbar && !result.Tailwind.LegacyV1)
            await TailwindSetup.EnsureScrollbarAsync(cwd, config.Css, ct: ct);

        // A preset code carrying a heading/body font selection also writes the font overlay (and
        // wires the Google Fonts link into the host page when the selection needs a webfont).
        // A project that defines its own fonts keeps them - the preset's selection is skipped
        // with a pointer to the explicit override.
        if (plan.CodeSelection is { } cs
            && (FontStacks.Stack(cs.Heading) is not null || FontStacks.Stack(cs.Font) is not null))
        {
            if (FontDetection.UserDefined(cwd, config.Css, out var fontReason))
            {
                result.Notes.Add(new(true,
                    $"[yellow]Skipping the preset's fonts:[/] {Spectre.Console.Markup.Escape(fontReason)}. " +
                    $"Run [white]blaizio apply {Spectre.Console.Markup.Escape(plan.PresetArg ?? "")} --only fonts[/] to replace your font setup."));
            }
            else
            {
                await TailwindSetup.EnsureFontsAsync(cwd, cs.Heading, cs.Font, config.Css, ct: ct);
                await new HostPageSetup().EnsureFontLinkAsync(cwd, FontCatalog.CssUrl(cs.Heading, cs.Font), ct);
                // Record the pair so `add font-*` items can later replace one half.
                config.Heading = cs.Heading == "default" ? null : cs.Heading;
                config.Font = cs.Font == "default" ? null : cs.Font;
                await ConfigStore.SaveAsync(cwd, config, ct);
            }
        }

        // A preset code naming an icon set other than Tabler installs that set's package (Tabler
        // stays - the styled components draw from it) and records the choice for `preset current`.
        if (plan.CodeSelection is { Icons: not IconSetCatalog.Default } sel
            && IconSetCatalog.Find(sel.Icons) is { } iconSet && project.CsprojPath is not null)
        {
            var ids = new[] { iconSet.Package };
            var pre = PackageLedger.PreExisting(project.CsprojPath, ids);
            var install = await svc.Dotnet.AddPackagesAsync([(iconSet.Package, PackageVersions.Blaizio)], null, ct);
            if (install.Success)
            {
                PackageLedger.Record(config, ids, pre);
                config.Icons = sel.Icons;
                await ConfigStore.SaveAsync(cwd, config, ct);
            }
            else
            {
                result.Notes.Add(new(true, $"[yellow]Icon set install reported an error:[/] {Spectre.Console.Markup.Escape(install.ErrorText)}"));
            }
        }

        // Wire the compile pipeline (standalone/node/…). Skipped only when the user asked for
        // 'none' - --json is output format only, never a behavior switch.
        if (!plan.TailwindMode.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            var registry = new TailwindPipelineRegistry();
            var pipeline = plan.TailwindMode.Equals("auto", StringComparison.OrdinalIgnoreCase)
                ? registry.Recommend(project)
                : registry.Resolve(plan.TailwindMode);

            if (pipeline is null)
                result.Notes.Add(new(true, $"[yellow]Unknown --tailwind '{Spectre.Console.Markup.Escape(plan.TailwindMode)}'; skipping pipeline setup.[/]"));
            else if (pipeline.CanSetup)
            {
                result.Pipeline = await pipeline.SetupAsync(project, TailwindPipelineSupport.PathsFor(null, config.Css));

                // A pipeline that compiles outside MSBuild (node, vite, postcss) writes into a
                // wwwroot MAUI has already globbed by the time dotnet build starts - so the CSS has
                // to exist before the build, not during it. The standalone target handles its own
                // registration; nothing to say there.
                if (project.IsMaui && !pipeline.Id.Equals("standalone", StringComparison.OrdinalIgnoreCase))
                    result.Notes.Add(new(false,
                        "MAUI packages [white]wwwroot[/] as it starts building: run the CSS build BEFORE [white]dotnet build[/], or the first app package ships without the stylesheet."));
            }
            else
                result.Notes.Add(new(false, $"Detected [cyan]{pipeline.Id}[/]: add its Tailwind plugin, then import {result.Tailwind.InputPath}. Build: {Spectre.Console.Markup.Escape(pipeline.BuildHint(project, TailwindPipelineSupport.PathsFor(null, config.Css)))}"));
        }

        // Wire the host page (index.html / App.razor / _Host.cshtml, whichever this app has): the
        // compiled stylesheet link and the pre-paint boot.js - no style-*/preset-* class in v3.
        // Idempotent - re-runs only add what's missing. Class libraries have no host. Note
        // dir="rtl" is never set: the rtl flag means RTL *support*; page direction stays the
        // app's decision.
        result.Host = plan.Template == InitTemplate.Library
            ? new HostPageResult()
            : await new HostPageSetup().EnsureAsync(cwd, ct: ct);

        // Register the services in Program.cs. Nothing else on the install path does, and a
        // project without the call runs until the first component that injects ICore or the
        // dialog service renders. Idempotent; a registration the app wrote itself counts.
        result.Services = plan.Template == InitTemplate.Library
            ? new ServicesResult()
            : await new ServicesSetup().EnsureAsync(cwd, ct);
        if (result.Services.Path is not null && !result.Services.Registered)
            result.Notes.Add(new(true,
                $"[yellow]{result.Services.Path}:[/] could not place the service registration. Add [white]builder.Services.AddBlaizio();[/] before the host is built - components that take ICore or the dialog and toast services fail without it."));

        // A registry template's own NuGet dependencies (beyond what its scaffolded csproj already
        // declares): installed like an item's, dev ones marked private. Not ledgered - like the
        // showcase csproj's packages, they are the new app's foundation, not an undoable add.
        if (plan.RegistryTemplate is { } tplDeps && project.CsprojPath is not null
            && (tplDeps.NugetDependencies.Count > 0 || tplDeps.DevDependencies is { Count: > 0 }))
        {
            var runtime = tplDeps.NugetDependencies.Select(NugetDependency.Parse).ToList();
            var dev = (tplDeps.DevDependencies ?? []).Select(NugetDependency.Parse).ToList();
            var install = await svc.Dotnet.AddPackagesAsync(
                runtime.Concat(dev).Select(d => (d.Id, d.Version)), null, ct);
            if (!install.Success)
                result.Notes.Add(new(true,
                    $"Template package install failed: {Spectre.Console.Markup.Escape(install.ErrorText)}"));
            else
                svc.Dotnet.MarkPrivateAssets(dev.Select(d => d.Id));
        }

        // The Showcase demo pages use this component set - a registry template names its own via
        // registryDependencies; otherwise honor args / an interactive pick.
        var chosenComponents = plan.Components.Length > 0 ? plan.Components
            : plan.Scaffolded ? ProjectTemplates.ShowcaseComponents
            : plan.RegistryTemplate is { RegistryDependencies.Count: > 0 } tpl ? [.. tpl.RegistryDependencies]
            : plan.Interactive && !plan.AdoptOnly && pickComponents is not null
                ? await pickComponents(svc.Registry) : [];

        if (chosenComponents.Length > 0)
        {
            // Reload services so the project context sees a freshly-scaffolded csproj.
            var addSvc = plan.Scaffolded || plan.RegistryTemplate is not null
                ? await CliServices.LoadAsync(cwd, config.Registry, ct, styleOverride: plan.Skin)
                : svc;
            var addService = new AddService(addSvc.Registry, addSvc.Project, config, addSvc.Dotnet);
            result.Added = await addService.RunAsync(
                new AddRequest { Components = chosenComponents, NoNuget = willScaffoldCsproj }, ct: ct);
        }

        return result;
    }
}
