using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
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
    [CommandArgument(0, "[COMPONENTS]")]
    [Description("Components to add right after initializing.")]
    public string[] Components { get; init; } = [];

    /// <summary>Project template to scaffold.</summary>
    [CommandOption("-t|--template <TEMPLATE>")]
    [Description("Project template: showcase, webapp, wasm, library.")]
    public InitTemplate? Template { get; init; }

    /// <summary>New project name (for scaffolding templates).</summary>
    [CommandOption("-n|--name <NAME>")]
    [Description("Project name for a scaffolded template.")]
    public string? Name { get; init; }

    /// <summary>Root namespace for copied components. Exposed as <c>-ns</c> too.</summary>
    [CommandOption("--namespace <NS>")]
    [Description("Root namespace for copied components (also -ns).")]
    public string? Namespace { get; init; }

    /// <summary>Component output directory.</summary>
    [CommandOption("-o|--output <DIR>")]
    [Description("Directory copied components are written to.")]
    public string? Output { get; init; }

    /// <summary>Overwrite an existing blaizio.json.</summary>
    [CommandOption("-f|--force")]
    [Description("Overwrite an existing blaizio.json.")]
    public bool Force { get; init; }

    /// <summary>Use defaults with no prompts (template=showcase).</summary>
    [CommandOption("-d|--defaults")]
    [Description("Use defaults without prompting.")]
    public bool Defaults { get; init; }

    /// <summary>Wire up RTL support.</summary>
    [CommandOption("--rtl")]
    [Description("Enable RTL support.")]
    public bool Rtl { get; init; }

    /// <summary>Enable pointer cursor on buttons.</summary>
    [CommandOption("--pointer")]
    [Description("Use a pointer cursor for buttons.")]
    public bool Pointer { get; init; }

    /// <summary>Component skin (style-*): ash, aura, ember, flint, forge, glow, spark, wisp.</summary>
    [CommandOption("--theme <NAME>")]
    [Description("Component skin: ash, aura, ember, flint, forge, glow, spark, wisp.")]
    public string? Theme { get; init; }

    /// <summary>Tailwind compile pipeline to wire: auto, standalone, node, vite, postcss, none.</summary>
    [CommandOption("--tailwind <MODE>")]
    [Description("Tailwind pipeline: auto, standalone, node, vite, postcss, none.")]
    [DefaultValue("auto")]
    public string Tailwind { get; init; } = "auto";

    /// <summary>Placeholder: preset configuration (not yet implemented).</summary>
    [CommandOption("-p|--preset [NAME]")]
    [Description("(Coming soon) apply a preset configuration.")]
    public FlagValue<string>? Preset { get; init; }
}

