using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;

namespace Blaizio.Cli.Commands;

/// <summary>
/// Turns <see cref="InitSettings"/> into an <see cref="InitPlan"/>: top-up detection, css-input
/// validation and discovery, namespace/output resolution, preset-code expansion, and the
/// skin/preset/RTL picks - every prompt the pipeline ever shows lives here, so the wiring phases
/// behind it never touch the console.
/// </summary>
internal static class InitInputs
{
    /// <summary>Resolve the plan, or return a non-zero exit code (invalid input, already reported).</summary>
    public static async Task<(int ExitCode, InitPlan? Plan)> ResolveAsync(InitSettings settings, CancellationToken ct)
    {
        var cwd = settings.ResolvedCwd;

        // An already-initialized project isn't an error: init tops up whatever is missing (packages,
        // CSS, host wiring, pipeline) around the recorded config, keeping its values (explicit flags
        // still override) and its installed map. --force starts over from scratch instead.
        var existing = settings.Force ? null : await ConfigStore.LoadAsync(cwd, ct);
        var topUp = existing is not null;
        if (topUp)
            settings.Line($"{BlaizioConfig.FileName} exists - adding missing Blaizio pieces (use [white]--force[/] to re-init).");

        // Bundler mode's input must exist before anything is written: erroring later would leave a
        // half-applied init (config saved, packages installed) with a bad path recorded in it.
        var cssInput = settings.Css ?? existing?.Css;
        if (cssInput is not null && !File.Exists(Path.Combine(cwd, cssInput)))
        {
            var origin = settings.Css is not null ? "--css" : $"blaizio.json \"css\"";
            CliOutput.Error.MarkupLine(
                $"[red]Error:[/] The css input '{Markup.Escape(cssInput)}' ({origin}) does not exist. " +
                $"Pass the path of your bundler's Tailwind input file, e.g. [white]blaizio add --css path/to/tailwind.css[/]");
            return (1, null);
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
                settings.Line($"Found Tailwind input [cyan]{Markup.Escape(found[0])}[/] - wiring the Blaizio imports into it (bundler mode; [white]--css[/] overrides).");
            }
            else if (found.Count > 1)
            {
                const string managedChoice = "Styles/app.css (CLI-managed input)";
                if (interactive)
                {
                    var pick = CliPrompts.Selection(new SelectionPrompt<string>()
                        .Title("Which Tailwind [green]input[/] should Blaizio wire into?")
                        .PageSize(10)
                        .AddChoices([.. found, managedChoice]));
                    if (pick != managedChoice)
                        cssInput = pick;
                }
                else
                {
                    settings.Warn($"[yellow]Multiple Tailwind inputs found[/] ({Markup.Escape(string.Join(", ", found))}) - pass [white]--css <path>[/] to pick one; using the CLI-managed Styles/app.css.");
                }
            }
        }

        // init wires an EXISTING app - it never scaffolds on its own. Templates only arrive
        // programmatically from `blaizio new`, which scaffolds and then runs this same pipeline.
        var template = topUp || settings.AdoptOnly ? null : settings.Template;
        var registryTemplate = topUp || settings.AdoptOnly ? null : settings.RegistryTemplate;
        if (template is null && registryTemplate is null && project.CsprojPath is null && !settings.AdoptOnly)
            settings.Line("No .csproj found here - [white]blaizio new <template>[/] scaffolds a fresh app; continuing with config + styling only.");
        var projectName = settings.Name ?? project.AssemblyName;
        var willScaffoldCsproj =
            template is InitTemplate.Showcase or InitTemplate.Library && project.CsprojPath is null;
        // A freshly scaffolded app roots at the project name, so derive the component namespace from
        // it (unless the user pinned one explicitly); an existing csproj keeps its own root namespace.
        // A registry template scaffolds into an empty dir the same way.
        var ns = (willScaffoldCsproj || (registryTemplate is not null && project.CsprojPath is null))
                && settings.Namespace is null
            ? $"{projectName}.Components.Ui"
            : NamespaceResolver.Resolve(settings.Namespace, config: existing, project);
        if (interactive && settings.Namespace is null)
            ns = CliPrompts.Text("Root [green]namespace[/]?", ns);

        var output = settings.Output ?? existing?.Output ?? "Components/Ui";
        if (interactive && settings.Output is null)
            output = CliPrompts.Text("Output [green]directory[/]?", output);

        var assets = new EmbeddedCssAssets();

