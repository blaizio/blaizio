using System.Text;

namespace Blaizio.Cli.Core.Styling;

/// <summary>Init-time styling toggles baked into the managed CSS.</summary>
/// <param name="Pointer">Give buttons a pointer cursor.</param>
/// <param name="Rtl">Right-to-left layout (recorded; the app must still set <c>dir="rtl"</c>).</param>
public readonly record struct TailwindOptions(bool Pointer = false, bool Rtl = false);

/// <summary>The outcome of writing the font overlay, for reporting.</summary>
/// <param name="HadSelection">True when the preset actually customized a heading and/or body font
/// (so a <c>fonts.css</c> was written); false when both were the built-in default.</param>
/// <param name="ImportWired">True when the <c>@import</c> for the overlay was added to
/// <c>Styles/app.css</c>; false when no input file existed to wire it into.</param>
/// <param name="Path">Project-relative posix path of the overlay, or <c>null</c> when none was written.</param>
public readonly record struct FontOverlayResult(bool HadSelection, bool ImportWired, string? Path);

/// <summary>The outcome of wiring Tailwind into a project, for reporting.</summary>
public sealed class TailwindResult
{
    /// <summary>Tailwind input file (project-relative), e.g. <c>Styles/app.css</c>.</summary>
    public required string InputPath { get; init; }

    /// <summary>Managed asset files written under <c>Styles/blaizio/</c> (project-relative).</summary>
    public required IReadOnlyList<string> Assets { get; init; }

    /// <summary>The skin that was installed.</summary>
    public required string Skin { get; init; }

    /// <summary>The color preset that was installed (<c>"nova"</c> = the default, no preset file).</summary>
    public required string Preset { get; init; }

    /// <summary>True when the input file was created; false when an existing one was updated in place.</summary>
    public required bool InputCreated { get; init; }
}

/// <summary>
/// Wires Tailwind v4 into a consumer project: writes the managed CSS assets (theme tokens, base
/// contract, chosen skin) under <c>Styles/blaizio/</c> and generates or updates the Tailwind input
/// <c>Styles/app.css</c> so it imports them, scans the component output directory, and enables the
/// dark variant. Re-running is safe: managed assets are rewritten; an existing input is only topped
/// up with any missing <c>@import</c>/<c>@source</c> lines, never clobbered.
/// </summary>
public sealed class TailwindSetup(ICssAssetProvider assets)
{
    internal const string StylesDir = "Styles";
    internal const string ManagedDir = "blaizio";
    internal const string InputName = "app.css";
    internal const string Marker = "/* blaizio:managed */";