/// <summary>Initializes a project: writes <c>blaizio.json</c>, installs packages, optionally adds components.</summary>
public sealed class InitCommand : AsyncCommand<InitSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, InitSettings settings)
    {
        var cwd = settings.ResolvedCwd;
        var ct = CliCancellation.Token;

        if (settings.Preset is { IsSet: true })
            settings.Warn("[yellow]--preset is not implemented yet; ignoring.[/]");

        if (ConfigStore.Exists(cwd) && !settings.Force)
        {
            settings.Warn($"[red]{BlaizioConfig.FileName} already exists.[/] Use [white]--force[/] to overwrite.");
            return 1;
        }

        var project = ProjectContext.Discover(cwd);
        var interactive = !settings.NonInteractive && !settings.Defaults;

        // Non-interactive init without an explicit -t is config-only: scaffolding a whole app into
        // an arbitrary cwd is never a silent default. -d/--defaults opts into the Showcase default.
        InitTemplate? template = settings.Template
            ?? (settings.Defaults ? InitTemplate.Showcase : interactive ? PromptTemplate() : null);
        var projectName = settings.Name ?? project.AssemblyName;
        var scaffolded = template == InitTemplate.Showcase;
        var willScaffoldCsproj = scaffolded && project.CsprojPath is null;
        // A freshly scaffolded app roots at the project name, so derive the component namespace from
        // it (unless the user pinned one explicitly); an existing csproj keeps its own root namespace.
        var ns = willScaffoldCsproj && settings.Namespace is null
            ? $"{projectName}.Components.Ui"
            : NamespaceResolver.Resolve(settings.Namespace, config: null, project);
        if (interactive && settings.Namespace is null)
            ns = AnsiConsole.Prompt(new TextPrompt<string>("Root [green]namespace[/]?").DefaultValue(ns));

        var output = settings.Output ?? "Components/Ui";
        if (interactive && settings.Output is null)
            output = AnsiConsole.Prompt(new TextPrompt<string>("Output [green]directory[/]?").DefaultValue(output));

        var assets = new EmbeddedCssAssets();
        var skin = ResolveSkin(settings, interactive, assets);
        var rtl = settings.Rtl || (interactive && AnsiConsole.Confirm("Enable [green]RTL[/] support?", defaultValue: false));

        var config = new BlaizioConfig
        {
            Namespace = ns,
            Output = output,
            Theme = skin,
            Rtl = rtl,
        };
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

        var svc = await CliServices.LoadAsync(cwd, config.Registry, ct);

        // Install the base NuGet layers (headless behavior, icons, class merger). Skipped in --json
        // mode, when no csproj exists, and when init just wrote the csproj (it already declares them).
        var packages = PackageVersions.BaseSet;
        if (project.CsprojPath is not null && !settings.Json && !willScaffoldCsproj)
        {
            async Task InstallAsync()
            {
                var install = await svc.Dotnet.AddPackagesAsync(packages, ct);
                if (!install.Success)
                    settings.Warn($"[yellow]Package install reported an error:[/] {Markup.Escape(install.ErrorText)}");
            }

            if (settings.Silent)
                await InstallAsync();
            else
                await AnsiConsole.Status().StartAsync("Installing packages...", _ => InstallAsync());
        }

        await ConfigStore.SaveAsync(cwd, config, ct);

        // Wire Tailwind: write the managed CSS assets and generate/patch Styles/app.css.
        var tailwind = await new TailwindSetup(assets)
            .EnsureAsync(cwd, output, skin, new TailwindOptions(settings.Pointer, rtl), ct);

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

        // The Showcase demo page uses this component set; otherwise honor args / an interactive pick.
        string[] showcaseComponents = ["button", "badge", "card", "alert", "separator"];
        var chosenComponents = settings.Components.Length > 0 ? settings.Components
            : scaffolded ? showcaseComponents
            : interactive ? await ComponentPrompts.PickAsync(svc.Registry, "Add components now? [grey](optional)[/]") : [];

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
                },
                ["added"] = added is null
                    ? null
                    : JsonSerializer.SerializeToNode(added, CoreJson.Default.AddResult),
                // NuGet install and pipeline setup are intentionally skipped in --json mode.
                ["packagesInstalled"] = false,
                ["pipeline"] = null,
            };
            Console.Out.WriteLine(payload.ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        AnsiConsole.MarkupLine($"[green]Initialized[/] {BlaizioConfig.FileName} (namespace [cyan]{Markup.Escape(ns)}[/], template [cyan]{template?.ToString().ToLowerInvariant() ?? "none"}[/]).");
        if (scaffold is not null)
            AnsiConsole.MarkupLine($"  [blue]scaffold[/] {scaffold.Written.Count} file(s){(scaffold.Skipped.Count > 0 ? $", {scaffold.Skipped.Count} skipped" : "")}");
        AnsiConsole.MarkupLine($"  [blue]css[/] {(tailwind.InputCreated ? "created" : "updated")} {Markup.Escape(tailwind.InputPath)} (skin [cyan]{Markup.Escape(skin)}[/])");
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
        else
        {
            AnsiConsole.MarkupLine($"[grey]Next:[/] compile CSS with [white]{Markup.Escape(buildHint)}[/],");
            AnsiConsole.MarkupLine($"[grey]      add [white].style-{Markup.Escape(skin)}[/] (and optionally [white].dark[/]) to your <html>, and reference the compiled css.[/]");
        }
        if (rtl)
            AnsiConsole.MarkupLine("[grey]      RTL: set [white]dir=\"rtl\"[/] on <html> (or wrap content in [white]<BlazeDirectionProvider Direction=\"Rtl\">[/]).[/]");
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

    /// <summary>Pick the skin: explicit <c>--theme</c>, an interactive list, or the <c>ember</c> default.</summary>
    private static string ResolveSkin(InitSettings settings, bool interactive, EmbeddedCssAssets assets)
    {
        const string fallback = "ember";
        if (settings.Theme is { } requested)
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
