using System.ComponentModel;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>apply</c>.</summary>
public sealed class ApplySettings : ConfirmRegistrySettings
{
    /// <summary>Resolve and report only; re-install nothing, patch nothing.</summary>
    [CommandOption("--dry-run")]
    [Description("Report what would change without writing or installing (default: false)")]
    public bool DryRun { get; init; }

    /// <summary>Preset name (nova, comet, …) or a compact /create preset code (e.g. <c>32r</c>).</summary>
    [CommandArgument(0, "[preset]")]
    [Description("The preset to apply: a name or a Themes preset code (from blaiz.io/themes)")]
    public string? Preset { get; init; }

    /// <summary>Restrict the apply to parts of the preset: <c>theme</c>, <c>fonts</c>, <c>tokens</c> and/or <c>icons</c>.</summary>
    [CommandOption("--only <parts>")]
    [Description("Apply only parts of a preset: theme, fonts, tokens, icons (comma-separated)")]
    public string? Only { get; init; }

    /// <summary>Also wire the pointer cursor on buttons into the tokens file.</summary>
    [CommandOption("--pointer")]
    [Description("Use a pointer cursor for buttons")]
    public bool Pointer { get; init; }

    /// <summary>Also wire the thin themed scrollbars into the tokens file.</summary>
    [CommandOption("--scrollbar")]
    [Description("Use thin themed scrollbars on component scroll areas")]
    public bool Scrollbar { get; init; }

