using Blaizio.Cli.Core.Registry;

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

/// <summary>The outcome of <c>eject</c>, for reporting.</summary>
/// <param name="InputPath">The tokens file the contract was copied into (project-relative posix).</param>
/// <param name="Materialized">True when the content came from the project's materialized
/// <c>.blaizio/</c> sheets (version-matching the installed Blaizio.Base); false when it fell back
/// to the CLI's embedded copies (the project was never built).</param>
public sealed record EjectResult(string InputPath, bool Materialized);

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

    /// <summary>
    /// True when the project is ejected (<c>blaizio.json "ejected"</c>) — the tokens file owns the
    /// contract and was left untouched.
    /// </summary>
    public bool Ejected { get; init; }
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
/// <remarks>
/// This type is the stable face the commands and services call; the work lives in three
/// collaborators in this folder: <see cref="TokensFileScaffolder"/> (create/sync + the shared
/// tokens-file mechanics), <see cref="TokensFilePatcher"/> (the surgical value patches), and
/// <see cref="TailwindMigration"/> (the one-shot v1 → v3 migration and eject transitions).
/// The layout constants and project-state detection stay here — they ARE the contract.
/// </remarks>
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

    /// <summary>The built-in <c>--font-heading</c> value (tracks the theme asset's default).</summary>
    internal const string HeadingDefault = "var(--font-sans, ui-sans-serif, system-ui, sans-serif)";

    /// <summary>The pointer-cursor rule the <c>--pointer</c> flag adds to <c>@layer base</c>
    /// (Tailwind v4 buttons default to <c>cursor-default</c>).</summary>
    internal const string PointerPrelude = "button:not(:disabled), [role=\"button\"]:not(:disabled)";

    internal const string PointerRule = $"{PointerPrelude} {{ cursor: pointer; }}";

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
    /// <param name="ejected">The project's <c>blaizio.json "ejected"</c> flag. An ejected tokens
    /// file owns the contract (the imports are gone by design) — syncing would reinsert them, so
    /// the file is left entirely alone.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<TailwindResult> EnsureAsync(
        string projectDir,
        string componentOutput,
        TailwindOptions options = default,
        string preset = "nova",
        bool topUpUserInput = true,
        string? cssInput = null,
        string chart = "default",
        string radius = "default",
        bool ejected = false,
        CancellationToken ct = default) =>
        new TokensFileScaffolder(assets).EnsureAsync(
            projectDir, componentOutput, options, preset, topUpUserInput, cssInput, chart, radius, ejected, ct);

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
    public Task<MigrationResult> MigrateAsync(
        string projectDir,
        string componentOutput,
        string preset = "nova",
        string? cssInput = null,
        CancellationToken ct = default) =>
        new TailwindMigration(assets).MigrateAsync(projectDir, componentOutput, preset, cssInput, ct);

    /// <summary>
    /// <c>blaizio eject</c>: copy the contract INTO the tokens file and stop depending on the
    /// <c>.blaizio/</c> materialization — the two <c>@import</c>s are removed and the sheets'
    /// content is appended, frozen at the current version and the user's to edit. Content comes
    /// from the project's materialized <c>.blaizio/</c> when present (it version-tracks the
    /// installed Blaizio.Base); the CLI's embedded copies are the never-built fallback.
    /// Irreversibility, confirmation and the <c>"ejected"</c> config flag are the caller's.
    /// </summary>
    /// <exception cref="InvalidOperationException">No tokens file, or it doesn't import the
    /// contract (not initialized, or already ejected).</exception>
    public Task<EjectResult> EjectAsync(
        string projectDir, string? cssInput = null, CancellationToken ct = default) =>
        new TailwindMigration(assets).EjectAsync(projectDir, cssInput, ct);

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
    /// <param name="dryRun">Report the outcome without writing the file.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task<TokenPatchResult> EnsureFontsAsync(
        string projectDir,
        string heading,
        string font,
        string? cssInput = null,
        bool dryRun = false,
        CancellationToken ct = default) =>
        TokensFilePatcher.EnsureFontsAsync(projectDir, heading, font, cssInput, dryRun, ct);

    /// <summary>
    /// Patch a chart/radius selection into the tokens file's <c>:root</c> — the scoped
    /// <c>apply --only tokens</c> path, which must not touch anything else in the file.
    /// </summary>
    /// <param name="projectDir">Project root.</param>
    /// <param name="chart">Chart palette selection (a <see cref="PresetCode.Charts"/> value).</param>
    /// <param name="radius">Radius scale selection (a <see cref="PresetCode.Radii"/> value).</param>
    /// <param name="cssInput">Bundler-owned css input (blaizio.json <c>css</c>), when configured.</param>
    /// <param name="dryRun">Report the outcome without writing the file.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task<TokenPatchResult> EnsureThemeTokensAsync(
        string projectDir,
        string chart,
        string radius,
        string? cssInput = null,
        bool dryRun = false,
        CancellationToken ct = default) =>
        TokensFilePatcher.EnsureThemeTokensAsync(projectDir, chart, radius, cssInput, dryRun, ct);

    /// <summary>
    /// Patch a registry theme item's token overrides into the tokens file: <c>light</c> values
    /// into <c>:root</c>, <c>dark</c> values into <c>.dark</c>. A block a spec targets that the
    /// file lacks is appended first, so a minimal tokens file still takes the full theme.
    /// </summary>
    /// <param name="projectDir">Project root.</param>
    /// <param name="vars">The item's <c>cssVars</c> payload.</param>
    /// <param name="cssInput">Bundler-owned css input (blaizio.json <c>css</c>), when configured.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task<TokenPatchResult> EnsureCssVarsAsync(
        string projectDir,
        CssVarsSpec vars,
        string? cssInput = null,
        CancellationToken ct = default) =>
        TokensFilePatcher.EnsureCssVarsAsync(projectDir, vars, cssInput, ct);

    /// <summary>Apply <paramref name="vars"/> to a tokens file's text (see <see cref="EnsureCssVarsAsync"/>).</summary>
    internal static string ApplyCssVars(string css, CssVarsSpec vars) => TokensFilePatcher.ApplyCssVars(css, vars);

    /// <summary>
    /// Re-theme an existing tokens file to <paramref name="preset"/>: every token the base theme
    /// defines is patched to the preset-merged value, in place — the <c>apply --only theme</c>
    /// leg. The user's own extra declarations survive; <c>--font-heading</c> is skipped (fonts are
    /// their own selection) and the recorded <paramref name="chart"/>/<paramref name="radius"/>
    /// overlays are re-applied afterwards so a re-theme never loses them.
    /// </summary>
    public Task<TokenPatchResult> ApplyPresetAsync(
        string projectDir,
        string preset,
        string? cssInput = null,
        string chart = "default",
        string radius = "default",
        bool dryRun = false,
        CancellationToken ct = default) =>
        new TokensFilePatcher(assets).ApplyPresetAsync(projectDir, preset, cssInput, chart, radius, dryRun, ct);

    /// <summary>
    /// Bake a chart/radius selection into the tokens file's <c>:root</c> by patching the matching
    /// declarations. <c>"default"</c> selections leave the values untouched — the theme's own
    /// values ARE the default.
    /// </summary>
    internal static string WithTokenOverrides(string css, string chart, string radius) =>
        TokensFilePatcher.WithTokenOverrides(css, chart, radius);

    /// <summary>See <see cref="TokensFileScaffolder.HasTokenMap"/>.</summary>
    internal static bool HasTokenMap(string text) => TokensFileScaffolder.HasTokenMap(text);

    /// <summary>See <see cref="TokensFileScaffolder.ComposeTokenBlock"/>.</summary>
    internal string ComposeTokenBlock(string preset, string chart, string radius, bool pointer) =>
        new TokensFileScaffolder(assets).ComposeTokenBlock(preset, chart, radius, pointer);

    /// <summary>See <see cref="TailwindMigration.ComposeMigratedTokenBlock"/>.</summary>
    internal string ComposeMigratedTokenBlock(string managedAbs, string preset) =>
        new TailwindMigration(assets).ComposeMigratedTokenBlock(managedAbs, preset);

    /// <summary>See <see cref="TokensFileScaffolder.EnsureGitignoreAsync"/>.</summary>
    internal static Task EnsureGitignoreAsync(string projectDir, CancellationToken ct) =>
        TokensFileScaffolder.EnsureGitignoreAsync(projectDir, ct);

    // ---- project-state detection (the contract the commands branch on) ----------------------------

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
        var (_, inputAbs) = TokensFileScaffolder.InputPath(projectDir, cssInput);
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
}
