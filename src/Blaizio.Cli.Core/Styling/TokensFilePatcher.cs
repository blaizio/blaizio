using Blaizio.Cli.Core.Registry;

namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// The surgical tokens-file patches (the CLI never rewrites an existing tokens file - it only
/// changes the declarations a selection targets): fonts, chart/radius overlays, registry-theme
/// <c>cssVars</c>, and the full preset re-theme. <see cref="TailwindSetup"/> stays the public
/// face; commands never construct this.
/// </summary>
internal sealed class TokensFilePatcher(ICssAssetProvider assets)
{
    /// <summary>See <see cref="TailwindSetup.EnsureFontsAsync"/> - this is its implementation.</summary>
    public static async Task<TokenPatchResult> EnsureFontsAsync(
        string projectDir, string heading, string font, string? cssInput, bool dryRun, CancellationToken ct)
    {
        var headingStack = FontStacks.Stack(heading);
        var fontStack = FontStacks.Stack(font);

        // Both default/unknown: nothing was ever selected, nothing to reset.
        if (headingStack is null && fontStack is null)
            return new TokenPatchResult(false, false, null);

        var (inputRel, inputAbs) = TokensFileScaffolder.InputPath(projectDir, cssInput);
        if (!File.Exists(inputAbs))
            return new TokenPatchResult(true, false, null);

        var css = await File.ReadAllTextAsync(inputAbs, ct);
        css = CssBlocks.SetDeclaration(css, ":root", "--font-heading", headingStack ?? TailwindSetup.HeadingDefault);
        css = fontStack is null
            ? CssBlocks.RemoveNestedRule(css, "@layer base", "html")
            : CssBlocks.SetNestedRule(css, "@layer base", "html", $"html {{ font-family: {fontStack}; }}");
        if (!dryRun)
            await File.WriteAllTextAsync(inputAbs, css, ct);
        return new TokenPatchResult(true, true, TokensFileScaffolder.ToPosix(inputRel));
    }

    /// <summary>See <see cref="TailwindSetup.EnsureThemeTokensAsync"/> - this is its implementation.</summary>
    public static async Task<TokenPatchResult> EnsureThemeTokensAsync(
        string projectDir, string chart, string radius, string? cssInput, bool dryRun, CancellationToken ct)
    {
        if (TokenOverlays.Radius(radius) is null && TokenOverlays.Chart(chart) is null)
            return new TokenPatchResult(false, false, null);

        var (inputRel, inputAbs) = TokensFileScaffolder.InputPath(projectDir, cssInput);
        if (!File.Exists(inputAbs))
            return new TokenPatchResult(true, false, null);

        var css = WithTokenOverrides(await File.ReadAllTextAsync(inputAbs, ct), chart, radius);
        if (!dryRun)
            await File.WriteAllTextAsync(inputAbs, css, ct);
        return new TokenPatchResult(true, true, TokensFileScaffolder.ToPosix(inputRel));
    }

    /// <summary>See <see cref="TailwindSetup.EnsureCssVarsAsync"/> - this is its implementation.</summary>
    public static async Task<TokenPatchResult> EnsureCssVarsAsync(
        string projectDir, CssVarsSpec vars, string? cssInput, CancellationToken ct)
    {
        if (vars.IsEmpty)
            return new TokenPatchResult(false, false, null);

        var (inputRel, inputAbs) = TokensFileScaffolder.InputPath(projectDir, cssInput);
        if (!File.Exists(inputAbs))
            return new TokenPatchResult(true, false, null);

        var css = ApplyCssVars(await File.ReadAllTextAsync(inputAbs, ct), vars);
        await File.WriteAllTextAsync(inputAbs, css, ct);
        return new TokenPatchResult(true, true, TokensFileScaffolder.ToPosix(inputRel));
    }

    /// <summary>Apply <paramref name="vars"/> to a tokens file's text: <c>light</c> values into
    /// <c>:root</c>, <c>dark</c> into <c>.dark</c>; a targeted block the file lacks is appended
    /// first, so a minimal tokens file still takes the full theme.</summary>
    public static string ApplyCssVars(string css, CssVarsSpec vars)
    {
        if (vars.Light.Count > 0 && CssBlocks.FindBlock(css, ":root") is null)
            css = $"{css.TrimEnd()}\n\n:root {{\n}}\n";
        if (vars.Dark.Count > 0 && CssBlocks.FindBlock(css, ".dark") is null)
            css = $"{css.TrimEnd()}\n\n.dark {{\n}}\n";

        foreach (var (name, value) in vars.Light)
            css = CssBlocks.SetDeclaration(css, ":root", CanonicalVar(name), value);
        foreach (var (name, value) in vars.Dark)
            css = CssBlocks.SetDeclaration(css, ".dark", CanonicalVar(name), value);
        return css;
    }

    /// <summary>Token name with its <c>--</c> prefix, whether or not the author wrote one.</summary>
    private static string CanonicalVar(string name) =>
        name.StartsWith("--", StringComparison.Ordinal) ? name : $"--{name}";

    /// <summary>See <see cref="TailwindSetup.ApplyPresetAsync"/> - this is its implementation.</summary>
    public async Task<TokenPatchResult> ApplyPresetAsync(
        string projectDir, string preset, string? cssInput, string chart, string radius, bool dryRun, CancellationToken ct)
    {
        var (inputRel, inputAbs) = TokensFileScaffolder.InputPath(projectDir, cssInput);
        if (!File.Exists(inputAbs))
            return new TokenPatchResult(true, false, null);

        var merged = new TokensFileScaffolder(assets).ComposeTokenBlock(preset, chart, radius, pointer: false);
        var css = await File.ReadAllTextAsync(inputAbs, ct);
        foreach (var (name, value) in CssBlocks.Declarations(merged, ":root"))
        {
            if (name is "--font-heading")
                continue;
            css = CssBlocks.SetDeclaration(css, ":root", name, value);
        }
        foreach (var (name, value) in CssBlocks.Declarations(merged, ".dark"))
            css = CssBlocks.SetDeclaration(css, ".dark", name, value);

        if (!dryRun)
            await File.WriteAllTextAsync(inputAbs, css, ct);
        return new TokenPatchResult(true, true, TokensFileScaffolder.ToPosix(inputRel));
    }

    /// <summary>
    /// Bake a chart/radius selection into the tokens file's <c>:root</c> by patching the matching
    /// declarations. <c>"default"</c> selections leave the values untouched — the theme's own
    /// values ARE the default.
    /// </summary>
    public static string WithTokenOverrides(string css, string chart, string radius)
    {
        if (TokenOverlays.Radius(radius) is { } radiusValue)
            css = CssBlocks.SetDeclaration(css, ":root", "--radius", radiusValue);
        if (TokenOverlays.Chart(chart) is { } chartValues)
            for (var i = 0; i < chartValues.Count; i++)
                css = CssBlocks.SetDeclaration(css, ":root", $"--chart-{i + 1}", chartValues[i]);
        return css;
    }
}
