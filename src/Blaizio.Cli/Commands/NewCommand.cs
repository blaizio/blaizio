using System.ComponentModel;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>new</c>.</summary>
public sealed class NewSettings : ConfirmRegistrySettings
{
    /// <summary>Template to scaffold. Prompted interactively when omitted.</summary>
    [CommandArgument(0, "[template]")]
    [Description("Template to scaffold: showcase, webapp, wasm, library (prompted when omitted)")]
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
    [Description("Color preset: nova (default), aurora, comet, corona, eclipse, equinox, magnetar, meteor, nebula, polaris, pulsar, quasar, solstice, umbra, zenith - or a Themes preset code (e.g. 32r)")]
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
        var template = ResolveTemplate(settings);
        if (template is null)
            return 1;

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
            Name = settings.Name,
            Namespace = settings.Namespace,
            Output = settings.Output,
            Style = settings.Style,
            Preset = settings.Preset,
            Tailwind = settings.Tailwind,
            Rtl = settings.Rtl,
            Pointer = settings.Pointer,
            Force = settings.Force,
            Defaults = settings.Defaults,
        };
        return await new InitCommand().ExecuteAsync(context, init);
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
