using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Styling;
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

        if (settings.Preset is { IsSet: true })
            AnsiConsole.MarkupLine("[yellow]--preset is not implemented yet; ignoring.[/]");

        if (ConfigStore.Exists(cwd) && !settings.Force)
        {
            AnsiConsole.MarkupLine($"[red]{BlaizioConfig.FileName} already exists.[/] Use [white]--force[/] to overwrite.");
            return 1;
        }

        var project = ProjectContext.Discover(cwd);
        var interactive = !settings.NonInteractive && !settings.Defaults;

        var template = settings.Template ?? (interactive ? PromptTemplate() : InitTemplate.Showcase);
        var ns = NamespaceResolver.Resolve(settings.Namespace, config: null, project);
        if (interactive && settings.Namespace is null)
            ns = AnsiConsole.Prompt(new TextPrompt<string>("Root [green]namespace[/]?").DefaultValue(ns));

        var output = settings.Output ?? "Components/Ui";
        if (interactive && settings.Output is null)
            output = AnsiConsole.Prompt(new TextPrompt<string>("Output [green]directory[/]?").DefaultValue(output));

        var assets = new EmbeddedCssAssets();
        var skin = ResolveSkin(settings.Theme, interactive, assets);
        var rtl = settings.Rtl || (interactive && AnsiConsole.Confirm("Enable [green]RTL[/] support?", defaultValue: false));

        var config = new BlaizioConfig
        {
            Namespace = ns,
            Output = output,
            Theme = skin,
            Rtl = rtl,
        };
        config.Aliases["ui"] = ns;

        var svc = await CliServices.LoadAsync(cwd, config.Registry);

        // Install the base NuGet layers (headless behavior, icons, class merger).
        // Skipped in --json mode (machine callers drive installs themselves) and when no csproj exists.
        string[] packages = ["Blaizio.Base", "Blaizio.Icons", "TailwindMerge.NET"];
        if (project.CsprojPath is not null && !settings.Json)
        {
            await AnsiConsole.Status().StartAsync("Installing packages...", async _ =>
            {
                var install = await svc.Dotnet.AddPackagesAsync(packages);
                if (!install.Success)
                    AnsiConsole.MarkupLine($"[yellow]Package install reported an error:[/] {Markup.Escape(install.StdErr.Trim())}");
            });
        }

        await ConfigStore.SaveAsync(cwd, config);

        // Wire Tailwind: write the managed CSS assets and generate/patch Styles/app.css.
        var tailwind = await new TailwindSetup(assets).EnsureAsync(cwd, output, skin);

        var chosenComponents = settings.Components.Length > 0
            ? settings.Components
            : interactive ? await PromptComponentsAsync(svc) : [];

        AddResult? added = null;
        if (chosenComponents.Length > 0)
        {
            var addService = new AddService(svc.Registry, svc.Project, config, svc.Dotnet);
            added = await addService.RunAsync(new AddRequest { Components = chosenComponents });
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(config, CoreJson.Default.BlaizioConfig));
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]Initialized[/] {BlaizioConfig.FileName} (namespace [cyan]{Markup.Escape(ns)}[/], template [cyan]{template.ToString().ToLowerInvariant()}[/]).");
        AnsiConsole.MarkupLine($"  [blue]css[/] {(tailwind.InputCreated ? "created" : "updated")} {Markup.Escape(tailwind.InputPath)} (skin [cyan]{Markup.Escape(skin)}[/])");
        if (template != InitTemplate.Library)
            AnsiConsole.MarkupLine("[grey]Template scaffolding is not generated yet — config, packages and styling are ready.[/]");
        if (added is not null)
            AnsiConsole.MarkupLine($"[green]Added[/] {added.Items.Count} component(s).");

        AnsiConsole.MarkupLine("[grey]Next:[/] compile CSS with [white]tailwindcss -i Styles/app.css -o wwwroot/app.css --watch[/],");
        AnsiConsole.MarkupLine($"[grey]      add [white].style-{Markup.Escape(skin)}[/] (and optionally [white].dark[/]) to your <html>, and reference the compiled css.[/]");
        return 0;
    }

    /// <summary>Pick the skin: explicit <c>--theme</c>, an interactive list, or the <c>ember</c> default.</summary>
    private static string ResolveSkin(string? requested, bool interactive, EmbeddedCssAssets assets)
    {
        const string fallback = "ember";
        if (requested is not null)
        {
            if (assets.AvailableSkins.Contains(requested, StringComparer.OrdinalIgnoreCase))
                return requested;
            AnsiConsole.MarkupLine($"[yellow]Unknown skin '{Markup.Escape(requested)}'; using '{fallback}'. Available: {string.Join(", ", assets.AvailableSkins)}.[/]");
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

    private static async Task<string[]> PromptComponentsAsync(CliServices svc)
    {
        var index = await svc.Registry.GetIndexAsync();
        if (index.Items.Count == 0)
            return [];

        var picked = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Add components now? [grey](optional)[/]")
                .NotRequired()
                .PageSize(15)
                .InstructionsText("[grey](space to toggle, enter to confirm)[/]")
                .AddChoices(index.Items.Select(i => i.Name)));
        return [.. picked];
    }
}
