using System.ComponentModel;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>apply</c>.</summary>
public sealed class ApplySettings : GlobalSettings
{
    /// <summary>Preset name (nova, comet, …) or a compact /create preset code (e.g. <c>32r</c>).</summary>
    [CommandArgument(0, "[preset]")]
    [Description("The preset to apply: a name or a /create preset code")]
    public string? Preset { get; init; }

    /// <summary>Restrict the apply to parts of the preset: <c>theme</c>, <c>fonts</c> and/or <c>tokens</c>.</summary>
    [CommandOption("--only <parts>")]
    [Description("Apply only parts of a preset: theme, fonts, tokens (comma-separated)")]
    public string? Only { get; init; }

    /// <inheritdoc />
    public override ValidationResult Validate()
    {
        foreach (var part in SelectedParts)
        {
            if (part is not ("theme" or "fonts" or "font" or "tokens"))
                return ValidationResult.Error($"Unknown --only part '{part}'. Use: theme, fonts, tokens.");
        }
        return base.Validate();
    }

    /// <summary>The normalized --only parts (empty = apply everything the preset carries).</summary>
    internal string[] SelectedParts =>
        (Only ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(p => p.ToLowerInvariant())
        .ToArray();
}

/// <summary>
/// Re-styles an existing project from a preset — the color tokens (and the style/fonts a /create
/// code carries) — without touching its host, packages or components. The styling counterpart of
/// <c>init</c>'s full wiring.
/// </summary>
public sealed class ApplyCommand : AsyncCommand<ApplySettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ApplySettings settings)
    {
        var cwd = settings.ResolvedCwd;
        var ct = CliCancellation.Token;
        var assets = new EmbeddedCssAssets();
        var config = await ConfigStore.LoadAsync(cwd, ct);

        // Resolve what to apply: a preset name, or a /create code expanding to style+preset+rtl+fonts.
        var requested = settings.Preset;
        if (string.IsNullOrWhiteSpace(requested))
        {
            if (settings.NonInteractive)
            {
                CliOutput.Error.MarkupLine("[red]Error:[/] No preset given. Run: [white]blaizio apply <preset>[/]");
                return 1;
            }
            requested = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Color [green]preset[/]?")
                    .PageSize(10)
                    .AddChoices(["nova", .. assets.AvailablePresets]));
        }

        PresetSelection? code = null;
        if (!IsPresetName(requested, assets) && PresetCode.TryDecode(requested, out var decoded))
        {
            code = decoded;
            settings.Line($"[grey]Preset code [cyan]{Markup.Escape(requested.Trim())}[/] → style [cyan]{decoded.Style}[/], preset [cyan]{decoded.Preset}[/]{(decoded.Rtl ? ", [cyan]RTL[/]" : "")}.[/]");
        }

        var preset = CanonicalPreset(code?.Preset ?? requested, assets, settings);
        var skin = code?.Style is { } style
            ? assets.AvailableSkins.FirstOrDefault(s => string.Equals(s, style, StringComparison.OrdinalIgnoreCase)) ?? config?.Theme ?? "ember"
            : config?.Theme ?? "ember";

        var parts = settings.SelectedParts;
        var applyTheme = parts.Length == 0 || parts.Contains("theme");
        var applyFonts = parts.Length == 0 || parts.Contains("fonts") || parts.Contains("font");
        var applyTokens = parts.Length == 0 || parts.Contains("tokens");

        if (!settings.NonInteractive && !AnsiConsole.Confirm(
                $"Apply preset [cyan]{Markup.Escape(preset)}[/] (skin [cyan]{Markup.Escape(skin)}[/]) to this project?"))
        {
            settings.Warn("[yellow]Apply cancelled.[/]");
            return 0;
        }

        var setup = new TailwindSetup(assets);
        var output = config?.Output ?? "Components/Ui";

        HostPageResult host = new();
        if (applyTheme)
        {
            // The pointer flag isn't recorded in config — preserve whatever options.css state exists.
            var pointer = File.Exists(Path.Combine(cwd, "Styles", "blaizio", "options.css"));
            var rtl = config?.Rtl == true || code?.Rtl == true;
            await setup.EnsureAsync(cwd, output, skin, new TailwindOptions(pointer, rtl), preset,
                cssInput: config?.Css, ct: ct);

            // The tokens activate through the style-*/preset-* classes on <html>: without this the
            // CSS is rewritten but the page keeps showing the old preset. Classes only — the host's
            // stylesheet link and boot script are already wired and its own business.
            host = await new HostPageSetup().EnsureAsync(cwd, skin, preset: preset, attributesOnly: true, ct: ct);

            if (config is not null)
            {
                config.Theme = skin;
                config.Preset = preset;
                await ConfigStore.SaveAsync(cwd, config, ct);
            }
        }

        FontOverlayResult? fonts = null;
        if (applyFonts)
        {
            var heading = code?.Heading ?? "default";
            var font = code?.Font ?? "default";
            // Never silently stomp the app's own typography: a full apply skips the preset's fonts
            // when the project defines its own (an @font-face, a --font-heading, an unmarked
            // webfont link...). An explicit --only fonts is the override.
            var explicitFonts = parts.Contains("fonts") || parts.Contains("font");
            var hasSelection = FontStacks.Stack(heading) is not null || FontStacks.Stack(font) is not null;
            if (hasSelection && !explicitFonts && FontDetection.UserDefined(cwd, config?.Css, out var fontReason))
            {
                settings.Warn(
                    $"[yellow]Skipping the preset's fonts:[/] {Markup.Escape(fontReason)}. " +
                    $"Run [white]blaizio apply {Markup.Escape(requested.Trim())} --only fonts[/] to replace your font setup.");
                applyFonts = false;
            }
            else
            {
                // A bare --only fonts run without a code has no font selection: EnsureFontsAsync
                // reports HadSelection=false and we surface that below instead of writing nothing silently.
                fonts = await TailwindSetup.EnsureFontsAsync(cwd, heading, font, config?.Css, ct);
                // Webfonts load through a host <link> (Tailwind would inline a CSS @import
                // mid-bundle, where it's ignored); a selection with no webfont removes a
                // previously wired link.
                if (fonts.Value.HadSelection)
                {
                    await new HostPageSetup().EnsureFontLinkAsync(cwd, FontCatalog.CssUrl(heading, font), ct);
                    if (config is not null)
                    {
                        // Record the pair so `add font-*` items can later replace one half.
                        config.Heading = heading == "default" ? null : heading;
                        config.Font = font == "default" ? null : font;
                        await ConfigStore.SaveAsync(cwd, config, ct);
                    }
                }
            }
        }

        FontOverlayResult? tokens = null;
        if (applyTokens)
        {
            var chart = code?.Chart ?? "default";
            var radius = code?.Radius ?? "default";
            tokens = await setup.EnsureTokensAsync(cwd, chart, radius, config?.Css, ct);
        }

        if (settings.Json)
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["preset"] = preset,
                ["skin"] = skin,
                ["theme"] = applyTheme,
                ["fonts"] = applyFonts && fonts?.HadSelection == true,
                ["tokens"] = applyTokens && tokens?.HadSelection == true,
            }.ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        if (applyTheme)
        {
            AnsiConsole.MarkupLine($"[green]Applied theme[/] (skin [cyan]{Markup.Escape(skin)}[/], preset [cyan]{Markup.Escape(preset)}[/]). Components untouched.");
            foreach (var change in host.Changes)
                AnsiConsole.MarkupLine($"  [blue]host[/] {Markup.Escape(host.HostPath!)}: {Markup.Escape(change)}");
        }
        if (applyFonts && fonts is { } f)
        {
            if (!f.HadSelection && !applyTheme)
                settings.Warn("[yellow]No font selection in the preset; nothing to apply.[/]");
            else if (f.HadSelection && !f.ImportWired)
                settings.Warn($"[yellow]Wrote {Markup.Escape(f.Path!)} but no Styles/app.css to import it — run 'blaizio init' first.[/]");
            else if (f.HadSelection)
                AnsiConsole.MarkupLine($"[green]Applied fonts[/] to {Markup.Escape(f.Path!)}.");
        }
        if (applyTokens && tokens is { } t)
        {
            if (!t.HadSelection && !applyTheme && parts.Contains("tokens"))
                settings.Warn("[yellow]No chart/radius selection in the preset; nothing to apply.[/]");
            else if (t.HadSelection && !t.ImportWired)
                settings.Warn($"[yellow]Wrote {Markup.Escape(t.Path!)} but no Styles/app.css to import it — run 'blaizio init' first.[/]");
            else if (t.HadSelection)
                AnsiConsole.MarkupLine($"[green]Applied chart/radius tokens[/] to {Markup.Escape(t.Path!)}.");
        }

        return 0;
    }

    private static bool IsPresetName(string requested, EmbeddedCssAssets assets) =>
        string.Equals(requested, "nova", StringComparison.OrdinalIgnoreCase)
        || assets.AvailablePresets.Any(p => string.Equals(p, requested, StringComparison.OrdinalIgnoreCase));

    /// <summary>Canonical preset name (embedded resource lookups are case-sensitive), warning on unknowns.</summary>
    private static string CanonicalPreset(string requested, EmbeddedCssAssets assets, ApplySettings settings)
    {
        const string fallback = "nova";
        if (string.Equals(requested, fallback, StringComparison.OrdinalIgnoreCase))
            return fallback;
        var canonical = assets.AvailablePresets
            .FirstOrDefault(p => string.Equals(p, requested, StringComparison.OrdinalIgnoreCase));
        if (canonical is not null)
            return canonical;
        settings.Warn($"[yellow]Unknown preset '{Markup.Escape(requested)}'; using '{fallback}'. Available: {fallback}, {string.Join(", ", assets.AvailablePresets)}.[/]");
        return fallback;
    }
}
