using System.ComponentModel;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>new</c>.</summary>
public sealed class NewSettings : ConfirmRegistrySettings
{
    /// <summary>Template to scaffold: a built-in name or a registry reference. Prompted when omitted.</summary>
    [CommandArgument(0, "[template]")]
    [Description("Template to scaffold: showcase, webapp, wasm, library, or a registry template (name, @namespace/name or URL). Prompted when omitted")]
    public string? Template { get; init; }

    /// <summary>New project name.</summary>
    [CommandOption("-n|--name <name>")]
    [Description("Project name (default: the directory name)")]
    public string? Name { get; init; }

    /// <summary>Root namespace for copied components. Exposed as <c>-ns</c> too.</summary>
    [CommandOption("--namespace <ns>")]
    [Description("Root namespace for copied components (default: <name>.Components.Ui)")]
    public string? Namespace { get; init; }

    /// <summary>Component output directory.</summary>
    [CommandOption("-o|--output <dir>")]
    [Description("Directory copied components are written to (default: Components/Ui)")]
    public string? Output { get; init; }

    /// <summary>Component skin (style-*): ash, aura, ember, flint, forge, glow, spark, wisp.</summary>
    [CommandOption("--style <name>")]
    [Description("Component style (skin): ash, aura, ember, flint, forge, glow, spark, wisp (default: ember)")]
    public string? Style { get; init; }

    /// <summary>Color preset by name or /create code.</summary>
    [CommandOption("-p|--preset <name|code>")]
    [Description("Color preset: nova (default), aurora, comet, corona, eclipse, equinox, magnetar, meteor, nebula, polaris, pulsar, quasar, solstice, umbra, vesper, zenith - or a Themes preset code (e.g. 32r)")]
    public string? Preset { get; init; }

    /// <summary>Tailwind compile pipeline to wire.</summary>
    [CommandOption("--tailwind <mode>")]
    [Description("Tailwind pipeline: auto, standalone, node, vite, rollup, postcss, none (default: auto)")]
    [DefaultValue("auto")]
    public string Tailwind { get; init; } = "auto";

    /// <summary>Wire up RTL support.</summary>
    [CommandOption("--rtl")]
    [Description("Enable RTL support")]
    public bool Rtl { get; init; }

    /// <summary>Enable pointer cursor on buttons.</summary>
    [CommandOption("--pointer")]
    [Description("Use a pointer cursor for buttons")]
    public bool Pointer { get; init; }

    /// <summary>Opt into the thin themed scrollbars.</summary>
    [CommandOption("--scrollbar")]
    [Description("Use thin themed scrollbars on component scroll areas")]
    public bool Scrollbar { get; init; }

    /// <summary>Overwrite existing scaffold files / blaizio.json.</summary>
    [CommandOption("-f|--force")]
    [Description("Overwrite existing scaffold files and blaizio.json (default: false)")]
    public bool Force { get; init; }

    /// <summary>Use defaults with no prompts (template=showcase).</summary>
    [CommandOption("-d|--defaults")]
    [Description("Use defaults without prompting: template showcase (default: false)")]
    public bool Defaults { get; init; }
}