    /// <inheritdoc />
    public override ValidationResult Validate()
    {
        foreach (var part in SelectedParts)
        {
            if (part is not ("theme" or "fonts" or "font" or "tokens" or "icons"))
                return ValidationResult.Error($"Unknown --only part '{part}'. Use: theme, fonts, tokens, icons.");
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
/// code carries). The scoped legs (<c>--only theme/fonts/tokens</c>) patch values in the tokens
/// file and touch nothing else. A FULL apply is the v3 skin swap and is destructive by design:
/// the look ships inlined in the component files, so it re-installs every ledgered component
/// from the target skin's registry variants (overwriting local edits — commit or stash first),
/// then patches the tokens.
/// </summary>
public sealed class ApplyCommand : ProjectCommand<ApplySettings>
{
    /// <inheritdoc />
    protected override async Task<int> ExecuteInProjectAsync(CommandContext context, ApplySettings settings, CancellationToken cancellationToken)
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
            requested = CliPrompts.Selection(
                new SelectionPrompt<string>()
                    .Title("Color [green]preset[/]?")
                    .PageSize(10)
                    .AddChoices(["nova", .. assets.AvailablePresets]));
        }

        PresetSelection? code = null;
        if (!IsPresetName(requested, assets) && PresetCode.TryDecode(requested, out var decoded))
        {
            code = decoded;
            settings.Line($"Preset code [cyan]{Markup.Escape(requested.Trim())}[/] → style [cyan]{decoded.Style}[/], preset [cyan]{decoded.Preset}[/]{(decoded.Rtl ? ", [cyan]RTL[/]" : "")}.");
        }

        var preset = CanonicalPreset(code?.Preset ?? requested, assets, settings);
        var skin = code?.Style is { } style
            ? assets.AvailableSkins.FirstOrDefault(s => string.Equals(s, style, StringComparison.OrdinalIgnoreCase)) ?? config?.Style ?? "ember"
            : config?.Style ?? "ember";

        var parts = settings.SelectedParts;
        var full = parts.Length == 0;
        var applyTheme = full || parts.Contains("theme");
        var applyFonts = full || parts.Contains("fonts") || parts.Contains("font");
        var applyTokens = full || parts.Contains("tokens");
        var applyIcons = full || parts.Contains("icons");

        // A full apply re-installs every ledgered component from the target skin's registry
        // variants — the only way a skin materializes in v3, and destructive to local edits.
        var reinstall = full && config is not null && config.Installed.Count > 0;

        // A dry run writes nothing, so there is nothing to consent to.
        if (!settings.DryRun && !settings.NonInteractive)
        {
            var prompt = reinstall
                ? $"Apply preset [cyan]{Markup.Escape(preset)}[/] (skin [cyan]{Markup.Escape(skin)}[/])? " +
                  $"[yellow]This re-installs {config!.Installed.Count} component(s), overwriting local edits - commit or stash first.[/]"
                : $"Apply preset [cyan]{Markup.Escape(preset)}[/] (skin [cyan]{Markup.Escape(skin)}[/]) to this project?";
            if (!CliPrompts.Confirm(prompt))
            {
                settings.Warn("[yellow]Apply cancelled.[/]");
                return 0;
            }
        }

        var setup = new TailwindSetup(assets);

        // The effective chart/radius: the code's selection when tokens are being applied, else
        // whatever the project already recorded — a theme patch must never lose a baked overlay.
        var newChart = applyTokens && code?.Chart is { } cc && cc != "default" ? cc : null;
        var newRadius = applyTokens && code?.Radius is { } cr && cr != "default" ? cr : null;
        var chart = newChart ?? config?.Chart ?? "default";
        var radius = newRadius ?? config?.Radius ?? "default";

        // The destructive leg first: fetch the ledgered components' variants for the target skin
        // and overwrite the local copies. Tokens are patched after, so an interrupted run leaves
        // consistent components with stale colors, not the reverse.
        if (reinstall)
        {
            var services = await CliServices.LoadAsync(cwd, settings.Registry, ct, styleOverride: skin);
            var addService = new AddService(services.Registry, services.Project, config!, services.Dotnet);
            var reinstallComponents = config!.Installed.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();
            // A code carrying RTL owes the direction cascade component, same as `add --rtl`: the
            // skins mirror via logical properties on their own, but a layout flips direction
            // through BzDirectionProvider. Registries that don't ship it are left alone.
            if (code?.Rtl == true && !config.Installed.ContainsKey("direction-provider"))
            {
                var index = await services.Registry.GetIndexAsync();
                if (index.Items.Any(i => string.Equals(i.Name, "direction-provider", StringComparison.OrdinalIgnoreCase)))
                {
                    reinstallComponents.Add("direction-provider");
                    settings.Line("  [blue]rtl[/] adding [cyan]direction-provider[/] - the direction cascade RTL layouts flip with");
                }
            }
            var request = new AddRequest
            {
                Components = [.. reinstallComponents],
                Overwrite = true,
                // A skin swap is all-or-nothing: keeping an edited component would leave it wearing
                // the OLD skin next to everything else on the new one. The confirm above is the
                // consent for exactly that (and -y accepts it), so no second per-component picker.
                Force = true,
                DryRun = settings.DryRun,
            };
            if (settings.Silent || settings.Json)
            {
                await addService.RunAsync(request, ct: ct);
            }
            else
            {
                await AnsiConsole.Status().StartAsync("Re-installing components...", async ctx =>
                    await addService.RunAsync(request,
                        new Progress<string>(msg => ctx.Status(Markup.Escape(msg))), ct));
            }
        }

        TokenPatchResult? theme = null;
        if (applyTheme)
        {
            theme = await setup.ApplyPresetAsync(cwd, preset, config?.Css, chart, radius, settings.DryRun, ct);
            if (theme.Value.Patched && config is not null && !settings.DryRun)
            {
                config.Style = skin;
                config.Preset = preset;
                config.Chart = chart == "default" ? null : chart;
                config.Radius = radius == "default" ? null : radius;
                await ConfigStore.SaveAsync(cwd, config, ct);
            }

            // A v3 code carries directly-edited tokens: patch them (and their derived partners)
            // over the preset values, the same declaration-by-declaration write theme items use.
            if (code is { Overrides.Count: > 0 } && !settings.DryRun)
            {
                var edits = await TailwindSetup.EnsureCssVarsAsync(
                    cwd, ThemeTokens.ToCssVars(code.Overrides), config?.Css, ct);
                if (edits.Patched)
                    settings.Line($"Patched [cyan]{code.Overrides.Count}[/] edited token(s) over the preset.");
            }
        }

        TokenPatchResult? fonts = null;
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
                fonts = await TailwindSetup.EnsureFontsAsync(cwd, heading, font, config?.Css, settings.DryRun, ct);
                // Webfonts load through a host <link> (Tailwind would inline a CSS @import
                // mid-bundle, where it's ignored); a selection with no webfont removes a
                // previously wired link.
                if (fonts.Value is { HadSelection: true, Patched: true } && !settings.DryRun)
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

        // The preset's icon set: a package install on top of Tabler (the styled components draw
        // from it), recorded in blaizio.json so `preset current` round-trips it.
        var iconsInstalled = false;
        if (applyIcons && code?.Icons is { } iconSetName && iconSetName != IconSetCatalog.Default
            && IconSetCatalog.Find(iconSetName) is { } iconSet && !settings.DryRun)
        {
            var install = await new DotnetCli(cwd).AddPackagesAsync([(iconSet.Package, PackageVersions.Blaizio)], null, ct);
            iconsInstalled = install.Success;
            if (!install.Success)
                settings.Warn($"[yellow]Icon set install reported an error:[/] {Markup.Escape(install.ErrorText)}");
            else if (config is not null)
            {
                config.Icons = iconSetName;
                await ConfigStore.SaveAsync(cwd, config, ct);
            }
        }

        TokenPatchResult? tokens = null;
        if (applyTokens)
        {
            if (applyTheme && theme is not null)
            {
                // The theme patch above already re-applied the selection with the preset values.
                tokens = new TokenPatchResult(
                    newChart is not null || newRadius is not null, theme.Value.Patched, theme.Value.Path);
            }
            else
            {
                // Tokens alone: patch only the chart/radius declarations in the tokens file.
                tokens = await TailwindSetup.EnsureThemeTokensAsync(cwd, chart, radius, config?.Css, settings.DryRun, ct);
                if (tokens.Value is { HadSelection: true, Patched: true } && config is not null && !settings.DryRun)
                {
                    config.Chart = chart == "default" ? null : chart;
                    config.Radius = radius == "default" ? null : radius;
                    await ConfigStore.SaveAsync(cwd, config, ct);
                }
            }
        }

        // The wiring toggles ride along like on new/add: pointer is a base-layer rule, the thin
        // scrollbar is the scrollbar-thin redefinition - both surgical, both idempotent.
        TokenPatchResult? pointer = null;
        TokenPatchResult? scrollbar = null;
        if (settings.Pointer)
            pointer = await TailwindSetup.EnsurePointerAsync(cwd, config?.Css, settings.DryRun, ct);
        if (settings.Scrollbar)
            scrollbar = await TailwindSetup.EnsureScrollbarAsync(cwd, config?.Css, settings.DryRun, ct);

        // A full apply of a code carrying RTL records the flag, so later add/update runs keep the
        // skins' RTL readiness (the reinstall leg above already owed direction-provider). Scoped
        // legs stay surgical - --only theme must not flip project wiring.
        if (full && code?.Rtl == true && config is not null && !config.Rtl && !settings.DryRun)
        {
            config.Rtl = true;
            await ConfigStore.SaveAsync(cwd, config, ct);
        }

        if (settings.Json)
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["preset"] = preset,
                ["skin"] = skin,
                ["theme"] = applyTheme && theme?.Patched == true,
                ["components"] = reinstall,
                ["fonts"] = applyFonts && fonts?.HadSelection == true,
                ["tokens"] = applyTokens && tokens?.HadSelection == true,
                ["icons"] = iconsInstalled,
                ["pointer"] = settings.Pointer && pointer?.Patched == true,
                ["scrollbar"] = settings.Scrollbar && scrollbar?.Patched == true,
                ["dryRun"] = settings.DryRun,
            }.ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        var applied = settings.DryRun ? "Would apply" : "Applied";
        if (reinstall)
            AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "Would re-install" : "Re-installed")}[/] {config!.Installed.Count} component(s) from skin [cyan]{Markup.Escape(skin)}[/].");
        if (applyTheme && theme is { } th)
        {
            if (!th.Patched)
                settings.Warn("[yellow]No tokens file to patch - run 'blaizio add' first.[/]");
            else
                AnsiConsole.MarkupLine($"[green]{applied} theme[/] (preset [cyan]{Markup.Escape(preset)}[/]) to {Markup.Escape(th.Path!)}.");
        }
        if (applyFonts && fonts is { } f)
        {
            if (!f.HadSelection && !applyTheme)
                settings.Warn("[yellow]No font selection in the preset; nothing to apply.[/]");
            else if (f is { HadSelection: true, Patched: false })
                settings.Warn("[yellow]No tokens file to patch the fonts into - run 'blaizio add' first.[/]");
            else if (f.HadSelection)
                AnsiConsole.MarkupLine($"[green]{applied} fonts[/] to {Markup.Escape(f.Path!)}.");
        }
        if (applyTokens && tokens is { } t)
        {
            if (!t.HadSelection && !applyTheme && parts.Contains("tokens"))
                settings.Warn("[yellow]No chart/radius selection in the preset; nothing to apply.[/]");
            else if (t is { HadSelection: true, Patched: false })
                settings.Warn("[yellow]No tokens file to bake the chart/radius into - run 'blaizio add' first.[/]");
            else if (t.HadSelection)
                AnsiConsole.MarkupLine($"[green]{applied} chart/radius tokens[/] to {Markup.Escape(t.Path!)}.");
        }
        if (iconsInstalled)
            AnsiConsole.MarkupLine($"[green]{applied} icon set[/] [cyan]{Markup.Escape(code!.Icons)}[/] ({Markup.Escape(IconSetCatalog.Find(code.Icons)!.Package)}).");
        else if (applyIcons && parts.Contains("icons") && (code?.Icons ?? IconSetCatalog.Default) == IconSetCatalog.Default)
            settings.Warn("[yellow]No icon set in the preset beyond Tabler; nothing to install.[/]");
        if (pointer is { } ptr)
        {
            if (!ptr.Patched)
                settings.Warn("[yellow]No tokens file to wire the pointer cursor into - run 'blaizio add' first.[/]");
            else
                AnsiConsole.MarkupLine($"[green]{applied} pointer cursor[/] to {Markup.Escape(ptr.Path!)}.");
        }
        if (scrollbar is { } sb)
        {
            if (!sb.Patched)
                settings.Warn("[yellow]No tokens file to wire the thin scrollbars into - run 'blaizio add' first.[/]");
            else
                AnsiConsole.MarkupLine($"[green]{applied} thin scrollbars[/] to {Markup.Escape(sb.Path!)}.");
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
