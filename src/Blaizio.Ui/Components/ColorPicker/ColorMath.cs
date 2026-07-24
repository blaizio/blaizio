using System.Globalization;

namespace Blaizio.Ui;

/// <summary>
/// Color conversions and string round-tripping for the <see cref="BzColorPicker"/> family. The
/// picker's internal model is HSVA (hue 0-360, saturation/value/alpha 0-1) - the natural space of
/// the area-plus-sliders UI - and this converts to and from the CSS-facing formats.
/// </summary>
public static class ColorMath
{
    /// <summary>HSV to RGB bytes. Hue in degrees (wrapped), saturation and value in <c>[0, 1]</c>.</summary>
    public static (byte R, byte G, byte B) HsvToRgb(double h, double s, double v)
    {
        h = Wrap(h);
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);

        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (r, g, b) = (h / 60) switch
        {
            < 1 => (c, x, 0d),
            < 2 => (x, c, 0d),
            < 3 => (0d, c, x),
            < 4 => (0d, x, c),
            < 5 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return (Byte(r + m), Byte(g + m), Byte(b + m));
    }

    /// <summary>RGB bytes to HSV. A grey (s = 0) reports hue 0.</summary>
    public static (double H, double S, double V) RgbToHsv(byte r, byte g, byte b)
    {
        var (rf, gf, bf) = (r / 255.0, g / 255.0, b / 255.0);
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;

        var h = delta == 0 ? 0
            : max == rf ? 60 * ((gf - bf) / delta % 6)
            : max == gf ? 60 * ((bf - rf) / delta + 2)
            : 60 * ((rf - gf) / delta + 4);
        return (Wrap(h), max == 0 ? 0 : delta / max, max);
    }

    /// <summary>
    /// Parse a CSS color string into HSVA. Accepts <c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c>,
    /// <c>#rrggbbaa</c>, and <c>rgb()</c> / <c>hsl()</c> in both the comma and space syntaxes
    /// (alpha via a fourth component or <c>/ a</c>, plain or percent).
    /// </summary>
    public static bool TryParse(string? text, out double h, out double s, out double v, out double a)
    {
        (h, s, v, a) = (0, 0, 0, 1);
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();

        if (text.StartsWith('#')) return TryParseHex(text, ref h, ref s, ref v, ref a);
        if (StripFunction(text, "rgb") is { } rgb) return TryParseRgb(rgb, ref h, ref s, ref v, ref a);
        if (StripFunction(text, "hsl") is { } hsl) return TryParseHsl(hsl, ref h, ref s, ref v, ref a);
        return false;
    }

    /// <summary>Serialize HSVA in the given <paramref name="format"/>. Alpha is omitted when 1.</summary>
    public static string Format(double h, double s, double v, double a, ColorFormat format)
    {
        a = Math.Clamp(a, 0, 1);
        var (r, g, b) = HsvToRgb(h, s, v);
        return format switch
        {
            ColorFormat.Rgb => a < 1
                ? $"rgb({r} {g} {b} / {Num(a)})"
                : $"rgb({r} {g} {b})",
            ColorFormat.Hsl => FormatHsl(h, s, v, a),
            _ => a < 1
                ? $"#{r:x2}{g:x2}{b:x2}{Byte(a):x2}"
                : $"#{r:x2}{g:x2}{b:x2}",
        };
    }

    /// <summary>True when two color strings parse to the same 8-bit RGBA - the swatch-selected test.</summary>
    public static bool AreEqual(string? left, string? right)
    {
        if (!TryParse(left, out var h1, out var s1, out var v1, out var a1)) return false;
        if (!TryParse(right, out var h2, out var s2, out var v2, out var a2)) return false;
        return HsvToRgb(h1, s1, v1) == HsvToRgb(h2, s2, v2) && Byte(a1) == Byte(a2);
    }

    private static string FormatHsl(double h, double s, double v, double a)
    {
        // HSV to HSL: same hue; lightness sits mid-cone, saturation re-derived around it.
        var l = v * (1 - s / 2);
        var sl = l is 0 or 1 ? 0 : (v - l) / Math.Min(l, 1 - l);
        var head = $"hsl({Num(Wrap(h))} {Num(sl * 100)}% {Num(l * 100)}%";
        return a < 1 ? $"{head} / {Num(a)})" : $"{head})";
    }

    private static bool TryParseHex(string text, ref double h, ref double s, ref double v, ref double a)
    {
        var hex = text[1..];
        if (hex.Length is not (3 or 4 or 6 or 8)) return false;
        if (hex.Length <= 4) // #rgb / #rgba - double each digit
            hex = string.Concat(hex.Select(c => $"{c}{c}"));

        // ulong: an 8-digit value (#rrggbbaa) overflows int's hex range.
        if (!ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)) return false;
        byte Channel(int index) => byte.Parse(hex.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        (h, s, v) = RgbToHsv(Channel(0), Channel(1), Channel(2));
        a = hex.Length == 8 ? Channel(3) / 255.0 : 1;
        return true;
    }

    private static bool TryParseRgb(string body, ref double h, ref double s, ref double v, ref double a)
    {
        var parts = SplitComponents(body);
        if (parts.Length is not (3 or 4)) return false;

        byte Channel(string part) =>
            Byte(part.EndsWith('%') ? Number(part[..^1]) / 100 : Number(part) / 255);

        (h, s, v) = RgbToHsv(Channel(parts[0]), Channel(parts[1]), Channel(parts[2]));
        a = parts.Length == 4 ? ParseAlpha(parts[3]) : 1;
        return true;
    }

    private static bool TryParseHsl(string body, ref double h, ref double s, ref double v, ref double a)
    {
        var parts = SplitComponents(body);
        if (parts.Length is not (3 or 4)) return false;

        var hue = Wrap(Number(parts[0].TrimEnd("deg".ToCharArray())));
        var sl = Math.Clamp(Number(parts[1].TrimEnd('%')) / 100, 0, 1);
        var l = Math.Clamp(Number(parts[2].TrimEnd('%')) / 100, 0, 1);

        // HSL to HSV: value is the cone top over this lightness; saturation re-derived around it.
        v = l + sl * Math.Min(l, 1 - l);
        s = v == 0 ? 0 : 2 * (1 - l / v);
        h = hue;
        a = parts.Length == 4 ? ParseAlpha(parts[3]) : 1;
        return true;
    }

    /// <summary>The inner of <c>name(...)</c> / <c>namea(...)</c> (rgba/hsla), or null when not that function.</summary>
    private static string? StripFunction(string text, string name)
    {
        if (!text.EndsWith(')')) return null;
        var open = text.IndexOf('(');
        if (open < 0) return null;
        var fn = text[..open].Trim().ToLowerInvariant();
        return fn == name || fn == name + "a" ? text[(open + 1)..^1] : null;
    }

    // "r, g, b, a" and "r g b / a" both flatten to bare components.
    private static string[] SplitComponents(string body) =>
        body.Replace("/", " ").Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static double ParseAlpha(string part) =>
        Math.Clamp(part.EndsWith('%') ? Number(part[..^1]) / 100 : Number(part), 0, 1);

    private static double Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static byte Byte(double fraction) => (byte)Math.Clamp(Math.Round(fraction * 255), 0, 255);

    private static double Wrap(double hue)
    {
        hue %= 360;
        return hue < 0 ? hue + 360 : hue;
    }

    private static string Num(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}
