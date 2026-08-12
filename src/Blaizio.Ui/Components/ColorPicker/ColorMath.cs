using System.Globalization;

namespace Blaizio.Ui;

/// <summary>
/// Color conversions and string round-tripping for the <see cref="BzColorPicker"/> family. The
/// picker's internal model is HSVA (hue 0-360, saturation/value/alpha 0-1) - the natural space of
/// the area-plus-sliders UI - and this converts to and from every supported CSS-facing format,
/// including the perceptual OKLab/OKLCH pair (Björn Ottosson's matrices over linear sRGB).
/// </summary>
public static class ColorMath
{
    // ---- HSV <-> RGB ----------------------------------------------------------------------------

    /// <summary>HSV to RGB bytes. Hue in degrees (wrapped), saturation and value in <c>[0, 1]</c>.</summary>
    public static (byte R, byte G, byte B) HsvToRgb(double h, double s, double v)
    {
        var (r, g, b) = HsvToRgbF(h, s, v);
        return (Byte(r), Byte(g), Byte(b));
    }

    /// <summary>HSV to sRGB in <c>[0, 1]</c>, unrounded - what the perceptual formats serialize from.</summary>
    public static (double R, double G, double B) HsvToRgbF(double h, double s, double v)
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
        return (r + m, g + m, b + m);
    }

    /// <summary>RGB bytes to HSV. A grey (s = 0) reports hue 0.</summary>
    public static (double H, double S, double V) RgbToHsv(byte r, byte g, byte b) =>
        RgbToHsv(r / 255.0, g / 255.0, b / 255.0);

    private static (double H, double S, double V) RgbToHsv(double rf, double gf, double bf)
    {
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;

        var h = delta == 0 ? 0
            : max == rf ? 60 * ((gf - bf) / delta % 6)
            : max == gf ? 60 * ((bf - rf) / delta + 2)
            : 60 * ((rf - gf) / delta + 4);
        return (Wrap(h), max == 0 ? 0 : delta / max, max);
    }

    // ---- parsing --------------------------------------------------------------------------------

    /// <summary>
    /// Parse a color string into HSVA, auto-detecting the format. Accepts hex with or without the
    /// leading <c>#</c>, every function form the picker serializes - <c>rgb()</c>/<c>rgba()</c>,
    /// <c>hsl()</c>/<c>hsla()</c>, <c>hwb()</c>, <c>cmyk()</c>/<c>device-cmyk()</c>,
    /// <c>oklch()</c>, <c>oklab()</c> - in both comma and space syntaxes, and a bare
    /// <c>r g b</c> / <c>r, g, b(, a)</c> number list.
    /// </summary>
    public static bool TryParse(string? text, out double h, out double s, out double v, out double a)
    {
        (h, s, v, a) = (0, 0, 0, 1);
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();

        if (text.StartsWith('#')) return TryParseHex(text[1..], ref h, ref s, ref v, ref a);

        var open = text.IndexOf('(');
        if (open > 0 && text.EndsWith(')'))
        {
            var fn = text[..open].Trim().ToLowerInvariant();
            var body = text[(open + 1)..^1];
            var parts = SplitComponents(body);
            return fn switch
            {
                "rgb" or "rgba" => TryParseRgb(parts, ref h, ref s, ref v, ref a),
                "hsl" or "hsla" => TryParseHsl(parts, ref h, ref s, ref v, ref a),
                "hsb" or "hsv" => TryParseHsb(parts, ref h, ref s, ref v, ref a),
                "hwb" => TryParseHwb(parts, ref h, ref s, ref v, ref a),
                "cmyk" or "device-cmyk" => TryParseCmyk(parts, ref h, ref s, ref v, ref a),
                "oklch" => TryParseOklch(parts, ref h, ref s, ref v, ref a),
                "oklab" => TryParseOklab(parts, ref h, ref s, ref v, ref a),
                _ => false,
            };
        }

        // Bare hex ("ff8800", "f80") - hex digits only, at a hex length.
        if (text.Length is 3 or 4 or 6 or 8 && text.All(Uri.IsHexDigit))
            return TryParseHex(text, ref h, ref s, ref v, ref a);

        // Bare number list ("255 128 0", "255, 128, 0, 0.5") - read as rgb.
        var bare = SplitComponents(text);
        return bare.Length is 3 or 4 && bare.All(p => double.TryParse(
                p.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            && TryParseRgb(bare, ref h, ref s, ref v, ref a);
    }

    private static bool TryParseHex(string hex, ref double h, ref double s, ref double v, ref double a)
    {
        if (hex.Length is not (3 or 4 or 6 or 8) || !hex.All(Uri.IsHexDigit)) return false;
        if (hex.Length <= 4) // rgb / rgba shorthand - double each digit
            hex = string.Concat(hex.Select(c => $"{c}{c}"));

        byte Channel(int index) => byte.Parse(hex.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        (h, s, v) = RgbToHsv(Channel(0), Channel(1), Channel(2));
        a = hex.Length == 8 ? Channel(3) / 255.0 : 1;
        return true;
    }

    private static bool TryParseRgb(string[] parts, ref double h, ref double s, ref double v, ref double a)
    {
        if (parts.Length is not (3 or 4)) return false;

        double Channel(string part) =>
            Math.Clamp(part.EndsWith('%') ? Number(part[..^1]) / 100 : Number(part) / 255, 0, 1);

        (h, s, v) = RgbToHsv(Channel(parts[0]), Channel(parts[1]), Channel(parts[2]));
        a = parts.Length == 4 ? ParseAlpha(parts[3]) : 1;
        return true;
    }

    private static bool TryParseHsl(string[] parts, ref double h, ref double s, ref double v, ref double a)
    {
        if (parts.Length is not (3 or 4)) return false;

        var hue = Wrap(Number(parts[0].TrimEnd('d', 'e', 'g')));
        var sl = Math.Clamp(Number(parts[1].TrimEnd('%')) / 100, 0, 1);
        var l = Math.Clamp(Number(parts[2].TrimEnd('%')) / 100, 0, 1);

        // HSL to HSV: value is the cone top over this lightness; saturation re-derived around it.
        v = l + sl * Math.Min(l, 1 - l);
        s = v == 0 ? 0 : 2 * (1 - l / v);
        h = hue;
        a = parts.Length == 4 ? ParseAlpha(parts[3]) : 1;
        return true;
    }

    private static bool TryParseHsb(string[] parts, ref double h, ref double s, ref double v, ref double a)
    {
        if (parts.Length is not (3 or 4)) return false;

        // HSB IS the picker's internal HSV - the channels map straight through.
        h = Wrap(Number(parts[0].TrimEnd('d', 'e', 'g')));
        s = Math.Clamp(Number(parts[1].TrimEnd('%')) / 100, 0, 1);
        v = Math.Clamp(Number(parts[2].TrimEnd('%')) / 100, 0, 1);
        a = parts.Length == 4 ? ParseAlpha(parts[3]) : 1;
        return true;
    }

    private static bool TryParseHwb(string[] parts, ref double h, ref double s, ref double v, ref double a)
    {
        if (parts.Length is not (3 or 4)) return false;

        var hue = Wrap(Number(parts[0].TrimEnd('d', 'e', 'g')));
        var w = Math.Clamp(Number(parts[1].TrimEnd('%')) / 100, 0, 1);
        var blk = Math.Clamp(Number(parts[2].TrimEnd('%')) / 100, 0, 1);
        if (w + blk >= 1) // an achromatic hwb - the spec normalizes to grey
        {
            var grey = w / (w + blk);
            (h, s, v) = (hue, 0, grey);
        }
        else
        {
            v = 1 - blk;
            s = 1 - w / v;
            h = hue;
        }
        a = parts.Length == 4 ? ParseAlpha(parts[3]) : 1;
        return true;
    }

    private static bool TryParseCmyk(string[] parts, ref double h, ref double s, ref double v, ref double a)
    {
        if (parts.Length is not (4 or 5)) return false;

        double Component(string part) =>
            Math.Clamp(part.EndsWith('%') ? Number(part[..^1]) / 100 : Number(part), 0, 1);

        var (c, m, y, k) = (Component(parts[0]), Component(parts[1]), Component(parts[2]), Component(parts[3]));
        (h, s, v) = RgbToHsv((1 - c) * (1 - k), (1 - m) * (1 - k), (1 - y) * (1 - k));
        a = parts.Length == 5 ? ParseAlpha(parts[4]) : 1;
        return true;
    }

    private static bool TryParseOklch(string[] parts, ref double h, ref double s, ref double v, ref double a)
    {
        if (parts.Length is not (3 or 4)) return false;

        var l = parts[0].EndsWith('%') ? Number(parts[0][..^1]) / 100 : Number(parts[0]);
        var c = parts[1].EndsWith('%') ? Number(parts[1][..^1]) / 100 * 0.4 : Number(parts[1]); // 100% = 0.4
        var hue = Wrap(Number(parts[2].TrimEnd('d', 'e', 'g'))) * Math.PI / 180;

        (h, s, v) = OklabToHsv(l, c * Math.Cos(hue), c * Math.Sin(hue));
        a = parts.Length == 4 ? ParseAlpha(parts[3]) : 1;
        return true;
    }

    private static bool TryParseOklab(string[] parts, ref double h, ref double s, ref double v, ref double a)
    {
        if (parts.Length is not (3 or 4)) return false;

        var l = parts[0].EndsWith('%') ? Number(parts[0][..^1]) / 100 : Number(parts[0]);
        double Axis(string part) => part.EndsWith('%') ? Number(part[..^1]) / 100 * 0.4 : Number(part); // 100% = ±0.4

        (h, s, v) = OklabToHsv(l, Axis(parts[1]), Axis(parts[2]));
        a = parts.Length == 4 ? ParseAlpha(parts[3]) : 1;
        return true;
    }

    // ---- serialization --------------------------------------------------------------------------

    /// <summary>Serialize HSVA in the given <paramref name="format"/>. Alpha is omitted where the format allows, when 1.</summary>
    public static string Format(double h, double s, double v, double a, ColorFormat format)
    {
        a = Math.Clamp(a, 0, 1);
        var (r, g, b) = HsvToRgb(h, s, v);
        return format switch
        {
            ColorFormat.Rgb => a < 1 ? $"rgb({r} {g} {b} / {Num(a)})" : $"rgb({r} {g} {b})",
            ColorFormat.Rgba => $"rgba({r}, {g}, {b}, {Num(a)})",
            ColorFormat.Hsl => FormatHsl(h, s, v, a, legacy: false),
            ColorFormat.Hsla => FormatHsl(h, s, v, a, legacy: true),
            ColorFormat.Hsb => FormatHsb(h, s, v, a),
            ColorFormat.Hwb => FormatHwb(h, s, v, a),
            ColorFormat.Cmyk => FormatCmyk(r, g, b, a),
            // The perceptual formats serialize from the UNROUNDED channels: 8-bit rgb would quantize
            // oklch's lightness and chroma into visible steps (0.7 coming back as 0.693).
            ColorFormat.Oklch => FormatOklch(HsvToRgbF(h, s, v), a),
            ColorFormat.Oklab => FormatOklab(HsvToRgbF(h, s, v), a),
            _ => a < 1 ? $"#{r:x2}{g:x2}{b:x2}{Byte(a):x2}" : $"#{r:x2}{g:x2}{b:x2}",
        };
    }

    /// <summary>
    /// The perceptual format a string is written in - <c>oklch()</c> or <c>oklab()</c> - or null for
    /// everything else. These two can address colors outside the sRGB gamut, so the picker keeps the
    /// source text of a value in one of them and hands it back untouched while the model still
    /// matches (see <c>BzColorPicker.Serialize</c>); every other format round-trips through 8-bit
    /// sRGB losslessly and needs no such memory.
    /// </summary>
    public static ColorFormat? PerceptualFormat(string? text)
    {
        var trimmed = text?.TrimStart();
        if (trimmed is null) return null;
        if (trimmed.StartsWith("oklch(", StringComparison.OrdinalIgnoreCase)) return ColorFormat.Oklch;
        if (trimmed.StartsWith("oklab(", StringComparison.OrdinalIgnoreCase)) return ColorFormat.Oklab;
        return null;
    }

    /// <summary>True when two color strings parse to the same 8-bit RGBA - the swatch-selected test.</summary>
    public static bool AreEqual(string? left, string? right)
    {
        if (!TryParse(left, out var h1, out var s1, out var v1, out var a1)) return false;
        if (!TryParse(right, out var h2, out var s2, out var v2, out var a2)) return false;
        return HsvToRgb(h1, s1, v1) == HsvToRgb(h2, s2, v2) && Byte(a1) == Byte(a2);
    }

    private static string FormatHsl(double h, double s, double v, double a, bool legacy)
    {
        // HSV to HSL: same hue; lightness sits mid-cone, saturation re-derived around it.
        var l = v * (1 - s / 2);
        var sl = l is 0 or 1 ? 0 : (v - l) / Math.Min(l, 1 - l);
        if (legacy)
            return $"hsla({Num(Wrap(h))}, {Num(sl * 100)}%, {Num(l * 100)}%, {Num(a)})";
        var head = $"hsl({Num(Wrap(h))} {Num(sl * 100)}% {Num(l * 100)}%";
        return a < 1 ? $"{head} / {Num(a)})" : $"{head})";
    }

    private static string FormatHsb(double h, double s, double v, double a)
    {
        var head = $"hsb({Num(Wrap(h))} {Num(Math.Clamp(s, 0, 1) * 100)}% {Num(Math.Clamp(v, 0, 1) * 100)}%";
        return a < 1 ? $"{head} / {Num(a)})" : $"{head})";
    }

    private static string FormatHwb(double h, double s, double v, double a)
    {
        var w = (1 - s) * v;
        var blk = 1 - v;
        var head = $"hwb({Num(Wrap(h))} {Num(w * 100)}% {Num(blk * 100)}%";
        return a < 1 ? $"{head} / {Num(a)})" : $"{head})";
    }

    private static string FormatCmyk(byte r, byte g, byte b, double a)
    {
        var (rf, gf, bf) = (r / 255.0, g / 255.0, b / 255.0);
        var k = 1 - Math.Max(rf, Math.Max(gf, bf));
        var (c, m, y) = k >= 1 ? (0d, 0d, 0d)
            : ((1 - rf - k) / (1 - k), (1 - gf - k) / (1 - k), (1 - bf - k) / (1 - k));
        var head = $"device-cmyk({Num(c * 100)}% {Num(m * 100)}% {Num(y * 100)}% {Num(k * 100)}%";
        return a < 1 ? $"{head} / {Num(a)})" : $"{head})";
    }

    private static string FormatOklch((double R, double G, double B) rgb, double a)
    {
        var (l, la, lb) = RgbToOklab(rgb.R, rgb.G, rgb.B);
        var c = Math.Sqrt(la * la + lb * lb);
        var hue = c < 0.0002 ? 0 : Wrap(Math.Atan2(lb, la) * 180 / Math.PI);
        var head = $"oklch({Num3(l)} {Num3(c)} {Num(hue)}";
        return a < 1 ? $"{head} / {Num(a)})" : $"{head})";
    }

    private static string FormatOklab((double R, double G, double B) rgb, double a)
    {
        var (l, la, lb) = RgbToOklab(rgb.R, rgb.G, rgb.B);
        var head = $"oklab({Num3(l)} {Num3(la)} {Num3(lb)}";
        return a < 1 ? $"{head} / {Num(a)})" : $"{head})";
    }

    // ---- OKLab (Björn Ottosson's sRGB matrices) -------------------------------------------------

    private static (double L, double A, double B) RgbToOklab(double r, double g, double b)
    {
        var (lr, lg, lb) = (SrgbToLinear(r), SrgbToLinear(g), SrgbToLinear(b));

        var l = Math.Cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb);
        var m = Math.Cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb);
        var s = Math.Cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb);

        return (
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    private static (double H, double S, double V) OklabToHsv(double lab, double a, double b)
    {
        var l = Math.Pow(lab + 0.3963377774 * a + 0.2158037573 * b, 3);
        var m = Math.Pow(lab - 0.1055613458 * a - 0.0638541728 * b, 3);
        var s = Math.Pow(lab - 0.0894841775 * a - 1.2914855480 * b, 3);

        var lr = +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        var lg = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        var lb = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

        // Clamp into the sRGB gamut - an out-of-gamut oklch pick lands on the nearest channel edge.
        return RgbToHsv(
            Math.Clamp(LinearToSrgb(lr), 0, 1),
            Math.Clamp(LinearToSrgb(lg), 0, 1),
            Math.Clamp(LinearToSrgb(lb), 0, 1));
    }

    private static double SrgbToLinear(double c) =>
        c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static double LinearToSrgb(double c) =>
        c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(Math.Max(c, 0), 1 / 2.4) - 0.055;

    // ---- shared bits ----------------------------------------------------------------------------

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

    private static string Num3(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
}
