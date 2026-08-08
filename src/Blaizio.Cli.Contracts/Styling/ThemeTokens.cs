using System.Globalization;

namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// An oklch color with the math the theme composer needs: WCAG contrast (so the docs picker can
/// grade AA/AAA and the foreground derivation can guarantee it) and CSS round-tripping. The
/// oklab-to-sRGB matrices are the CSS Color 4 reference ones - the same numbers the browser uses.
/// </summary>
public readonly record struct OklchColor(double L, double C, double H)
{
    /// <summary>Format as a CSS <c>oklch()</c> function.</summary>
    public string ToCss() => string.Create(CultureInfo.InvariantCulture,
        $"oklch({Math.Round(L, 4)} {Math.Round(C, 4)} {Math.Round(H, 1)})");

    /// <summary>
    /// Parse any of the formats the docs color picker can emit - <c>oklch()</c>, <c>#hex</c>,
    /// <c>rgb()/rgba()</c> - into oklch. False on anything else.
    /// </summary>
    public static bool TryParseAny(string? css, out OklchColor color)
    {
        color = default;
        if (css is null) return false;
        css = css.Trim();
        if (TryParse(css, out color)) return true;

        if (css.StartsWith('#'))
        {
            var hex = css[1..];
            if (hex.Length is 3 or 4) hex = string.Concat(hex.Select(ch => $"{ch}{ch}"));
            if (hex.Length is not (6 or 8)) return false;
            if (!int.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
                || !int.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
                || !int.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return false;
            color = FromSrgb(r / 255.0, g / 255.0, b / 255.0);
            return true;
        }

        if (css.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open = css.IndexOf('(');
            var close = css.IndexOf(')');
            if (open < 0 || close < open) return false;
            var parts = css[(open + 1)..close]
                .Split([' ', ',', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 3) return false;
            var channels = new double[3];
            for (var i = 0; i < 3; i++)
            {
                var s = parts[i];
                var percent = s.EndsWith('%');
                if (percent) s = s[..^1];
                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return false;
                channels[i] = percent ? v / 100 : v / 255;
            }
            color = FromSrgb(channels[0], channels[1], channels[2]);
            return true;
        }

        return false;
    }

    /// <summary>Convert gamma-encoded sRGB channels (0..1) to oklch.</summary>
    public static OklchColor FromSrgb(double r, double g, double b)
    {
        static double Lin(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        (r, g, b) = (Lin(r), Lin(g), Lin(b));
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        var okL = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
        var okA = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
        var okB = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;
        var c = Math.Sqrt(okA * okA + okB * okB);
        var h = Math.Atan2(okB, okA) * 180 / Math.PI;
        if (h < 0) h += 360;
        return new OklchColor(okL, c, h);
    }

    /// <summary>Parse an <c>oklch(L C H ...)</c> string (percent or unit L). False on anything else.</summary>
    public static bool TryParse(string? css, out OklchColor color)
    {
        color = default;
        if (css is null) return false;
        var open = css.IndexOf('(');
        var close = css.IndexOf(')');
        if (!css.TrimStart().StartsWith("oklch", StringComparison.OrdinalIgnoreCase)
            || open < 0 || close < open)
            return false;
        var parts = css[(open + 1)..close]
            .Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3) return false;

        if (!TryComponent(parts[0], out var l) || !TryComponent(parts[1], out var c)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
            return false;
        color = new OklchColor(l, c, h);
        return true;

        static bool TryComponent(string s, out double value)
        {
            var percent = s.EndsWith('%');
            if (percent) s = s[..^1];
            var ok = double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            if (percent) value /= 100;
            return ok;
        }
    }

    /// <summary>WCAG relative luminance (linear sRGB, per-channel gamut clipped like a browser).</summary>
    public double Luminance()
    {
        var hr = H * Math.PI / 180;
        var (l, a, b) = (L, C * Math.Cos(hr), C * Math.Sin(hr));
        var l_ = Cube(l + 0.3963377774 * a + 0.2158037573 * b);
        var m_ = Cube(l - 0.1055613458 * a - 0.0638541728 * b);
        var s_ = Cube(l - 0.0894841775 * a - 1.291485548 * b);
        var r = Clamp01(4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_);
        var g = Clamp01(-1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_);
        var bl = Clamp01(-0.0041960863 * l_ - 0.7034186147 * m_ + 1.707614701 * s_);
        return 0.2126 * r + 0.7152 * g + 0.0722 * bl;

        static double Cube(double v) => v * v * v;
        static double Clamp01(double v) => Math.Clamp(v, 0, 1);
    }

    /// <summary>WCAG contrast ratio between two colors (1..21).</summary>
    public static double Contrast(OklchColor a, OklchColor b)
    {
        var (la, lb) = (a.Luminance(), b.Luminance());
        var (hi, lo) = la > lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }
}

/// <summary>One edited theme token: which token, which mode, and the picked color.</summary>
public sealed record TokenOverride(string Token, bool Dark, OklchColor Color);

/// <summary>
/// The theme composer's directly-editable token set and the derivation that keeps every edit
/// WCAG-legal. The token list is APPEND-ONLY: preset codes encode indices into it.
/// </summary>
public static class ThemeTokens
{
    /// <summary>Canonical editable tokens, in code order. Append-only.</summary>
    public static readonly string[] All =
    [
        "primary", "background", "foreground", "accent", "secondary", "muted",
        "destructive", "success", "warning", "info", "border",
        "chart-1", "chart-2", "chart-3", "chart-4", "chart-5",
    ];

    /// <summary>Tokens whose surface carries text - editing one also derives its
    /// <c>-foreground</c> partner so the pairing always clears AA.</summary>
    private static readonly HashSet<string> HasForeground =
        ["primary", "accent", "secondary", "muted", "destructive", "success", "warning", "info"];

    /// <summary>Whether editing <paramref name="token"/> derives a <c>-foreground</c> partner.</summary>
    public static bool PairsWithForeground(string token) => HasForeground.Contains(token);

    /// <summary>
    /// The AA-guaranteed label for a surface: near-white or near-black (hue-tinted), whichever
    /// contrasts more. Mid-lightness surfaces where even the tinted extremes fall short of 4.5
    /// get the pure extreme instead - white/black bottom out at 4.58:1 against the worst possible
    /// surface, so the pairing always clears AA.
    /// </summary>
    public static OklchColor DeriveForeground(OklchColor surface)
    {
        var light = new OklchColor(0.985, Math.Min(surface.C, 0.006), surface.H);
        var dark = new OklchColor(0.18, Math.Min(surface.C, 0.03), surface.H);
        var best = OklchColor.Contrast(surface, light) >= OklchColor.Contrast(surface, dark) ? light : dark;
        if (OklchColor.Contrast(surface, best) >= 4.5) return best;

        var white = new OklchColor(1, 0, surface.H);
        var black = new OklchColor(0, 0, surface.H);
        return OklchColor.Contrast(surface, white) >= OklchColor.Contrast(surface, black) ? white : black;
    }

    /// <summary>
    /// Expand edits into the declarations they imply, per mode. Editing a text-bearing surface
    /// also derives its foreground; primary additionally drives the ring and sidebar mirrors,
    /// exactly like the authored palettes do.
    /// </summary>
    public static (List<(string Name, string Value)> Light, List<(string Name, string Value)> Dark)
        Expand(IEnumerable<TokenOverride> overrides)
    {
        var light = new List<(string, string)>();
        var dark = new List<(string, string)>();
        foreach (var o in overrides)
        {
            var into = o.Dark ? dark : light;
            var css = o.Color.ToCss();
            into.Add(($"--{o.Token}", css));
            if (HasForeground.Contains(o.Token))
                into.Add(($"--{o.Token}-foreground", DeriveForeground(o.Color).ToCss()));
            if (o.Token == "primary")
            {
                into.Add(("--ring", css));
                into.Add(("--sidebar-primary", css));
                into.Add(("--sidebar-primary-foreground", DeriveForeground(o.Color).ToCss()));
            }
        }
        return (light, dark);
    }

    /// <summary>
    /// The override stylesheet for a set of edits: per-mode <c>:root</c> blocks whose custom
    /// properties are <c>!important</c> so they outrank any <c>.preset-*.dark</c> pair regardless
    /// of source order. This is what the docs composer injects live.
    /// </summary>
    public static string BuildCss(IEnumerable<TokenOverride> overrides)
    {
        var (light, dark) = Expand(overrides);
        var sb = new System.Text.StringBuilder();
        if (light.Count > 0)
            sb.Append(":root:not(.dark) {\n  ")
              .AppendJoin("\n  ", light.Select(d => $"{d.Name}: {d.Value} !important;"))
              .Append("\n}\n");
        if (dark.Count > 0)
            sb.Append(":root.dark {\n  ")
              .AppendJoin("\n  ", dark.Select(d => $"{d.Name}: {d.Value} !important;"))
              .Append("\n}\n");
        return sb.ToString();
    }

    /// <summary>The same expansion as a tokens-file patch spec - the CLI's apply leg, which
    /// patches decoded overrides into a project's <c>:root</c>/<c>.dark</c> blocks.</summary>
    public static Registry.CssVarsSpec ToCssVars(IEnumerable<TokenOverride> overrides)
    {
        var (light, dark) = Expand(overrides);
        return new Registry.CssVarsSpec
        {
            Light = light.ToDictionary(d => d.Name, d => d.Value),
            Dark = dark.ToDictionary(d => d.Name, d => d.Value),
        };
    }
}
