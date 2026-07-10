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
    private const string StylesDir = "Styles";
    private const string ManagedDir = "blaizio";
    private const string InputName = "app.css";
    private const string Marker = "/* blaizio:managed */";

    /// <summary>Run the setup for <paramref name="projectDir"/>.</summary>
    /// <param name="projectDir">Project root.</param>
    /// <param name="componentOutput">Component output dir (project-relative), scanned by Tailwind.</param>
    /// <param name="skin">Skin to install (without the <c>style-</c> prefix).</param>
    /// <param name="options">Init-time styling toggles (pointer cursor, RTL).</param>
    /// <param name="preset">Color preset to install (without the <c>preset-</c> prefix);
    /// <c>"nova"</c> - the default palette baked into theme.css - writes no preset file.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<TailwindResult> EnsureAsync(
        string projectDir,
        string componentOutput,
        string skin,
        TailwindOptions options = default,
        string preset = "nova",
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

        // @source globs are relative to the input file (in Styles/); point them at the component dir.
        var sourceGlob = ToPosix(Path.GetRelativePath(stylesAbs, Path.Combine(projectDir, componentOutput)));

        var required = new List<string>
        {
            // source(none) disables Tailwind's automatic source detection: without it the scanner
            // walks the whole project — including bin/obj build output, whose binaries crash it
            // ("value that is out of range of code points") — and we list our sources explicitly.
            "@import \"tailwindcss\" source(none);",
            // tw-animate-css is vendored (below) so the Node-free standalone binary can resolve it.
            $"@import \"./{ManagedDir}/animate.css\";",
            $"@import \"./{ManagedDir}/theme.css\";",
        };
        // The preset must follow theme.css (source order breaks the :root vs .preset-* tie) and
        // stay unlayered so it participates in the same cascade as the base tokens.
        if (hasPreset)
            required.Add($"@import \"./{ManagedDir}/{presetFile}\";");
        required.AddRange(
        [
            $"@import \"./{ManagedDir}/base.css\";",
            // The shared skin layer must precede the skin: the skin's scoped rules override it.
            $"@import \"./{ManagedDir}/shared.css\" layer(components);",
            $"@import \"./{ManagedDir}/{skinFile}\" layer(components);",
        ]);
        if (hasOptions)
            required.Add($"@import \"./{ManagedDir}/options.css\";");
        // Copied components (.razor + .cs class builders), plus every other .razor in the project
        // (pages, layouts) so app markup utilities are generated too.
        required.Add($"@source \"{sourceGlob}/**/*.razor\";");
        required.Add($"@source \"{sourceGlob}/**/*.cs\";");
        required.Add("@source \"../**/*.razor\";");

        var inputAbs = Path.Combine(stylesAbs, InputName);
        var created = !File.Exists(inputAbs);

        // Create fresh, or fully regenerate a file we own (marker present) so a skin change doesn't
        // leave a stale import. A user-authored file is only topped up with missing directives.
        var isManaged = created ||
            (await File.ReadAllTextAsync(inputAbs, ct)).StartsWith(Marker, StringComparison.Ordinal);
        if (isManaged)
            await File.WriteAllTextAsync(inputAbs, BuildInput(required), ct);
        else
            await TopUpInput(inputAbs, required, ct);

        return new TailwindResult
        {
            InputPath = ToPosix(Path.Combine(StylesDir, InputName)),
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
        // input. Without an app.css we don't fabricate a full input file (that's init's job).
        var importLine = $"@import \"./{ManagedDir}/fonts.css\";";
        var inputAbs = Path.Combine(stylesAbs, InputName);
        var importWired = false;
        if (File.Exists(inputAbs))
        {
            var existing = await File.ReadAllTextAsync(inputAbs, ct);
            if (!existing.Contains(importLine, StringComparison.Ordinal))
            {
                var input = new StringBuilder(existing);
                if (!existing.EndsWith('\n'))
                    input.AppendLine();
                input.AppendLine($"{Marker} (added)");
                input.AppendLine(importLine);
                await File.WriteAllTextAsync(inputAbs, input.ToString(), ct);
            }
            importWired = true;
        }

        return new FontOverlayResult(true, importWired, ToPosix(Path.Combine(StylesDir, ManagedDir, "fonts.css")));
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

    /// <summary>Append any required directive not already present, preserving the user's file.</summary>
    private static async Task TopUpInput(string inputAbs, IReadOnlyList<string> required, CancellationToken ct)
    {
        var existing = await File.ReadAllTextAsync(inputAbs, ct);
        var missing = required.Where(line => !existing.Contains(line, StringComparison.Ordinal)).ToArray();
        if (missing.Length == 0)
            return;

        var sb = new StringBuilder(existing);
        if (!existing.EndsWith('\n'))
            sb.AppendLine();
        sb.AppendLine($"{Marker} (added)");
        foreach (var line in missing)
            sb.AppendLine(line);
        await File.WriteAllTextAsync(inputAbs, sb.ToString(), ct);
    }

    private static string ToPosix(string path) => path.Replace('\\', '/');
}
