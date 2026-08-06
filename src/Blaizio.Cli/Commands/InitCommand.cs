using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Project templates <c>new</c> can scaffold. Full scaffolding lands with the registry templates.</summary>
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

/// <summary>Settings for the wiring pipeline - populated programmatically by <c>new</c> and
/// <c>add</c>. The pipeline has no CLI surface of its own (<c>init</c> is not a registered
/// command), so nothing here carries a command attribute - the flags live on
/// <see cref="NewSettings"/> and <see cref="AddSettings"/>, which forward them.</summary>
public sealed class InitSettings : GlobalSettings
{
    /// <summary>Components to add immediately after initialization.</summary>
    public string[] Components { get; init; } = [];

    /// <summary>
    /// Template to scaffold before wiring — set programmatically by <c>new</c> (never a flag:
    /// <c>init</c> means an EXISTING app; scaffolding a fresh one is <c>blaizio new</c>'s job).
    /// </summary>
    public InitTemplate? Template { get; init; }

    /// <summary>
    /// A fetched <c>registry:template</c> item to scaffold instead of a built-in template — set
    /// programmatically by <c>new</c> when its argument names a registry reference.
    /// </summary>
    public Core.Registry.RegistryItem? RegistryTemplate { get; init; }

    /// <summary>Project name for a scaffolded template — set programmatically by <c>new</c>.</summary>
    public string? Name { get; init; }

    /// <summary>Root namespace for copied components.</summary>
    public string? Namespace { get; init; }

    /// <summary>Component output directory.</summary>
    public string? Output { get; init; }

    /// <summary>Custom Tailwind input for bundler setups (recorded as blaizio.json <c>css</c>).</summary>
    public string? Css { get; init; }

    /// <summary>Overwrite an existing blaizio.json.</summary>
    public bool Force { get; init; }

    /// <summary>Use defaults with no prompts.</summary>
    public bool Defaults { get; init; }

    /// <summary>Wire up RTL support.</summary>
    public bool Rtl { get; init; }

    /// <summary>Enable pointer cursor on buttons.</summary>
    public bool Pointer { get; init; }

    /// <summary>Component skin (style-*): ash, aura, ember, flint, forge, glow, spark, wisp.</summary>
    public string? Style { get; init; }

    /// <summary>Tailwind compile pipeline to wire: auto, standalone, node, vite, rollup, postcss, none.</summary>
    public string Tailwind { get; init; } = "auto";

    /// <summary>Color preset (preset-*) by name - or a compact preset CODE from the docs /create
    /// page (e.g. <c>32r</c>), which expands to its style + preset + RTL parts.</summary>
    public string? Preset { get; init; }

    /// <summary>
    /// Set (programmatically, not a flag) when <c>add</c> adopts an uninitialized project: config +
    /// wiring only — never scaffold, never prompt for a template or components; <c>add</c> itself
    /// carries on with the component work.
    /// </summary>
    public bool AdoptOnly { get; init; }
}

/// <summary>The wiring pipeline entry point: <see cref="InitInputs"/> resolves flags + prompts
/// into a plan, <see cref="InitWiring"/> runs the phases (scaffold, packages, styling, host,
/// components), and <see cref="InitOutput"/> renders the outcome. Not registered as a command -
/// <c>new</c> runs it after scaffolding and <c>add</c> runs it as its wiring leg.</summary>
public sealed class InitCommand : AsyncCommand<InitSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, InitSettings settings)
    {
        var ct = CliCancellation.Token;

        var (exit, plan) = await InitInputs.ResolveAsync(settings, ct);
        if (plan is null)
            return exit;

        // The command owns the console: the wiring gets a runner that decides whether a status
        // spinner wraps the package install, and a delegate for the interactive component pick.
        Func<Func<IProgress<string>?, Task>, Task> installRunner = settings.Silent || settings.Json
            ? install => install(null)
            : install => AnsiConsole.Status().StartAsync("Installing packages...",
                ctx => install(new Progress<string>(msg => ctx.Status(Markup.Escape(msg)))));

        var run = await InitWiring.RunAsync(settings.ResolvedCwd, plan, installRunner,
            registry => ComponentPrompts.PickAsync(registry, "Add components now? [grey](optional)[/]", ct), ct);

        // Replay the phases' messages with the same gating live output had.
        foreach (var note in run.Notes)
        {
            if (note.Warning) settings.Warn(note.Markup);
            else settings.Line(note.Markup);
        }

        if (settings.Json)
            return InitOutput.EmitJson(plan, run);
        return settings.Silent ? 0 : InitOutput.Report(plan, run);
    }
}