        // --preset also accepts a compact code from the docs /themes page ("32r"): expand it to
        // its style/preset/rtl parts. A real preset NAME always wins the (theoretical) ambiguity;
        // in practice no preset name decodes as a code. Explicit --style still overrides the
        // code's style. The code's font selection patches into the tokens file and its
        // chart/radius selection bakes into the tokens file's :root.
        PresetSelection? codeSelection = null;
        if (settings.Preset is { } presetArg
            && !string.Equals(presetArg, "nova", StringComparison.OrdinalIgnoreCase)
            && !assets.AvailablePresets.Any(p => string.Equals(p, presetArg, StringComparison.OrdinalIgnoreCase))
            && PresetCode.TryDecode(presetArg, out var decoded))
        {
            codeSelection = decoded;
            settings.Line($"Preset code [cyan]{Markup.Escape(presetArg.Trim())}[/] → style [cyan]{decoded.Style}[/], preset [cyan]{decoded.Preset}[/]{(decoded.Rtl ? ", [cyan]RTL[/]" : "")}{(decoded.Icons == IconSetCatalog.Default ? "" : $", icons [cyan]{decoded.Icons}[/]")}.");
        }

        var styleArg = settings.Style ?? codeSelection?.Style;
        var presetName = codeSelection?.Preset ?? settings.Preset;
        var skin = styleArg is null && existing is not null
            ? existing.Style
            : ResolveSkin(styleArg, settings, interactive, assets);
        var preset = presetName is null && existing is not null
            ? existing.Preset
            : ResolvePreset(presetName, settings, interactive, assets);

        var rtl = settings.Rtl || codeSelection?.Rtl == true || existing?.Rtl == true
            || (interactive && CliPrompts.Confirm("Enable [green]RTL[/] support?", defaultValue: false));

        // Chart/radius from the code (falling back to what a top-up already recorded) bake straight
        // into theme.css's :root; recording them is what keeps re-runs (update/apply) from
        // resetting the values.
        var chart = codeSelection?.Chart is { } cc && cc != "default" ? cc : existing?.Chart ?? "default";
        var radius = codeSelection?.Radius is { } cr && cr != "default" ? cr : existing?.Radius ?? "default";

        var config = existing ?? new BlaizioConfig { Namespace = ns };
        config.Namespace = ns;
        config.Output = output;
        config.Css = cssInput;
        config.Style = skin;
        config.Preset = preset;
        config.Rtl = rtl;
        config.Chart = chart == "default" ? null : chart;
        config.Radius = radius == "default" ? null : radius;
        if (!string.IsNullOrWhiteSpace(settings.Registry))
            config.Registry = settings.Registry;

        return (0, new InitPlan
        {
            Config = config,
            TopUp = topUp,
            Template = template,
            RegistryTemplate = registryTemplate,
            ProjectName = projectName,
            Skin = skin,
            Preset = preset,
            Chart = chart,
            Radius = radius,
            Rtl = rtl,
            Pointer = settings.Pointer,
            Scrollbar = settings.Scrollbar,
            TailwindMode = settings.Tailwind,
            Force = settings.Force,
            AdoptOnly = settings.AdoptOnly,
            CodeSelection = codeSelection,
            PresetArg = settings.Preset,
            Components = settings.Components,
            Interactive = interactive,
        });
    }

    /// <summary>Pick the skin: explicit <c>--style</c> (or a preset code's style), an interactive list, or the <c>ember</c> default.</summary>
    private static string ResolveSkin(string? style, InitSettings settings, bool interactive, EmbeddedCssAssets assets)
    {
        const string fallback = "ember";
        if (style is { } requested)
        {
            // Return the canonical casing - the embedded resource lookup is case-sensitive.
            var canonical = assets.AvailableSkins
                .FirstOrDefault(s => string.Equals(s, requested, StringComparison.OrdinalIgnoreCase));
            if (canonical is not null)
                return canonical;
            settings.Warn($"[yellow]Unknown skin '{Markup.Escape(requested)}'; using '{fallback}'. Available: {string.Join(", ", assets.AvailableSkins)}.[/]");
            return fallback;
        }

        if (!interactive)
            return fallback;

        return CliPrompts.Selection(
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
            // Return the canonical casing - the embedded resource lookup is case-sensitive.
            var canonical = assets.AvailablePresets
                .FirstOrDefault(p => string.Equals(p, requested, StringComparison.OrdinalIgnoreCase));
            if (canonical is not null)
                return canonical;
            settings.Warn($"[yellow]Unknown preset '{Markup.Escape(requested)}'; using '{fallback}'. Available: {fallback}, {string.Join(", ", assets.AvailablePresets)}.[/]");
            return fallback;
        }

        if (!interactive)
            return fallback;

        return CliPrompts.Selection(
            new SelectionPrompt<string>()
                .Title("Color [green]preset[/]?")
                .PageSize(10)
                .AddChoices([fallback, .. assets.AvailablePresets]));
    }
}
