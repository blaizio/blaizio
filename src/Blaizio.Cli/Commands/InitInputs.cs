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
/// style/preset/RTL picks - every prompt the pipeline ever shows lives here, so the wiring phases
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
        // An unknown name keeps what a top-up recorded rather than falling back to ember: a typo
        // must not quietly move the project to a style nobody asked for.
        var style = styleArg is null && existing is not null
            ? existing.Style
            : ResolveStyle(styleArg, settings, interactive, assets, fallback: existing?.Style ?? "ember");
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

        // The icon set: an explicit --icons, else the code's, else what a top-up already recorded.
        // Recorded BEFORE the components install so the glyph file lands retargeted and the base
        // packages carry the set's package instead of Tabler's.
        var icons = settings.Icons ?? codeSelection?.Icons ?? existing?.Icons ?? IconSetCatalog.Default;
        if (IconSetCatalog.Find(icons) is not { } iconSet)
        {
            CliOutput.Error.MarkupLine($"[red]Error:[/] Unknown icon set '{Markup.Escape(icons)}'. Use one of: {string.Join(", ", IconSetCatalog.All.Select(s => s.Name))}.");
            return (1, null);
        }
        icons = iconSet.Name;

        // The recorded style is the one the installed components wear: they ship with the look
        // inlined, so only a full apply (re-installing every one of them) can change it. A top-up
        // asked for another style keeps the recorded one - recording it anyway would make the next
        // add mix styles and `preset resolve` encode a look the project does not have - and names
        // the apply that does switch. Only items that copied files count (a font item has none);
        // a project with nothing like that installed has no files to disagree.
        var styledCount = existing?.Installed.Values.Count(i => i.Files.Count > 0) ?? 0;
        string? styleNotApplied = null;
        if (existing is not null && styledCount > 0
            && !string.Equals(style, existing.Style, StringComparison.OrdinalIgnoreCase))
        {
            var rtlBit = codeSelection?.Rtl == true || PresetSupport.RtlForApply(existing, settings.Rtl);
            var code = TargetCode(existing, codeSelection, style, preset, rtlBit, chart, radius, icons);
            settings.Warn(StyleNotAppliedWarning(style, existing.Style, styledCount, code));
            styleNotApplied = style;
            style = existing.Style;
        }

        var config = existing ?? new BlaizioConfig { Namespace = ns };
        config.Namespace = ns;
        config.Icons = icons == IconSetCatalog.Default ? null : icons;
        config.Output = output;
        config.Css = cssInput;
        config.Style = style;
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
            Style = style,
            StyleNotApplied = styleNotApplied,
            Preset = preset,
            Chart = chart,
            Radius = radius,
            Rtl = rtl,
            Pointer = settings.Pointer,
            Scrollbar = settings.Scrollbar,
            Icons = icons,
            TailwindMode = settings.Tailwind,
            Force = settings.Force,
            AdoptOnly = settings.AdoptOnly,
            CodeSelection = codeSelection,
            PresetArg = settings.Preset,
            Components = settings.Components,
            Interactive = interactive,
        });
    }

    /// <summary>
    /// The warning a top-up prints when it keeps the recorded style over the one it was asked for:
    /// both styles, why the record stays, and the full apply (with the code for the asked-for look)
    /// that does switch. Markup, with every value escaped.
    /// </summary>
    internal static string StyleNotAppliedWarning(string requested, string recorded, int installed, string code) =>
        $"[yellow]Style {Markup.Escape(requested)} not applied:[/] the {installed} installed " +
        $"{(installed == 1 ? "component wears" : "components wear")} style [cyan]{Markup.Escape(recorded)}[/], " +
        "so blaizio.json keeps it and new components arrive in it too. " +
        $"A full [white]blaizio apply {Markup.Escape(code)}[/] switches style by re-installing the components, overwriting local edits (it asks first).";

    /// <summary>
    /// The code for the look a top-up was asked for, to hand to a full apply: a preset code's own
    /// selection (or the project's recorded look) with this run's style, palette, overlays and icon
    /// set. <paramref name="rtl"/> is the RTL bit the caller decided (see
    /// <see cref="PresetSupport.RtlForApply"/>), not the recorded flag.
    /// </summary>
    internal static string TargetCode(BlaizioConfig existing, PresetSelection? code, string style, string preset,
        bool rtl, string chart, string radius, string icons) =>
        PresetCode.Encode((code ?? PresetSupport.FromConfig(existing)) with
        {
            Style = style,
            Preset = preset,
            Rtl = rtl,
            Chart = chart,
            Radius = radius,
            Icons = icons,
        });

    /// <summary>Pick the style: explicit <c>--style</c> (or a preset code's style), an interactive list, or
    /// <paramref name="fallback"/> (<c>ember</c>, or the style a top-up recorded).</summary>
    private static string ResolveStyle(string? style, InitSettings settings, bool interactive, EmbeddedCssAssets assets, string fallback)
    {
        if (style is { } requested)
        {
            // Return the canonical casing - the embedded resource lookup is case-sensitive.
            var canonical = assets.AvailableStyles
                .FirstOrDefault(s => string.Equals(s, requested, StringComparison.OrdinalIgnoreCase));
            if (canonical is not null)
                return canonical;
            settings.Warn($"[yellow]Unknown style '{Markup.Escape(requested)}'; using '{fallback}'. Available: {string.Join(", ", assets.AvailableStyles)}.[/]");
            return fallback;
        }

        if (!interactive)
            return fallback;

        return CliPrompts.Selection(
            new SelectionPrompt<string>()
                .Title("Component [green]style[/]?")
                .PageSize(10)
                .AddChoices(assets.AvailableStyles));
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
