using System.Text;
using System.Text.RegularExpressions;

namespace Blaizio.Cli.Core.Styling;

/// <summary>Init-time styling toggles baked into the tokens file.</summary>
/// <param name="Pointer">Give buttons a pointer cursor.</param>
/// <param name="Rtl">Right-to-left layout (recorded; the app must still set <c>dir="rtl"</c>).</param>
public readonly record struct TailwindOptions(bool Pointer = false, bool Rtl = false);

/// <summary>The outcome of a surgical tokens-file patch (fonts, chart/radius, preset), for reporting.</summary>
/// <param name="HadSelection">True when there was actually something to apply (a non-default
/// selection); false when every input was the built-in default.</param>
/// <param name="Patched">True when the tokens file existed and was patched; false when there was
/// no tokens file to patch (the project was never initialized).</param>
/// <param name="Path">Project-relative posix path of the patched tokens file, or <c>null</c>.</param>
public readonly record struct TokenPatchResult(bool HadSelection, bool Patched, string? Path);

/// <summary>The outcome of the v1 → v3 CSS migration, for reporting.</summary>
/// <param name="InputPath">The rewritten tokens file (project-relative posix).</param>
/// <param name="InputWasCliOwned">True when the input was CLI-written (v1 marker) or created by
/// the migration itself — what <c>cssCreated</c> records so uninstall may delete it.</param>
/// <param name="Removed">The deleted <c>Styles/blaizio/</c> files (project-relative posix).</param>
public sealed record MigrationResult(string InputPath, bool InputWasCliOwned, IReadOnlyList<string> Removed);

/// <summary>The outcome of wiring Tailwind into a project, for reporting.</summary>
public sealed class TailwindResult
{
    /// <summary>Tokens file / Tailwind input (project-relative), e.g. <c>Styles/app.css</c>.</summary>
    public required string InputPath { get; init; }

    /// <summary>The color preset whose values are baked in (<c>"nova"</c> = the default palette).</summary>
    public required string Preset { get; init; }

    /// <summary>True when the input file was created; false when an existing one was updated in place.</summary>
    public required bool InputCreated { get; init; }

    /// <summary>
    /// True when the project still carries the v1 layout (<c>Styles/blaizio/</c> imports) — nothing
    /// was touched; <c>blaizio update</c> runs the migration.
    /// </summary>
    public bool LegacyV1 { get; init; }
}

/// <summary>
/// Wires Tailwind v4 into a consumer project, v3 layout: ONE user-owned tokens file (default
/// <c>Styles/app.css</c>) holding the Tailwind input, the theme values (<c>:root</c>/<c>.dark</c>,
/// preset palette merged in, chart/radius/fonts baked as plain editable values) and the
/// <c>@theme inline</c> token map — plus imports of the contract sheets Blaizio.Base materializes
/// into <c>.blaizio/</c> at build. No <c>Styles/blaizio/</c> managed directory exists anymore:
/// the skin lives inlined in the components, the plumbing tracks the Base package.
/// After creation the CLI touches the file only surgically — imports kept in sync, token values
/// patched in place — never a rewrite.
/// </summary>
public sealed class TailwindSetup(ICssAssetProvider assets)
{
    internal const string StylesDir = "Styles";
    internal const string InputName = "app.css";

    /// <summary>The contract dir Blaizio.Base materializes at build (gitignored, like obj/).</summary>
    internal const string ContractDir = ".blaizio";

    /// <summary>The materialized contract sheet the tokens file imports.</summary>
    internal const string ContractSheet = "blaizio.css";

    /// <summary>The materialized vendored tw-animate sheet.</summary>
    internal const string AnimateSheet = "animate.css";

    /// <summary>The v1 managed CSS dir (<c>Styles/blaizio/</c>) — legacy detection only.</summary>
    internal const string LegacyManagedDir = "blaizio";

    /// <summary>The v1 full-file ownership marker (legacy detection and sync cleanup).</summary>
    internal const string Marker = "/* blaizio:managed */";