    /// <summary>Run the setup for <paramref name="projectDir"/>.</summary>
    /// <param name="projectDir">Project root.</param>
    /// <param name="componentOutput">Component output dir (project-relative), scanned by Tailwind.</param>
    /// <param name="skin">Skin to install (without the <c>style-</c> prefix).</param>
    /// <param name="options">Init-time styling toggles (pointer cursor, RTL).</param>
    /// <param name="preset">Color preset to install (without the <c>preset-</c> prefix);
    /// <c>"nova"</c> - the default palette baked into theme.css - writes no preset file.</param>
    /// <param name="topUpUserInput">Whether a user-authored (unmanaged) <c>Styles/app.css</c> gets
    /// missing directives appended. <c>init</c> keeps the default (adopting an existing file is the
    /// point); <c>update</c> passes <c>false</c> so re-runs never append to a file the app owns.</param>
    /// <param name="cssInput">Custom Tailwind input path (project-relative, from blaizio.json
    /// <c>css</c>) for bundler setups. When set, the CLI never writes its own input: it keeps the
    /// managed imports in THIS file in sync — and leaves the <c>tailwindcss</c> import and source
    /// scanning to the bundler's own configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<TailwindResult> EnsureAsync(
        string projectDir,
        string componentOutput,
        string skin,
        TailwindOptions options = default,
        string preset = "nova",
        bool topUpUserInput = true,
        string? cssInput = null,
        CancellationToken ct = default)
    {
        var stylesAbs = Path.Combine(projectDir, StylesDir);
        var managedAbs = Path.Combine(stylesAbs, ManagedDir);
        Directory.CreateDirectory(managedAbs);

        // Managed assets: always (re)written so they track the installed tool version.
        var skinFile = $"style-{skin}.css";
        await WriteAsset(managedAbs, "theme.css", assets.GetThemeCss(), ct);
        await WriteAsset(managedAbs, "animate.css", assets.GetAnimateCss(), ct);
        await WriteAsset(managedAbs, "base.css", assets.GetBaseCss(), ct);
        await WriteAsset(managedAbs, "shared.css", assets.GetSharedSkinCss(), ct);
        await WriteAsset(managedAbs, skinFile, assets.GetSkinCss(skin), ct);

        // The color preset: a token sheet scoped under .preset-<name>, imported right after
        // theme.css so it wins the specificity tie against :root by source order. "nova" (the
        // default palette) has no file - it IS theme.css.
        var hasPreset = !string.Equals(preset, "nova", StringComparison.OrdinalIgnoreCase);
        var presetFile = $"preset-{preset}.css";
        if (hasPreset)
            await WriteAsset(managedAbs, presetFile, assets.GetPresetCss(preset), ct);

        // Drop any previously-installed preset so a change (or a switch back to nova) doesn't
        // leave an orphan file behind.
        foreach (var stale in Directory.EnumerateFiles(managedAbs, "preset-*.css")
                     .Where(p => !hasPreset || !string.Equals(Path.GetFileName(p), presetFile, StringComparison.OrdinalIgnoreCase)))
            File.Delete(stale);

        // Optional flag-driven overrides. Written only when something is enabled, and imported last
        // so it wins. Removed when empty so toggling a flag off cleans up.
        var optionsCss = BuildOptionsCss(options);
        var optionsAbs = Path.Combine(managedAbs, "options.css");
        var hasOptions = optionsCss.Length > 0;
        if (hasOptions)
            await WriteAsset(managedAbs, "options.css", optionsCss, ct);
        else if (File.Exists(optionsAbs))
            File.Delete(optionsAbs);

        // Drop any previously-installed skin so a skin change doesn't leave an orphan file behind.
        foreach (var stale in Directory.EnumerateFiles(managedAbs, "style-*.css")
                     .Where(p => !string.Equals(Path.GetFileName(p), skinFile, StringComparison.OrdinalIgnoreCase)))
            File.Delete(stale);

        // The input this run maintains: the CLI's own Styles/app.css, or - bundler mode - the file
        // blaizio.json `css` points at. Import/source paths are relative to wherever that file sits.
        var inputRel = cssInput ?? Path.Combine(StylesDir, InputName);
        var inputAbs = Path.GetFullPath(Path.Combine(projectDir, inputRel));
        var inputDirAbs = Path.GetDirectoryName(inputAbs)!;
        var managedPrefix = RelativePrefix(inputDirAbs, managedAbs);

        // @source globs are relative to the input file; point them at the component dir.
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
        // tw-animate-css is vendored so the Node-free standalone binary can resolve it.
        required.Add($"@import \"{managedPrefix}/animate.css\";");
        required.Add($"@import \"{managedPrefix}/theme.css\";");
        // The preset must follow theme.css (source order breaks the :root vs .preset-* tie) and
        // stay unlayered so it participates in the same cascade as the base tokens.
        if (hasPreset)
            required.Add($"@import \"{managedPrefix}/{presetFile}\";");
        required.AddRange(
        [
            $"@import \"{managedPrefix}/base.css\";",
            // The shared skin layer must precede the skin: the skin's scoped rules override it.
            $"@import \"{managedPrefix}/shared.css\" layer(components);",
            $"@import \"{managedPrefix}/{skinFile}\" layer(components);",
        ]);
        if (hasOptions)
            required.Add($"@import \"{managedPrefix}/options.css\";");
        // Copied components (.razor + .cs class builders) so their utilities always generate.
        required.Add($"@source \"{sourceGlob}/**/*.razor\";");
        required.Add($"@source \"{sourceGlob}/**/*.cs\";");
        if (cssInput is null)
        {
            // Every other .razor (pages, layouts) so app markup utilities generate too. A bundler
            // input relies on its own content scanning for app markup.
            required.Add("@source \"../**/*.razor\";");
        }

        if (cssInput is not null && !File.Exists(inputAbs))
            throw new InvalidOperationException(
                $"The css input '{ToPosix(inputRel)}' (blaizio.json \"css\") does not exist. Create it or fix the path.");

        var created = !File.Exists(inputAbs);

        // Create fresh, or fully regenerate a file we own (marker present) so a skin change doesn't
        // leave a stale import. A user-authored file is synced: stale managed skin/preset/options
        // lines swapped, missing directives appended. Bundler mode always syncs - that's the point.
        var isManaged = created ||
            (await File.ReadAllTextAsync(inputAbs, ct)).StartsWith(Marker, StringComparison.Ordinal);
        if (isManaged && cssInput is null)
            await File.WriteAllTextAsync(inputAbs, BuildInput(required), ct);
        else if (topUpUserInput || cssInput is not null)
            await SyncInput(inputAbs, required, ct);

        return new TailwindResult
        {
            InputPath = ToPosix(inputRel),
            Assets =
            [
                ToPosix(Path.Combine(StylesDir, ManagedDir, "theme.css")),
                ToPosix(Path.Combine(StylesDir, ManagedDir, "base.css")),
                ToPosix(Path.Combine(StylesDir, ManagedDir, "shared.css")),
                ToPosix(Path.Combine(StylesDir, ManagedDir, skinFile)),
                .. hasPreset ? new[] { ToPosix(Path.Combine(StylesDir, ManagedDir, presetFile)) } : [],
            ],
            Skin = skin,
            Preset = hasPreset ? preset : "nova",
            InputCreated = created,
        };
    }

    /// <summary>
    /// Write the font overlay for a preset code's heading/body selection. Emits a
    /// <c>Styles/blaizio/fonts.css</c> that sets <c>--font-heading</c> and/or the document
    /// <c>font-family</c>, and imports it from <c>Styles/app.css</c> after the preset/skin imports so
    /// it wins. Nothing is written when both selections are the built-in default.
    /// </summary>
    /// <param name="projectDir">Project root.</param>
    /// <param name="heading">Heading font selection (a <see cref="PresetCode.Fonts"/> value).</param>
    /// <param name="font">Body font selection (a <see cref="PresetCode.Fonts"/> value).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<FontOverlayResult> EnsureFontsAsync(
        string projectDir,
        string heading,
        string font,
        string? cssInput = null,
        CancellationToken ct = default)
    {
        var headingStack = FontStacks.Stack(heading);
        var fontStack = FontStacks.Stack(font);

        // Both default/unknown: no overlay to write.
        if (headingStack is null && fontStack is null)
            return new FontOverlayResult(false, false, null);

        var sb = new StringBuilder();
        sb.AppendLine(Marker);
        sb.AppendLine("/* Font overlay written by 'blaizio init'. */");
        if (headingStack is not null)
            sb.AppendLine($":root {{ --font-heading: {headingStack}; }}");
        if (fontStack is not null)
            sb.AppendLine($"html {{ font-family: {fontStack}; }}");

        var stylesAbs = Path.Combine(projectDir, StylesDir);
        var managedAbs = Path.Combine(stylesAbs, ManagedDir);
        Directory.CreateDirectory(managedAbs);
        await WriteAsset(managedAbs, "fonts.css", sb.ToString(), ct);

        // Wire the import — after the preset/skin imports so it wins — but only into an existing
        // input (the CLI's own, or the bundler input blaizio.json `css` points at). Without one we
        // don't fabricate a full input file (that's init's job).
        var inputRel = cssInput ?? Path.Combine(StylesDir, InputName);
        var inputAbs = Path.GetFullPath(Path.Combine(projectDir, inputRel));
        var importLine = $"@import \"{RelativePrefix(Path.GetDirectoryName(inputAbs)!, managedAbs)}/fonts.css\";";
        var importWired = false;
        if (File.Exists(inputAbs))
        {
            var existing = await File.ReadAllTextAsync(inputAbs, ct);
            if (!existing.Contains(importLine, StringComparison.Ordinal))
            {
                // After the last @import - a trailing @import is dead code in CSS.
                var lines = existing.Split('\n').ToList();
                lines.InsertRange(InsertionIndex(lines), [$"{Marker} (added)", importLine]);
                await File.WriteAllTextAsync(inputAbs, string.Join('\n', lines), ct);
            }
            importWired = true;
        }

        return new FontOverlayResult(true, importWired, ToPosix(Path.Combine(StylesDir, ManagedDir, "fonts.css")));
    }

    /// <summary>
    /// True when the project runs its own Tailwind pipeline: a <c>Styles/app.css</c> exists that is
    /// neither managed (no <see cref="Marker"/>) nor references the managed <c>Styles/blaizio/</c>
    /// assets. <c>update</c> uses this to leave such a project's styling entirely alone - writing
    /// the managed assets or appending imports there would just be spam.
    /// </summary>
    public static bool HasCustomInput(string projectDir)
    {
        var inputAbs = Path.Combine(projectDir, StylesDir, InputName);
        if (!File.Exists(inputAbs))
            return false;

        var text = File.ReadAllText(inputAbs);
        return !text.StartsWith(Marker, StringComparison.Ordinal)
            && !text.Contains($"./{ManagedDir}/", StringComparison.Ordinal);
    }

    private static async Task WriteAsset(string dir, string name, string content, CancellationToken ct)
        => await File.WriteAllTextAsync(Path.Combine(dir, name), content, ct);

    /// <summary>
    /// Build the flag-driven override sheet. Pointer adds a cursor rule (v4 buttons default to
    /// <c>cursor-default</c>). RTL is a DOM concern (the app sets <c>dir="rtl"</c>) so it contributes
    /// no CSS here — the skins already use logical properties and <c>:dir()</c>.
    /// </summary>
    private static string BuildOptionsCss(TailwindOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Marker);
        sb.AppendLine("/* Flag-driven overrides written by 'blaizio init'. */");

        var wrote = false;
        if (options.Pointer)
        {
            sb.AppendLine("@layer base {");
            sb.AppendLine("  button:not(:disabled),");
            sb.AppendLine("  [role=\"button\"]:not(:disabled) { cursor: pointer; }");
            sb.AppendLine("}");
            wrote = true;
        }

        return wrote ? sb.ToString() : string.Empty;
    }

    private static string BuildInput(IReadOnlyList<string> required)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Marker);
        sb.AppendLine("/* Tailwind v4 input for Blaizio. Compile with the Tailwind CLI, e.g.:");
        sb.AppendLine("   tailwindcss -i Styles/app.css -o wwwroot/app.css --watch");
        sb.AppendLine("   Put `.style-<skin>` (and optionally `.dark`) on <html> to activate the look. */");
        foreach (var line in required)
            sb.AppendLine(line);
        return sb.ToString();
    }

    /// <summary>
    /// Sync a user-owned input file with the managed directives — owning their content AND their
    /// position: every managed line (current, stale, or misplaced, including a hand-written mirror
    /// of the same imports) is removed from wherever it sits and the required set is reinserted
    /// right after the file's last <c>@import</c>. Idempotent; everything the user authored stays.
    /// </summary>
    private static async Task SyncInput(
        string inputAbs,
        IReadOnlyList<string> required,
        CancellationToken ct)
    {
        var original = await File.ReadAllTextAsync(inputAbs, ct);
        var lines = original.Split('\n').ToList();

        bool Ours(string line)
        {
            if (line.Contains(Marker, StringComparison.Ordinal))
                return true;
            var trimmed = line.TrimStart();
            if ((trimmed.StartsWith("@import", StringComparison.Ordinal) || trimmed.StartsWith("@source", StringComparison.Ordinal))
                && line.Contains($"{ManagedDir}/", StringComparison.Ordinal))
                return true;
            // Directives that don't mention blaizio/ (the component @source globs) match exactly.
            return required.Contains(line.Trim());
        }

        lines.RemoveAll(Ours);

        var text = string.Join('\n', lines);
        var toInsert = required.Where(line => !text.Contains(line, StringComparison.Ordinal)).ToArray();
        if (toInsert.Length > 0)
            lines.InsertRange(InsertionIndex(lines), [$"{Marker} (added)", .. toInsert]);

        var updated = string.Join('\n', lines);
        if (!string.Equals(updated, original, StringComparison.Ordinal))
            await File.WriteAllTextAsync(inputAbs, updated, ct);
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

    /// <summary>Import prefix from the input file's directory to a managed asset dir (POSIX, ./-anchored).</summary>
    private static string RelativePrefix(string fromDirAbs, string toDirAbs)
    {
        var rel = ToPosix(Path.GetRelativePath(fromDirAbs, toDirAbs));
        return rel.StartsWith('.') ? rel : $"./{rel}";
    }

    private static string ToPosix(string path) => path.Replace('\\', '/');
}
