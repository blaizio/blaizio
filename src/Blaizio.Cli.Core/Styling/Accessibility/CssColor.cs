using System.Globalization;
using System.Text.RegularExpressions;

namespace Blaizio.Cli.Core.Styling.Accessibility;

/// <summary>
/// A parsed CSS color as linear-light sRGB components (unclamped until conversion), plus the
/// color math the contrast audit needs: WCAG relative luminance / contrast ratio and the OKLab
/// round-trip used to reproduce the button's dark-mode <c>color-mix</c> fill. Parses the color
/// syntaxes a tokens file realistically carries: <c>oklch()</c>, <c>oklab()</c>, <c>#hex</c>,
/// <c>rgb()/rgba()</c> and <c>hsl()/hsla()</c>. <c>white</c>/<c>black</c> keywords too - anything
/// else (gradients, var chains the caller didn't resolve) returns null and the caller reports the
/// token as unchecked rather than guessing.
/// </summary>
public readonly partial struct CssColor
{
    private CssColor(double r, double g, double b) => (R, G, B) = (r, g, b);

    /// <summary>Gamma-encoded sRGB components, clamped to [0, 1].</summary>
    public double R { get; }

    /// <summary>Gamma-encoded sRGB green.</summary>
    public double G { get; }

    /// <summary>Gamma-encoded sRGB blue.</summary>
    public double B { get; }

    /// <summary>Parse a CSS color value, or null when the syntax isn't a supported literal.</summary>
    public static CssColor? Parse(string value)
    {
        value = value.Trim();

        if (string.Equals(value, "white", StringComparison.OrdinalIgnoreCase))
            return new CssColor(1, 1, 1);
        if (string.Equals(value, "black", StringComparison.OrdinalIgnoreCase))
            return new CssColor(0, 0, 0);

        if (value.StartsWith('#'))
            return ParseHex(value);

        var fn = FunctionRegex().Match(value);
        if (!fn.Success)
            return null;
        var name = fn.Groups[1].Value.ToLowerInvariant();
        var args = ParseArgs(fn.Groups[2].Value);
        if (args.Count < 3)
            return null;

        return name switch
        {
            "oklch" => FromOklch(args[0].Scaled(1), args[1].Scaled(0.4), args[2].Value),
            "oklab" => FromOklab(args[0].Scaled(1), args[1].Scaled(0.4), args[2].Scaled(0.4)),
            "rgb" or "rgba" => new CssColor(
                Clamp01(args[0].Scaled(255) / 255), Clamp01(args[1].Scaled(255) / 255), Clamp01(args[2].Scaled(255) / 255)),
            "hsl" or "hsla" => FromHsl(args[0].Value, args[1].Scaled(1), args[2].Scaled(1)),
            _ => null,
        };
    }

    /// <summary>WCAG 2.x relative luminance.</summary>
    public double Luminance()
    {
        static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return 0.2126 * Lin(R) + 0.7152 * Lin(G) + 0.0722 * Lin(B);
    }

    /// <summary>WCAG contrast ratio between two colors (1..21).</summary>
    public static double Contrast(CssColor a, CssColor b)
    {
        var (l1, l2) = (a.Luminance(), b.Luminance());
        var (hi, lo) = (Math.Max(l1, l2), Math.Min(l1, l2));
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>
    /// <c>color-mix(in oklab, this p%, black)</c> - the formula behind the button's dark-mode
    /// fill. Mixing with black in OKLab scales every component by <paramref name="p"/>.
    /// </summary>
    public CssColor MixBlackOklab(double p)
    {
        var (l, a, b) = ToOklab();
        return FromOklab(l * p, a * p, b * p);
    }

    private (double L, double A, double B) ToOklab()
    {
        static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var (r, g, b) = (Lin(R), Lin(G), Lin(B));
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return (
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    private static CssColor FromOklch(double l, double c, double hDeg)
    {
        var h = hDeg * Math.PI / 180.0;
        return FromOklab(l, c * Math.Cos(h), c * Math.Sin(h));
    }

    private static CssColor FromOklab(double l, double a, double b)
    {
        var l_ = l + 0.3963377774 * a + 0.2158037573 * b;
        var m_ = l - 0.1055613458 * a - 0.0638541728 * b;
        var s_ = l - 0.0894841775 * a - 1.2914855480 * b;
        var (l3, m3, s3) = (l_ * l_ * l_, m_ * m_ * m_, s_ * s_ * s_);
        var r = +4.0767416621 * l3 - 3.3077115913 * m3 + 0.2309699292 * s3;
        var g = -1.2684380046 * l3 + 2.6097574011 * m3 - 0.3413193965 * s3;
        var bb = -0.0041960863 * l3 - 0.7034186147 * m3 + 1.7076147010 * s3;
        static double Gam(double x)
        {
            x = Clamp01(x);
            return x <= 0.0031308 ? 12.92 * x : 1.055 * Math.Pow(x, 1 / 2.4) - 0.055;
        }
        return new CssColor(Gam(r), Gam(g), Gam(bb));
    }

    private static CssColor FromHsl(double hDeg, double sPct, double lPct)
    {
        var (h, s, l) = (((hDeg % 360) + 360) % 360 / 360.0, Clamp01(sPct), Clamp01(lPct));
        if (s == 0)
            return new CssColor(l, l, l);
        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        static double Hue(double p, double q, double t)
        {
            t = ((t % 1) + 1) % 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
        return new CssColor(Hue(p, q, h + 1.0 / 3), Hue(p, q, h), Hue(p, q, h - 1.0 / 3));
    }

    private static CssColor? ParseHex(string value)
    {
        var hex = value[1..];
        if (hex.Length is 3 or 4)
            hex = string.Concat(hex.Select(c => $"{c}{c}"));
        if (hex.Length is not (6 or 8))
            return null;
        if (!int.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !int.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !int.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            return null;
        return new CssColor(r / 255.0, g / 255.0, b / 255.0);
    }

    /// <summary>A numeric component that may carry <c>%</c> or <c>deg</c>.</summary>
    private readonly record struct Component(double Value, bool Percent)
    {
        /// <summary>The value with <c>%</c> resolved against what 100% means for its slot.</summary>
        public double Scaled(double hundredPercent) => Percent ? Value / 100.0 * hundredPercent : Value;
    }

    private static List<Component> ParseArgs(string body)
    {
        // "0.55 0.22 304 / 80%" or "255, 0, 0" - split on whitespace/commas, stop at the alpha slash
        // (alpha never changes a contrast verdict enough to matter for token values; tokens are opaque).
        var slash = body.IndexOf('/');
        if (slash >= 0)
            body = body[..slash];
        var result = new List<Component>();
        foreach (var raw in body.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            var percent = token.EndsWith('%');
            token = token.TrimEnd('%');
            if (token.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
                token = token[..^3];
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return [];
            result.Add(new Component(value, percent));
        }
        return result;
    }

    private static double Clamp01(double v) => Math.Min(1, Math.Max(0, v));

    [GeneratedRegex(@"^([a-zA-Z]+)\(([^)]*)\)$")]
    private static partial Regex FunctionRegex();
}