    /// <summary>Every CLI-written comment line starts with this — how sync and uninstall recognize
    /// their own lines (including legacy variants) inside a user-owned file.</summary>
    internal const string MarkerPrefix = "/* blaizio:";

    /// <summary>The header line above the synced block in a user-owned input.</summary>
    internal const string SyncHeader = "/* blaizio:managed imports (kept in sync by the CLI) */";

    /// <summary>The tokens-file marker of an initialized project: the contract import.</summary>
    internal const string ContractImportMarker = $"{ContractDir}/{ContractSheet}";

    /// <summary>Run the setup for <paramref name="projectDir"/>.</summary>
    /// <param name="projectDir">Project root.</param>
    /// <param name="componentOutput">Component output dir (project-relative), scanned by Tailwind.</param>
    /// <param name="options">Init-time styling toggles (pointer cursor, RTL).</param>
    /// <param name="preset">Color preset whose palette is merged into the scaffolded
    /// <c>:root</c>/<c>.dark</c>; <c>"nova"</c> = the default palette (theme values as-is).</param>
    /// <param name="topUpUserInput">Whether a user-authored <c>Styles/app.css</c> gets missing
    /// directives appended. <c>init</c> keeps the default (adopting an existing file is the point);
    /// <c>update</c> passes <c>false</c> so re-runs never append to a file the app owns.</param>
    /// <param name="cssInput">Custom Tailwind input path (project-relative, from blaizio.json
    /// <c>css</c>) for bundler setups. When set, the CLI never creates its own input: it keeps the
    /// Blaizio imports in THIS file in sync and injects the token block when absent — the
    /// <c>tailwindcss</c> import and app-markup scanning stay the bundler's business.</param>
    /// <param name="chart">Chart palette selection baked into the <c>:root</c> <c>--chart-*</c>
    /// values; <c>"default"</c> = the preset's own palette.</param>
    /// <param name="radius">Radius scale selection baked into <c>--radius</c>; <c>"default"</c> =
    /// the theme's own radius.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<TailwindResult> EnsureAsync(
        string projectDir,
        string componentOutput,
        TailwindOptions options = default,
        string preset = "nova",
        bool topUpUserInput = true,
        string? cssInput = null,
        string chart = "default",
        string radius = "default",
        CancellationToken ct = default)
    {
        var inputRel = cssInput ?? Path.Combine(StylesDir, InputName);
        var inputAbs = Path.GetFullPath(Path.Combine(projectDir, inputRel));
        var inputDirAbs = Path.GetDirectoryName(inputAbs)!;

        if (cssInput is not null && !File.Exists(inputAbs))
            throw new InvalidOperationException(
                $"The css input '{ToPosix(inputRel)}' (blaizio.json \"css\") does not exist. Create it or fix the path.");

        var hasPreset = !string.Equals(preset, "nova", StringComparison.OrdinalIgnoreCase);

        // A project still on the v1 layout (Styles/blaizio/ imports) is never silently rewritten:
        // flipping the imports to v3 would orphan the skin sheets while the components still carry
        // bz-* classes. `blaizio update` runs the migration; here we only report.
        if (IsLegacyV1(projectDir, inputAbs))
        {
            return new TailwindResult
            {
                InputPath = ToPosix(inputRel),
                Preset = hasPreset ? preset : "nova",
                InputCreated = false,
                LegacyV1 = true,
            };
        }

        var required = BuildRequiredLines(projectDir, componentOutput, cssInput, inputDirAbs);

        var created = !File.Exists(inputAbs);

        if (created)
        {
            Directory.CreateDirectory(inputDirAbs);
            await File.WriteAllTextAsync(inputAbs, BuildScaffold(required, preset, chart, radius, options), ct);
        }
        else
        {
            var text = await File.ReadAllTextAsync(inputAbs, ct);
            if (text.StartsWith(Marker, StringComparison.Ordinal) && cssInput is null)
            {
                // A v1 marker file with no v1 imports left (already-migrated edge) was fully
                // CLI-written — regenerate it as the v3 scaffold.
                await File.WriteAllTextAsync(inputAbs, BuildScaffold(required, preset, chart, radius, options), ct);
            }
            else if (topUpUserInput || cssInput is not null)
            {
                text = SyncInput(text, required);
                // Adopt: a file that never got the token block (no @theme inline map) gains the
                // full block — values, map and base layer — appended after its own content.
                if (!text.Contains("@theme inline", StringComparison.Ordinal))
                    text = $"{text.TrimEnd('\n')}\n\n{ComposeTokenBlock(preset, chart, radius, options.Pointer)}";
                await File.WriteAllTextAsync(inputAbs, text, ct);
            }
        }

        await EnsureGitignoreAsync(projectDir, ct);

        return new TailwindResult
        {
            InputPath = ToPosix(inputRel),
            Preset = hasPreset ? preset : "nova",
            InputCreated = created,
        };
    }

    /// <summary>
    /// The v1 → v3 migration's CSS leg (the component re-install is the caller's; see
    /// <c>update</c>): compose the v3 token block from the PROJECT's v1 managed sheets — the
    /// user's own theme.css values survive, the active preset sheet merges into
    /// <c>:root</c>/<c>.dark</c>, the fonts.css and options.css overlays fold in, and the
    /// v1-only leftovers (<c>.bz-font-heading</c>, the <c>--primary-button</c> button repaint)
    /// drop — then rewrite the input to the v3 shape and delete <c>Styles/blaizio/</c>.
    /// A marker-owned input is regenerated wholesale; a user/bundler input keeps its own content
    /// and gets the imports synced + the token block injected when absent.
    /// </summary>
    /// <returns>What happened, for reporting: the input path, whether the CLI owned it (feeds
    /// <c>cssCreated</c>), and the managed files that were deleted.</returns>
    public async Task<MigrationResult> MigrateAsync(
        string projectDir,
        string componentOutput,
        string preset = "nova",
        string? cssInput = null,
        CancellationToken ct = default)
    {
        var managedAbs = Path.Combine(projectDir, StylesDir, LegacyManagedDir);
        var block = ComposeMigratedTokenBlock(managedAbs, preset);

        var (inputRel, inputAbs) = InputPath(projectDir, cssInput);
        var inputDirAbs = Path.GetDirectoryName(inputAbs)!;
        var required = BuildRequiredLines(projectDir, componentOutput, cssInput, inputDirAbs);

        var wasManaged = false;
        if (File.Exists(inputAbs))
        {
            var text = await File.ReadAllTextAsync(inputAbs, ct);
            wasManaged = text.StartsWith(Marker, StringComparison.Ordinal);
            if (wasManaged && cssInput is null)
            {
                // Fully CLI-written v1 input: regenerate as the v3 scaffold around the composed block.
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("/* Tailwind input + Blaizio theme tokens. This file is yours: edit the :root/.dark values");
                sb.AppendLine("   to retheme. The CLI only keeps the imports in sync and patches values on apply. */");
                foreach (var line in required)
                    sb.AppendLine(line);
                sb.AppendLine();
                sb.Append(block);
                await File.WriteAllTextAsync(inputAbs, sb.ToString(), ct);
            }
            else
            {
                // A user/bundler input: strip the v1 lines, wire the v3 imports, inject the
                // composed block when the file doesn't carry a token map of its own.
                text = SyncInput(text, required);
                if (!text.Contains("@theme inline", StringComparison.Ordinal))
                    text = $"{text.TrimEnd('\n')}\n\n{block}";
                await File.WriteAllTextAsync(inputAbs, text, ct);
            }
        }
        else
        {
            Directory.CreateDirectory(inputDirAbs);
            var sb = new System.Text.StringBuilder();
            foreach (var line in required)
                sb.AppendLine(line);
            sb.AppendLine();
            sb.Append(block);
            await File.WriteAllTextAsync(inputAbs, sb.ToString(), ct);
            wasManaged = true; // this run created it - ours to delete on uninstall
        }

        // The v1 managed sheets are dead now - the values live in the tokens file, the contract
        // materializes into .blaizio/ at build, and the skin ships inlined in the components.
        var removed = new List<string>();
        if (Directory.Exists(managedAbs))
        {
            foreach (var file in Directory.EnumerateFiles(managedAbs, "*", SearchOption.AllDirectories))
                removed.Add(ToPosix(Path.GetRelativePath(projectDir, file)));
            Directory.Delete(managedAbs, recursive: true);
        }

        await EnsureGitignoreAsync(projectDir, ct);

        return new MigrationResult(ToPosix(inputRel), wasManaged, removed);
    }

    /// <summary>
    /// The v3 token block composed from the project's v1 sheets (embedded-asset fallback when a
    /// sheet is missing): theme.css values as the user left them, preset merged, fonts/pointer
    /// folded in, v1-only rules and the retired <c>--primary-button</c> dropped.
    /// </summary>
    internal string ComposeMigratedTokenBlock(string managedAbs, string preset)
    {
        var themePath = Path.Combine(managedAbs, "theme.css");
        var css = File.Exists(themePath) ? File.ReadAllText(themePath) : assets.GetThemeCss();
        css = CollapseBlankRuns(CssBlocks.StripComments(css)).TrimStart('\n');

        // v1-only pieces with no v3 home: the heading hook now inlines from shared.css, and the
        // dark button repaint derives from --primary (no extra token).
        css = CssBlocks.RemoveNestedBlock(css, "@layer base", ".bz-font-heading");
        css = CssBlocks.RemoveTopLevelBlock(css, ".dark .bz-button-variant-default");
        css = CssBlocks.RemoveTopLevelBlock(css, ".dark .bz-button-variant-default:hover");
        css = CssBlocks.RemoveDeclaration(css, ".dark", "--primary-button");

        if (!string.Equals(preset, "nova", StringComparison.OrdinalIgnoreCase))
        {
            var presetPath = Path.Combine(managedAbs, $"preset-{preset}.css");
            var presetCss = File.Exists(presetPath) ? File.ReadAllText(presetPath) : assets.GetPresetCss(preset);
            presetCss = CssBlocks.StripComments(presetCss);
            foreach (var (name, value) in CssBlocks.Declarations(presetCss, $".preset-{preset}"))
            {
                if (name != "--primary-button")
                    css = CssBlocks.SetDeclaration(css, ":root", name, value);
            }
            foreach (var (name, value) in CssBlocks.Declarations(presetCss, $".preset-{preset}.dark"))
            {
                if (name != "--primary-button")
                    css = CssBlocks.SetDeclaration(css, ".dark", name, value);
            }
        }

        // fonts.css: ":root { --font-heading: …; }" and/or a top-level "html { font-family: …; }".
        var fontsPath = Path.Combine(managedAbs, "fonts.css");
        if (File.Exists(fontsPath))
        {
            var fonts = CssBlocks.StripComments(File.ReadAllText(fontsPath));
            foreach (var (name, value) in CssBlocks.Declarations(fonts, ":root"))
            {
                if (name == "--font-heading")
                    css = CssBlocks.SetDeclaration(css, ":root", name, value);
            }
            var htmlLine = fonts.Split('\n').Select(l => l.Trim())
                .FirstOrDefault(l => l.StartsWith("html {", StringComparison.Ordinal) && l.EndsWith("}", StringComparison.Ordinal));
            if (htmlLine is not null)
                css = CssBlocks.SetNestedRule(css, "@layer base", "html", htmlLine);
        }

        // options.css: the only flag it ever carried is the pointer cursor.
        var optionsPath = Path.Combine(managedAbs, "options.css");
        if (File.Exists(optionsPath) && File.ReadAllText(optionsPath).Contains("cursor: pointer", StringComparison.Ordinal))
            css = CssBlocks.SetNestedRule(css, "@layer base", PointerPrelude, PointerRule);

        return css;
    }

    /// <summary>The directives the tokens file must carry: the Tailwind import + app-wide scan
    /// (default flow only — a bundler input owns both), the materialized contract imports, and
    /// the component @source globs.</summary>
    private static List<string> BuildRequiredLines(
        string projectDir, string componentOutput, string? cssInput, string inputDirAbs)
    {
        var contractPrefix = RelativePrefix(inputDirAbs, Path.Combine(projectDir, ContractDir));
        var sourceGlob = ToPosix(Path.GetRelativePath(inputDirAbs, Path.Combine(projectDir, componentOutput)));

        var required = new List<string>();
        if (cssInput is null)
        {
            // source(none) disables Tailwind's automatic source detection: without it the scanner
            // walks the whole project — including bin/obj build output, whose binaries crash it
            // ("value that is out of range of code points") — and we list our sources explicitly.
            // A bundler-owned input keeps its own tailwindcss import and scanning setup.
            required.Add("@import \"tailwindcss\" source(none);");
        }
        // The sheets Blaizio.Base materializes into .blaizio/ at build: the vendored tw-animate
        // (Node-free pipelines can't resolve node_modules) and the static contract (data-*
        // variants, keyframes, toast/chart machinery).
        required.Add($"@import \"{contractPrefix}/{AnimateSheet}\";");
        required.Add($"@import \"{contractPrefix}/{ContractSheet}\";");
        // Copied components (.razor + .cs class builders) so their utilities always generate.
        required.Add($"@source \"{sourceGlob}/**/*.razor\";");
        required.Add($"@source \"{sourceGlob}/**/*.cs\";");
        if (cssInput is null)
        {
            // Every other .razor (pages, layouts) so app markup utilities generate too. A bundler
            // input relies on its own content scanning for app markup.
            required.Add("@source \"../**/*.razor\";");
        }
        return required;
    }

    /// <summary>
    /// Patch the recorded heading/body font selection into the tokens file:
    /// <c>--font-heading</c> in <c>:root</c> (reset to the built-in default stack when the
    /// selection is <c>"default"</c>) and the document <c>font-family</c> as an
    /// <c>html {{ … }}</c> rule in <c>@layer base</c> (removed when default). Webfont LOADING is
    /// not this file's job: the css2 stylesheet rides on the host page (see
    /// <see cref="HostPageSetup.EnsureFontLinkAsync"/>) because an external CSS <c>@import</c>
    /// would be inlined mid-bundle by Tailwind, where it is ignored.
    /// </summary>
    /// <param name="projectDir">Project root.</param>
    /// <param name="heading">Heading font selection (a <see cref="PresetCode.Fonts"/> value).</param>
    /// <param name="font">Body font selection (a <see cref="PresetCode.Fonts"/> value).</param>
    /// <param name="cssInput">Bundler-owned css input (blaizio.json <c>css</c>), when configured.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<TokenPatchResult> EnsureFontsAsync(
        string projectDir,
        string heading,
        string font,
        string? cssInput = null,
        CancellationToken ct = default)
    {
        var headingStack = FontStacks.Stack(heading);
        var fontStack = FontStacks.Stack(font);

        // Both default/unknown: nothing was ever selected, nothing to reset.
        if (headingStack is null && fontStack is null)
            return new TokenPatchResult(false, false, null);

        var (inputRel, inputAbs) = InputPath(projectDir, cssInput);
        if (!File.Exists(inputAbs))
            return new TokenPatchResult(true, false, null);

        var css = await File.ReadAllTextAsync(inputAbs, ct);
        css = CssBlocks.SetDeclaration(css, ":root", "--font-heading", headingStack ?? HeadingDefault);
        css = fontStack is null
            ? CssBlocks.RemoveNestedRule(css, "@layer base", "html")
            : CssBlocks.SetNestedRule(css, "@layer base", "html", $"html {{ font-family: {fontStack}; }}");
        await File.WriteAllTextAsync(inputAbs, css, ct);
        return new TokenPatchResult(true, true, ToPosix(inputRel));
    }

    /// <summary>
    /// Patch a chart/radius selection into the tokens file's <c>:root</c> — the scoped
    /// <c>apply --only tokens</c> path, which must not touch anything else in the file.
    /// </summary>
    /// <param name="projectDir">Project root.</param>
    /// <param name="chart">Chart palette selection (a <see cref="PresetCode.Charts"/> value).</param>
    /// <param name="radius">Radius scale selection (a <see cref="PresetCode.Radii"/> value).</param>
    /// <param name="cssInput">Bundler-owned css input (blaizio.json <c>css</c>), when configured.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<TokenPatchResult> EnsureThemeTokensAsync(
        string projectDir,
        string chart,
        string radius,
        string? cssInput = null,
        CancellationToken ct = default)
    {
        if (TokenOverlays.Radius(radius) is null && TokenOverlays.Chart(chart) is null)
            return new TokenPatchResult(false, false, null);

        var (inputRel, inputAbs) = InputPath(projectDir, cssInput);
        if (!File.Exists(inputAbs))
            return new TokenPatchResult(true, false, null);

        var css = WithTokenOverrides(await File.ReadAllTextAsync(inputAbs, ct), chart, radius);
        await File.WriteAllTextAsync(inputAbs, css, ct);
        return new TokenPatchResult(true, true, ToPosix(inputRel));
    }

    /// <summary>
    /// Re-theme an existing tokens file to <paramref name="preset"/>: every token the base theme
    /// defines is patched to the preset-merged value, in place — the <c>apply --only theme</c>
    /// leg. The user's own extra declarations survive; <c>--font-heading</c> is skipped (fonts are
    /// their own selection) and the recorded <paramref name="chart"/>/<paramref name="radius"/>
    /// overlays are re-applied afterwards so a re-theme never loses them.
    /// </summary>
    public async Task<TokenPatchResult> ApplyPresetAsync(
        string projectDir,
        string preset,
        string? cssInput = null,
        string chart = "default",
        string radius = "default",
        CancellationToken ct = default)
    {
        var (inputRel, inputAbs) = InputPath(projectDir, cssInput);
        if (!File.Exists(inputAbs))
            return new TokenPatchResult(true, false, null);

        var merged = ComposeTokenBlock(preset, chart, radius, pointer: false);
        var css = await File.ReadAllTextAsync(inputAbs, ct);
        foreach (var (name, value) in CssBlocks.Declarations(merged, ":root"))
        {
            if (name is "--font-heading")
                continue;
            css = CssBlocks.SetDeclaration(css, ":root", name, value);
        }
        foreach (var (name, value) in CssBlocks.Declarations(merged, ".dark"))
            css = CssBlocks.SetDeclaration(css, ".dark", name, value);

        await File.WriteAllTextAsync(inputAbs, css, ct);
        return new TokenPatchResult(true, true, ToPosix(inputRel));
    }

    /// <summary>
    /// Bake a chart/radius selection into the tokens file's <c>:root</c> by patching the matching
    /// declarations. <c>"default"</c> selections leave the values untouched — the theme's own
    /// values ARE the default.
    /// </summary>
    internal static string WithTokenOverrides(string css, string chart, string radius)
    {
        if (TokenOverlays.Radius(radius) is { } radiusValue)
            css = CssBlocks.SetDeclaration(css, ":root", "--radius", radiusValue);
        if (TokenOverlays.Chart(chart) is { } chartValues)
            for (var i = 0; i < chartValues.Count; i++)
                css = CssBlocks.SetDeclaration(css, ":root", $"--chart-{i + 1}", chartValues[i]);
        return css;
    }

    /// <summary>
    /// True when the project runs its own Tailwind pipeline: a <c>Styles/app.css</c> exists that
    /// neither carries the v1 marker nor references the Blaizio sheets (v1 managed or v3
    /// contract). <c>update</c> uses this to leave such a project's styling entirely alone.
    /// </summary>
    public static bool HasCustomInput(string projectDir)
    {
        var inputAbs = Path.Combine(projectDir, StylesDir, InputName);
        if (!File.Exists(inputAbs))
            return false;

        var text = File.ReadAllText(inputAbs);
        return !text.StartsWith(Marker, StringComparison.Ordinal)
            && !text.Contains($"{LegacyManagedDir}/", StringComparison.Ordinal)
            && !text.Contains(ContractImportMarker, StringComparison.Ordinal);
    }

    /// <summary>
    /// True when the tokens file (the default input or <paramref name="cssInput"/>) already
    /// imports the materialized contract — the v3 "initialized" marker.
    /// </summary>
    public static bool HasContractImport(string projectDir, string? cssInput = null)
    {
        var (_, inputAbs) = InputPath(projectDir, cssInput);
        return File.Exists(inputAbs)
            && File.ReadAllText(inputAbs).Contains(ContractImportMarker, StringComparison.Ordinal);
    }

    /// <summary>
    /// True when the project is still on the v1 CSS layout: the input imports the old
    /// <c>Styles/blaizio/</c> managed assets (or that directory still holds them). The v1→v3
    /// migration in <c>update</c> is what moves such a project forward.
    /// </summary>
    public static bool IsLegacyV1(string projectDir, string? inputAbsOverride = null)
    {
        var inputAbs = inputAbsOverride ?? Path.Combine(projectDir, StylesDir, InputName);
        if (File.Exists(inputAbs))
        {
            foreach (var line in File.ReadAllLines(inputAbs))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("@import", StringComparison.Ordinal)
                    && trimmed.Contains($"{LegacyManagedDir}/", StringComparison.Ordinal)
                    && !trimmed.Contains($"{ContractDir}/", StringComparison.Ordinal))
                    return true;
            }
        }
        return File.Exists(Path.Combine(projectDir, StylesDir, LegacyManagedDir, "theme.css"));
    }

    /// <summary>The built-in <c>--font-heading</c> value (tracks the theme asset's default).</summary>
    internal const string HeadingDefault = "var(--font-sans, ui-sans-serif, system-ui, sans-serif)";

    /// <summary>The pointer-cursor rule the <c>--pointer</c> flag adds to <c>@layer base</c>
    /// (Tailwind v4 buttons default to <c>cursor-default</c>).</summary>
    internal const string PointerPrelude = "button:not(:disabled), [role=\"button\"]:not(:disabled)";

    private const string PointerRule = $"{PointerPrelude} {{ cursor: pointer; }}";

    /// <summary>The full fresh-init tokens file: directives up top, token block below.</summary>
    private string BuildScaffold(
        IReadOnlyList<string> required, string preset, string chart, string radius, TailwindOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("/* Tailwind input + Blaizio theme tokens. This file is yours: edit the :root/.dark values");
        sb.AppendLine("   to retheme. The CLI only keeps the imports in sync and patches values on apply. */");
        foreach (var line in required)
            sb.AppendLine(line);
        sb.AppendLine();
        sb.Append(ComposeTokenBlock(preset, chart, radius, options.Pointer));
        return sb.ToString();
    }

    /// <summary>
    /// The token block of the tokens file: the theme asset (dark variant, <c>@theme inline</c>
    /// map, <c>:root</c>/<c>.dark</c> values, base layer) with the preset palette merged into the
    /// value blocks, the chart/radius selection baked, and the pointer rule when enabled —
    /// comment-free, plain editable values.
    /// </summary>
    internal string ComposeTokenBlock(string preset, string chart, string radius, bool pointer)
    {
        var css = CssBlocks.StripComments(assets.GetThemeCss());
        css = CollapseBlankRuns(css).TrimStart('\n');

        if (!string.Equals(preset, "nova", StringComparison.OrdinalIgnoreCase))
        {
            var presetCss = CssBlocks.StripComments(assets.GetPresetCss(preset));
            foreach (var (name, value) in CssBlocks.Declarations(presetCss, $".preset-{preset}"))
                css = CssBlocks.SetDeclaration(css, ":root", name, value);
            foreach (var (name, value) in CssBlocks.Declarations(presetCss, $".preset-{preset}.dark"))
                css = CssBlocks.SetDeclaration(css, ".dark", name, value);
        }

        css = WithTokenOverrides(css, chart, radius);
        if (pointer)
            css = CssBlocks.SetNestedRule(css, "@layer base", PointerPrelude, PointerRule);
        return css;
    }

    /// <summary>Append <c>.blaizio/</c> to the project's .gitignore (creating one when absent) —
    /// the materialized contract is build output, like obj/.</summary>
    internal static async Task EnsureGitignoreAsync(string projectDir, CancellationToken ct)
    {
        var path = Path.Combine(projectDir, ".gitignore");
        const string entry = $"{ContractDir}/";
        if (File.Exists(path))
        {
            var lines = await File.ReadAllLinesAsync(path, ct);
            if (lines.Any(l => l.Trim() is entry or ContractDir))
                return;
            var text = await File.ReadAllTextAsync(path, ct);
            await File.WriteAllTextAsync(path, $"{text.TrimEnd('\n')}\n{entry}\n", ct);
        }
        else
        {
            await File.WriteAllTextAsync(path, $"{entry}\n", ct);
        }
    }

    /// <summary>
    /// Sync a user-owned input with the managed directives — owning their content AND their
    /// position: every managed line (current, stale v1, or misplaced, including a hand-written
    /// mirror of the same imports) is removed from wherever it sits and the required set is
    /// reinserted right after the file's last <c>@import</c>. Idempotent; everything the user
    /// authored stays.
    /// </summary>
    private static string SyncInput(string original, IReadOnlyList<string> required)
    {
        var lines = original.Split('\n').ToList();

        bool Ours(string line)
        {
            if (line.Contains(MarkerPrefix, StringComparison.Ordinal))
                return true;
            var trimmed = line.TrimStart();
            if ((trimmed.StartsWith("@import", StringComparison.Ordinal) || trimmed.StartsWith("@source", StringComparison.Ordinal))
                && line.Contains($"{LegacyManagedDir}/", StringComparison.Ordinal))
                return true;
            // Directives that don't mention blaizio/ (the component @source globs) match exactly.
            return required.Contains(line.Trim());
        }

        lines.RemoveAll(Ours);

        var text = string.Join('\n', lines);
        var toInsert = required.Where(line => !text.Contains(line, StringComparison.Ordinal)).ToArray();
        if (toInsert.Length > 0)
            lines.InsertRange(InsertionIndex(lines), [SyncHeader, .. toInsert]);

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Where new directives go: right after the file's last <c>@import</c>. CSS ignores an
    /// <c>@import</c> that follows any other rule, so appending at the end would be dead code.
    /// </summary>
    private static int InsertionIndex(List<string> lines)
    {
        var index = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("@import", StringComparison.Ordinal))
                index = i + 1;
        }
        return index;
    }

    /// <summary>The tokens file this project uses: the recorded bundler input, or the default.</summary>
    private static (string Rel, string Abs) InputPath(string projectDir, string? cssInput)
    {
        var rel = cssInput ?? Path.Combine(StylesDir, InputName);
        return (rel, Path.GetFullPath(Path.Combine(projectDir, rel)));
    }

    /// <summary>Collapse runs of blank lines (comment stripping leaves them behind).</summary>
    private static string CollapseBlankRuns(string css)
        => BlankRunRegex().Replace(css.Replace("\r\n", "\n"), "\n\n");

    private static readonly Regex s_blankRun = new(@"\n{3,}", RegexOptions.Compiled);

    private static Regex BlankRunRegex() => s_blankRun;

    /// <summary>Import prefix from the input file's directory to a target dir (POSIX, ./-anchored).
    /// Anchors unless the path already climbs (<c>../…</c>) — a dot-DIRECTORY like
    /// <c>.blaizio</c> still needs the <c>./</c>.</summary>
    private static string RelativePrefix(string fromDirAbs, string toDirAbs)
    {
        var rel = ToPosix(Path.GetRelativePath(fromDirAbs, toDirAbs));
        return rel.StartsWith("../", StringComparison.Ordinal) || rel == ".." ? rel : $"./{rel}";
    }

    private static string ToPosix(string path) => path.Replace('\\', '/');
}