/// <summary>
/// Scaffolds a NEW app from a template, then runs the <c>init</c> pipeline over it (config,
/// packages, tokens file, host wiring, Tailwind pipeline) and adds the template's component set.
/// The split exists so each verb means one thing: <c>new</c> = start an app, <c>init</c> = wire
/// Blaizio into the app you already have, <c>add</c> = grab components (bootstrapping if needed).
/// </summary>
public sealed class NewCommand : AsyncCommand<NewSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, NewSettings settings)
    {
        // A built-in name scaffolds from the CLI's embedded templates; anything else is a
        // registry reference to a registry:template item.
        InitTemplate? template = null;
        RegistryItem? registryTemplate = null;
        if (settings.Template is { } arg && !Enum.TryParse<InitTemplate>(arg, ignoreCase: true, out _))
        {
            registryTemplate = await FetchTemplateAsync(arg, settings);
            if (registryTemplate is null)
                return 1;
        }
        else
        {
            template = ResolveTemplate(settings);
            if (template is null)
                return 1;
        }

        // The whole build lives in InitCommand's pipeline - `new` is the scaffolding front door.
        // Template/Name have no init flags; they only arrive here, programmatically.
        var init = new InitSettings
        {
            Cwd = settings.Cwd,
            Yes = settings.Yes,
            Silent = settings.Silent,
            Json = settings.Json,
            Registry = settings.Registry,
            Template = template,
            RegistryTemplate = registryTemplate,
            Name = settings.Name,
            Namespace = settings.Namespace,
            Output = settings.Output,
            Style = settings.Style,
            Preset = settings.Preset,
            Tailwind = settings.Tailwind,
            Rtl = settings.Rtl,
            Pointer = settings.Pointer,
            Scrollbar = settings.Scrollbar,
            Force = settings.Force,
            Defaults = settings.Defaults,
        };
        return await new InitCommand().ExecuteAsync(context, init);
    }

    /// <summary>
    /// Fetch and validate a registry template. Errors return null after reporting: an item that is
    /// not <c>registry:template</c>, or one with no files, cannot scaffold anything. A template is
    /// source code that becomes the whole app, so a direct-URL fetch warns (and asks,
    /// interactively) before proceeding - the same posture as <c>add</c>'s trust gate, without a
    /// config to record the answer in yet.
    /// </summary>
    private static async Task<RegistryItem?> FetchTemplateAsync(string reference, NewSettings settings)
    {
        var isUrl = reference.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (isUrl)
        {
            settings.Warn(
                $"[yellow]Fetching a template from[/] {Markup.Escape(reference)} - a template is source code "
                + "that becomes your whole app. Inspect it first with [white]blaizio view <url>[/] if unsure.");
            if (!settings.NonInteractive && AnsiConsole.Profile.Capabilities.Interactive
                && !AnsiConsole.Confirm("Continue?", defaultValue: false))
                return null;
        }

        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, CliCancellation.Token);
        var item = await services.Registry.GetItemAsync(reference, CliCancellation.Token);
        if (item.Type != ItemType.Template)
        {
            CliOutput.Error.MarkupLine(
                $"[red]Error:[/] '{Markup.Escape(reference)}' is {Markup.Escape(item.Type.ToString().ToLowerInvariant())}, "
                + $"not a template. Install items with [white]blaizio add {Markup.Escape(reference)}[/]; "
                + "templates scaffold whole apps.");
            return null;
        }
        if (item.Files.Count == 0)
        {
            CliOutput.Error.MarkupLine(
                $"[red]Error:[/] Template '{Markup.Escape(item.Name)}' carries no files - nothing to scaffold.");
            return null;
        }
        return item;
    }

    /// <summary>Explicit argument, the showcase default under <c>-d</c>/non-interactive, or a prompt.</summary>
    private InitTemplate? ResolveTemplate(NewSettings settings)
    {
        if (settings.Template is { } arg)
        {
            if (Enum.TryParse<InitTemplate>(arg, ignoreCase: true, out var parsed))
                return parsed;
            CliOutput.Error.MarkupLine(
                $"[red]Error:[/] Unknown template '{Markup.Escape(arg)}'. Available: showcase, webapp, wasm, library.");
            return null;
        }

        if (settings.Defaults || settings.NonInteractive)
            return InitTemplate.Showcase;

        return AnsiConsole.Prompt(
            new SelectionPrompt<InitTemplate>()
                .Title("Project [green]template[/]?")
                .UseConverter(t => t switch
                {
                    InitTemplate.Showcase => "Showcase - full demo app (dashboard, forms, overlays, data, auth)",
                    InitTemplate.WebApp => "Blazor Web App (Server / WASM / Auto)",
                    InitTemplate.Wasm => "WASM standalone",
                    InitTemplate.Library => "Class library (components only)",
                    _ => t.ToString(),
                })
                .AddChoices(Enum.GetValues<InitTemplate>()));
    }
}
