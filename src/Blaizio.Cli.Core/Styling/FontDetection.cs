using System.Text.RegularExpressions;

namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// Detects a font setup the CLI did not write, so a preset's font selection never silently stomps
/// the app's own typography. Everything the CLI writes is marked (<see cref="TailwindSetup.Marker"/>
/// in CSS, <see cref="HostPageSetup.FontLinkMarker"/> on the host link) - anything font-shaped
/// WITHOUT a marker is the user's.
/// </summary>
public static partial class FontDetection
{
    /// <summary>
    /// True when the project carries a user-defined font setup: an edited (unmarked)
    /// <c>Styles/blaizio/fonts.css</c>, an unmarked Google Fonts <c>&lt;link&gt;</c> in the host
    /// page, or an <c>@font-face</c> / <c>--font-heading</c> / root <c>font-family</c> in the
    /// user-authored Tailwind input. <paramref name="reason"/> names what was found.
    /// </summary>
    public static bool UserDefined(string projectDir, string? cssInput, out string reason)
    {
        reason = string.Empty;

        // 1. A legacy v1 fonts.css overlay we didn't write (or one the user edited - the marker is line 1).
        var overlayAbs = Path.Combine(projectDir, TailwindSetup.StylesDir, TailwindSetup.LegacyManagedDir, "fonts.css");
        if (File.Exists(overlayAbs)
            && !File.ReadAllText(overlayAbs).StartsWith(TailwindSetup.Marker, StringComparison.Ordinal))
        {
            reason = "Styles/blaizio/fonts.css exists but wasn't written by the CLI";
            return true;
        }

        // 2. A webfont stylesheet in the host page without our data-blaizio="fonts" marker.
        if (HostPageSetup.FindHostContent(projectDir) is { } host)
        {
            foreach (var line in host.Split('\n'))
            {
                if (line.Contains("fonts.googleapis.com", StringComparison.OrdinalIgnoreCase)
                    && !line.Contains(HostPageSetup.FontLinkMarker, StringComparison.Ordinal))
                {
                    reason = "the host page already loads its own Google Fonts stylesheet";
                    return true;
                }
            }
        }

        // 3. The tokens file declares fonts the CLI can't account for: an @font-face, a
        //    --font-heading value that is neither the built-in default nor a catalog stack the
        //    CLI could have patched in, or a document-level font-family off the catalog. (The v3
        //    tokens file always carries a --font-heading — the scaffold bakes the default — so
        //    mere presence proves nothing; only a hand-written value does.)
        var inputRel = cssInput ?? Path.Combine(TailwindSetup.StylesDir, TailwindSetup.InputName);
        var inputAbs = Path.GetFullPath(Path.Combine(projectDir, inputRel));
        if (File.Exists(inputAbs))
        {
            var text = File.ReadAllText(inputAbs);
            if (!text.StartsWith(TailwindSetup.Marker, StringComparison.Ordinal))
            {
                if (text.Contains("@font-face", StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"{ToPosix(inputRel)} declares its own @font-face";
                    return true;
                }
                var heading = CssBlocks.Declarations(text, ":root")
                    .LastOrDefault(d => d.Name == "--font-heading").Value;
                if (heading is not null && !IsKnownStack(heading))
                {
                    reason = $"{ToPosix(inputRel)} sets --font-heading itself";
                    return true;
                }
                var family = RootFontFamilyRegex().Match(text);
                if (family.Success && !IsKnownStack(family.Groups[1].Value.Trim()))
                {
                    reason = $"{ToPosix(inputRel)} sets a document font-family itself";
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>A stack the CLI could have written: the built-in default or any catalog font's.</summary>
    private static bool IsKnownStack(string value) =>
        string.Equals(value, TailwindSetup.HeadingDefault, StringComparison.Ordinal)
        || FontCatalog.All.Any(f => string.Equals(f.Stack, value, StringComparison.Ordinal));

    // A font-family declared on html/body/:root - the document-level font, not a component's.
    // Captures the value so a stack the CLI itself patched in (a catalog font) isn't flagged.
    [GeneratedRegex(@"(?:^|[}\s])(?:html|body|:root)[^{}]*\{[^}]*font-family\s*:\s*([^;}]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RootFontFamilyRegex();

    private static string ToPosix(string path) => path.Replace('\\', '/');
}
