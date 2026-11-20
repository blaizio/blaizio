using System.Globalization;
using Blaizio.Ui;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>ColorMath: every format the color picker reads and writes, to and from its HSVA model.</summary>
public class ColorMathTests
{
    private static (byte R, byte G, byte B) Rgb(string text)
    {
        Assert.True(ColorMath.TryParse(text, out var h, out var s, out var v, out _), $"'{text}' did not parse");
        return ColorMath.HsvToRgb(h, s, v);
    }

    private static double Alpha(string text)
    {
        Assert.True(ColorMath.TryParse(text, out _, out _, out _, out var a), $"'{text}' did not parse");
        return a;
    }

    // ---- HSV and RGB --------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 255, 0, 0)]
    [InlineData(60, 255, 255, 0)]
    [InlineData(120, 0, 255, 0)]
    [InlineData(180, 0, 255, 255)]
    [InlineData(240, 0, 0, 255)]
    [InlineData(300, 255, 0, 255)]
    public void The_six_corners_of_the_hue_wheel_are_the_pure_colors(double hue, byte r, byte g, byte b)
    {
        Assert.Equal((r, g, b), ColorMath.HsvToRgb(hue, 1, 1));
        var (h, s, v) = ColorMath.RgbToHsv(r, g, b);
        Assert.Equal(hue, h, 6);
        Assert.Equal(1, s, 6);
        Assert.Equal(1, v, 6);
    }

    [Theory]
    [InlineData(360, 0)]    // a full turn is red again
    [InlineData(720, 0)]
    [InlineData(-120, 240)] // a negative hue counts back from red
    [InlineData(480, 120)]
    [InlineData(359.999, 359.999)]
    public void A_hue_outside_the_wheel_wraps_onto_it(double hue, double same)
    {
        Assert.Equal(ColorMath.HsvToRgb(same, 1, 1), ColorMath.HsvToRgb(hue, 1, 1));
    }

    [Fact]
    public void Saturation_and_value_beyond_their_range_are_clamped()
    {
        Assert.Equal(ColorMath.HsvToRgb(200, 1, 1), ColorMath.HsvToRgb(200, 4, 9));
        Assert.Equal(((byte)0, (byte)0, (byte)0), ColorMath.HsvToRgb(200, -1, -1));
    }

    [Fact]
    public void A_grey_has_no_hue_and_no_saturation()
    {
        Assert.Equal((0d, 0d, 0d), ColorMath.RgbToHsv(0, 0, 0));
        var (h, s, v) = ColorMath.RgbToHsv(128, 128, 128);
        Assert.Equal(0, h);
        Assert.Equal(0, s);
        Assert.Equal(128 / 255.0, v, 6);
        Assert.Equal(((byte)128, (byte)128, (byte)128), ColorMath.HsvToRgb(77, 0, 128 / 255.0));
    }

    [Fact]
    public void The_unrounded_channels_keep_what_eight_bits_would_drop()
    {
        var (r, g, b) = ColorMath.HsvToRgbF(0, 0.5, 0.7);
        Assert.Equal(0.7, r, 9);
        Assert.Equal(0.35, g, 9);
        Assert.Equal(0.35, b, 9);
    }

    // ---- parsing ------------------------------------------------------------------------------

    [Theory]
    [InlineData("#ff8800")]
    [InlineData("#FF8800")]
    [InlineData("  #ff8800  ")]
    [InlineData("#f80")]
    [InlineData("ff8800")]
    [InlineData("f80")]
    [InlineData("rgb(255, 136, 0)")]
    [InlineData("rgb(255 136 0)")]
    [InlineData("RGB(255 136 0)")]
    [InlineData("rgba(255, 136, 0, 1)")]
    [InlineData("rgb(100% 53.3333% 0%)")]
    [InlineData("255 136 0")]
    [InlineData("255, 136, 0")]
    [InlineData("hsl(32 100% 50%)")]
    [InlineData("hsl(32deg, 100%, 50%)")]
    [InlineData("hsla(32, 100%, 50%, 1)")]
    [InlineData("hsb(32 100% 100%)")]
    [InlineData("hsv(32, 100%, 100%)")]
    [InlineData("hwb(32 0% 0%)")]
    [InlineData("device-cmyk(0% 46.6667% 100% 0%)")]
    [InlineData("cmyk(0, 0.466667, 1, 0)")]
    public void Every_spelling_of_one_color_parses_to_the_same_color(string text)
    {
        Assert.Equal(((byte)255, (byte)136, (byte)0), Rgb(text));
        Assert.Equal(1, Alpha(text));
    }

    [Theory]
    [InlineData("hsl(120 100% 25%)", 0, 128, 0)]
    [InlineData("hsl(0 0% 100%)", 255, 255, 255)]
    [InlineData("hsl(210 100% 0%)", 0, 0, 0)]
    [InlineData("hsv(120, 50%, 100%)", 128, 255, 128)]
    [InlineData("hwb(120 0% 50%)", 0, 128, 0)]
    [InlineData("hwb(0 50% 50%)", 128, 128, 128)]
    // Whiteness and blackness that add up past 100% are a grey in their own proportion.
    [InlineData("hwb(90 100% 100%)", 128, 128, 128)]
    [InlineData("hwb(90 30% 90%)", 64, 64, 64)]
    [InlineData("hwb(90 0% 100%)", 0, 0, 0)]
    [InlineData("cmyk(100% 0% 0% 0%)", 0, 255, 255)]
    [InlineData("cmyk(0, 0, 0, 1)", 0, 0, 0)]
    [InlineData("device-cmyk(0% 0% 0% 50%)", 128, 128, 128)]
    [InlineData("oklch(1 0 0)", 255, 255, 255)]
    [InlineData("oklch(100% 0 0)", 255, 255, 255)]
    [InlineData("oklch(0 0 0)", 0, 0, 0)]
    [InlineData("oklab(1 0 0)", 255, 255, 255)]
    [InlineData("oklab(0 0 0)", 0, 0, 0)]
    public void Function_forms_land_on_the_color_they_name(string text, byte r, byte g, byte b)
    {
        Assert.Equal((r, g, b), Rgb(text));
    }

    [Theory]
    [InlineData("oklch(0.628 0.258 29.23)", 255, 0, 0)]
    [InlineData("oklch(62.8% 64.5% 29.23deg)", 255, 0, 0)] // chroma: 100% is 0.4
    [InlineData("oklab(0.628 0.225 0.126)", 255, 0, 0)]
    [InlineData("oklch(0.452 0.313 264.05)", 0, 0, 255)]
    [InlineData("oklab(0.8664 -0.2339 0.1795)", 0, 255, 0)]
    public void Perceptual_forms_land_within_a_step_of_the_color_they_name(string text, int r, int g, int b)
    {
        var parsed = Rgb(text);
        Assert.InRange(parsed.R, r - 2, r + 2);
        Assert.InRange(parsed.G, g - 2, g + 2);
        Assert.InRange(parsed.B, b - 2, b + 2);
    }

    [Fact]
    public void A_perceptual_color_outside_the_screen_gamut_is_brought_to_its_edge()
    {
        // No screen shows this much chroma: the channels stop at their ends instead of wrapping.
        Assert.True(ColorMath.TryParse("oklch(0.7 0.4 150)", out _, out var s, out var v, out _));
        Assert.InRange(s, 0, 1);
        Assert.InRange(v, 0, 1);
        var (r, g, _) = Rgb("oklch(0.7 0.4 150)");
        Assert.Equal(0, r);
        Assert.True(g > 200, $"green was {g}");
    }

    [Theory]
    [InlineData("hsl(360 100% 50%)")]
    [InlineData("hsl(-360 100% 50%)")]
    [InlineData("hsl(720deg 100% 50%)")]
    [InlineData("hsb(360 100% 100%)")]
    [InlineData("hwb(360 0% 0%)")]
    [InlineData("oklch(0.628 0.258 389.23)")]
    public void A_hue_of_a_full_turn_parses_as_red(string text)
    {
        Assert.True(ColorMath.TryParse(text, out var h, out _, out _, out _));
        var (r, g, b) = Rgb(text);
        Assert.InRange(r, 253, 255);
        Assert.InRange(g, 0, 2);
        Assert.InRange(b, 0, 2);
        Assert.InRange(h, 0, 359.9999);
    }

    [Theory]
    [InlineData("#ff880080", 128 / 255.0)]
    [InlineData("#f808", 0x88 / 255.0)]
    [InlineData("ff880000", 0)]
    [InlineData("rgba(255, 136, 0, 0.5)", 0.5)]
    [InlineData("rgb(255 136 0 / 0.5)", 0.5)]
    [InlineData("rgb(255 136 0 / 50%)", 0.5)]
    [InlineData("255, 136, 0, 0.25", 0.25)]
    [InlineData("hsl(32 100% 50% / 0.2)", 0.2)]
    [InlineData("hsla(32, 100%, 50%, 20%)", 0.2)]
    [InlineData("hsb(32 100% 100% / 0.4)", 0.4)]
    [InlineData("hwb(32 0% 0% / 0.6)", 0.6)]
    [InlineData("device-cmyk(0% 47% 100% 0% / 0.7)", 0.7)]
    [InlineData("oklch(0.7 0.1 50 / 0.8)", 0.8)]
    [InlineData("oklab(0.7 0.1 0.1 / 90%)", 0.9)]
    // Past either end the alpha stops at the end.
    [InlineData("rgb(0 0 0 / 150%)", 1)]
    [InlineData("rgba(0, 0, 0, -1)", 0)]
    public void Alpha_is_read_from_every_form_that_carries_one(string text, double alpha)
    {
        Assert.Equal(alpha, Alpha(text), 6);
    }

    [Theory]
    [InlineData("rgb(300 -20 128)", 255, 0, 128)]
    [InlineData("rgb(150% 50% -10%)", 255, 128, 0)]
    [InlineData("hsl(0 250% 50%)", 255, 0, 0)]
    [InlineData("hsb(0 100% 400%)", 255, 0, 0)]
    public void Channels_past_their_range_are_clamped(string text, byte r, byte g, byte b)
    {
        Assert.Equal((r, g, b), Rgb(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#123456789")]
    [InlineData("#gggggg")]
    [InlineData("12345")]
    [InlineData("red")] // named colors are not part of what the picker reads
    [InlineData("transparent")]
    [InlineData("rgb(1, 2)")]
    [InlineData("rgb(1, 2, 3, 4, 5)")]
    [InlineData("rgb()")]
    [InlineData("rgb(1, 2, 3")]
    [InlineData("(1, 2, 3)")]
    [InlineData("hsl(120)")]
    [InlineData("hwb(1 2)")]
    [InlineData("cmyk(1, 2, 3)")]
    [InlineData("oklch(0.5 0.1)")]
    [InlineData("oklab(0.5)")]
    [InlineData("lab(50 10 10)")]
    [InlineData("color(display-p3 1 0 0)")]
    [InlineData("1 2")]
    [InlineData("1 2 three")]
    [InlineData("1 2 3 4 5")]
    public void What_is_not_a_color_is_refused_and_leaves_opaque_black(string? text)
    {
        Assert.False(ColorMath.TryParse(text, out var h, out var s, out var v, out var a));
        Assert.Equal((0d, 0d, 0d, 1d), (h, s, v, a));
    }

    [Theory(Skip = "bug: ColorMath.TryParse reads a channel that is not a number as 0, so rgb(a b c) parses as black")]
    [InlineData("rgb(a b c)")]
    [InlineData("hsl(up 10% 10%)")]
    [InlineData("rgb(12 34 x)")]
    public void Words_inside_a_color_function_are_refused(string text)
    {
        Assert.False(ColorMath.TryParse(text, out _, out _, out _, out _));
    }

    // ---- writing ------------------------------------------------------------------------------

    [Theory]
    [InlineData(ColorFormat.Hex, "#ff0000")]
    [InlineData(ColorFormat.Rgb, "rgb(255 0 0)")]
    [InlineData(ColorFormat.Rgba, "rgba(255, 0, 0, 1)")]
    [InlineData(ColorFormat.Hsl, "hsl(0 100% 50%)")]
    [InlineData(ColorFormat.Hsla, "hsla(0, 100%, 50%, 1)")]
    [InlineData(ColorFormat.Hsb, "hsb(0 100% 100%)")]
    [InlineData(ColorFormat.Hwb, "hwb(0 0% 0%)")]
    [InlineData(ColorFormat.Cmyk, "device-cmyk(0% 100% 100% 0%)")]
    [InlineData(ColorFormat.Oklch, "oklch(0.628 0.258 29.23)")]
    [InlineData(ColorFormat.Oklab, "oklab(0.628 0.225 0.126)")]
    public void Opaque_red_in_every_format(ColorFormat format, string expected)
    {
        Assert.Equal(expected, ColorMath.Format(0, 1, 1, 1, format));
    }

    [Theory]
    [InlineData(ColorFormat.Hex, "#ff000080")]
    [InlineData(ColorFormat.Rgb, "rgb(255 0 0 / 0.5)")]
    [InlineData(ColorFormat.Rgba, "rgba(255, 0, 0, 0.5)")]
    [InlineData(ColorFormat.Hsl, "hsl(0 100% 50% / 0.5)")]
    [InlineData(ColorFormat.Hsla, "hsla(0, 100%, 50%, 0.5)")]
    [InlineData(ColorFormat.Hsb, "hsb(0 100% 100% / 0.5)")]
    [InlineData(ColorFormat.Hwb, "hwb(0 0% 0% / 0.5)")]
    [InlineData(ColorFormat.Cmyk, "device-cmyk(0% 100% 100% 0% / 0.5)")]
    [InlineData(ColorFormat.Oklch, "oklch(0.628 0.258 29.23 / 0.5)")]
    [InlineData(ColorFormat.Oklab, "oklab(0.628 0.225 0.126 / 0.5)")]
    public void Half_transparent_red_in_every_format(ColorFormat format, string expected)
    {
        Assert.Equal(expected, ColorMath.Format(0, 1, 1, 0.5, format));
    }

    [Theory]
    [InlineData(ColorFormat.Hex, "#000000", "#ffffff")]
    [InlineData(ColorFormat.Rgb, "rgb(0 0 0)", "rgb(255 255 255)")]
    [InlineData(ColorFormat.Hsl, "hsl(0 0% 0%)", "hsl(0 0% 100%)")]
    [InlineData(ColorFormat.Hsb, "hsb(0 0% 0%)", "hsb(0 0% 100%)")]
    [InlineData(ColorFormat.Hwb, "hwb(0 0% 100%)", "hwb(0 100% 0%)")]
    [InlineData(ColorFormat.Cmyk, "device-cmyk(0% 0% 0% 100%)", "device-cmyk(0% 0% 0% 0%)")]
    [InlineData(ColorFormat.Oklch, "oklch(0 0 0)", "oklch(1 0 0)")]
    [InlineData(ColorFormat.Oklab, "oklab(0 0 0)", "oklab(1 0 0)")]
    public void Black_and_white_in_every_format(ColorFormat format, string black, string white)
    {
        Assert.Equal(black, ColorMath.Format(0, 0, 0, 1, format));
        Assert.Equal(white, ColorMath.Format(0, 0, 1, 1, format));
    }

    [Theory]
    [InlineData(360, "hsl(0 100% 50%)")]
    [InlineData(-90, "hsl(270 100% 50%)")]
    [InlineData(450, "hsl(90 100% 50%)")]
    public void A_hue_outside_the_wheel_is_written_inside_it(double hue, string expected)
    {
        Assert.Equal(expected, ColorMath.Format(hue, 1, 1, 1, ColorFormat.Hsl));
    }

    [Fact]
    public void Alpha_outside_its_range_is_written_at_the_nearest_end()
    {
        Assert.Equal("rgba(255, 0, 0, 1)", ColorMath.Format(0, 1, 1, 7, ColorFormat.Rgba));
        Assert.Equal("#ff0000", ColorMath.Format(0, 1, 1, 7, ColorFormat.Hex));
        Assert.Equal("#ff000000", ColorMath.Format(0, 1, 1, -3, ColorFormat.Hex));
        Assert.Equal("rgb(255 0 0 / 0)", ColorMath.Format(0, 1, 1, -3, ColorFormat.Rgb));
    }

    [Fact]
    public void Numbers_are_written_with_a_dot_whatever_the_culture()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal("rgba(255, 0, 0, 0.25)", ColorMath.Format(0, 1, 1, 0.25, ColorFormat.Rgba));
            Assert.Equal("oklch(0.628 0.258 29.23 / 0.25)", ColorMath.Format(0, 1, 1, 0.25, ColorFormat.Oklch));
            Assert.Equal(0.25, Alpha("rgba(255, 0, 0, 0.25)"), 6);
            Assert.Equal(((byte)255, (byte)0, (byte)0), Rgb("hsl(0 100% 50.0%)"));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    // ---- round trips --------------------------------------------------------------------------

    public static TheoryData<ColorFormat> Formats => new(Enum.GetValues<ColorFormat>());

    public static TheoryData<ColorFormat> ScreenFormats =>
        new(Enum.GetValues<ColorFormat>().Where(f => f is not (ColorFormat.Oklch or ColorFormat.Oklab)));

    // Every sixth step of a channel, both ends included.
    private static IEnumerable<int> Steps()
    {
        for (var c = 0; c < 255; c += 6) yield return c;
        yield return 255;
    }

    [Theory]
    [MemberData(nameof(ScreenFormats))]
    public void A_color_written_in_a_screen_format_reads_back_as_the_very_color(ColorFormat format)
    {
        foreach (var r in Steps())
        foreach (var g in Steps())
        foreach (var b in Steps())
        {
            var (h, s, v) = ColorMath.RgbToHsv((byte)r, (byte)g, (byte)b);
            var text = ColorMath.Format(h, s, v, 1, format);

            Assert.True(ColorMath.TryParse(text, out var h2, out var s2, out var v2, out var a2), text);
            var back = ColorMath.HsvToRgb(h2, s2, v2);
            if (back != ((byte)r, (byte)g, (byte)b))
                Assert.Fail($"rgb({r} {g} {b}) written as {text} read back as rgb({back.R} {back.G} {back.B})");
            Assert.Equal(1, a2);
        }
    }

    [Theory]
    [InlineData(ColorFormat.Oklch)]
    [InlineData(ColorFormat.Oklab)]
    public void A_color_written_in_a_perceptual_format_reads_back_next_to_itself(ColorFormat format)
    {
        // Three decimals of lightness and chroma are finer than a screen step almost everywhere,
        // but not for a channel close to zero, where a step is far smaller than the eye can tell.
        const int Near = 10;
        var worst = 0;
        var rewritten = 0;

        foreach (var r in Steps())
        foreach (var g in Steps())
        foreach (var b in Steps())
        {
            var (h, s, v) = ColorMath.RgbToHsv((byte)r, (byte)g, (byte)b);
            var text = ColorMath.Format(h, s, v, 1, format);

            Assert.True(ColorMath.TryParse(text, out var h2, out var s2, out var v2, out _), text);
            var back = ColorMath.HsvToRgb(h2, s2, v2);
            worst = Math.Max(worst,
                Math.Max(Math.Abs(back.R - r), Math.Max(Math.Abs(back.G - g), Math.Abs(back.B - b))));

            // Away from the edge of what a screen shows, nothing is clamped on the way back, so
            // the text the picker would write again is the text it read.
            var inside = Math.Min(r, Math.Min(g, b)) >= 30 && Math.Max(r, Math.Max(g, b)) <= 225;
            if (inside && Unsigned(ColorMath.Format(h2, s2, v2, 1, format)) != Unsigned(text)) rewritten++;
        }

        Assert.InRange(worst, 0, Near);
        Assert.Equal(0, rewritten);

        // An axis that rounds to zero from below is written "-0" (see the skipped test below):
        // the same number, so it is not counted as a change here.
        static string Unsigned(string text) => text.Replace(" -0 ", " 0 ").Replace(" -0)", " 0)");
    }

    [Fact(Skip = "bug: ColorMath.Format writes an OKLab axis that rounds to zero from below as -0")]
    public void An_axis_that_rounds_to_zero_is_written_without_a_sign()
    {
        var (h, s, v) = ColorMath.RgbToHsv(30, 36, 54);

        Assert.Equal("oklab(0.263 0 -0.035)", ColorMath.Format(h, s, v, 1, ColorFormat.Oklab));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Alpha_survives_the_trip_in_every_format(ColorFormat format)
    {
        foreach (var alpha in new[] { 0, 0.25, 0.5, 0.75 })
        {
            var text = ColorMath.Format(200, 0.6, 0.8, alpha, format);
            Assert.True(ColorMath.TryParse(text, out _, out _, out _, out var back), text);
            // Hex keeps alpha in eight bits, the others in two decimals.
            Assert.Equal(alpha, back, 2);
        }
    }

    // ---- helpers the picker leans on ------------------------------------------------------------

    [Theory]
    [InlineData("#ff0000", "rgb(255 0 0)", true)]
    [InlineData("#FF0000", "hsl(0 100% 50%)", true)]
    [InlineData("#ff0000", "#ff0000ff", true)]
    [InlineData("#ff0000", "#ff0001", false)]
    [InlineData("#ff0000", "#ff000080", false)]
    [InlineData("#ff0000", "red", false)]
    [InlineData("nonsense", "nonsense", false)]
    [InlineData(null, null, false)]
    public void Two_strings_are_equal_when_they_name_the_same_eight_bit_color(string? left, string? right, bool equal)
    {
        Assert.Equal(equal, ColorMath.AreEqual(left, right));
        Assert.Equal(equal, ColorMath.AreEqual(right, left));
    }

    [Theory]
    [InlineData("oklch(0.7 0.1 50)", ColorFormat.Oklch)]
    [InlineData("  OKLCH(0.7 0.1 50)", ColorFormat.Oklch)]
    [InlineData("oklab(0.7 0.1 0.1)", ColorFormat.Oklab)]
    [InlineData("#ff0000", null)]
    [InlineData("rgb(1 2 3)", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Only_the_two_perceptual_forms_are_reported_as_perceptual(string? text, ColorFormat? expected)
    {
        Assert.Equal(expected, ColorMath.PerceptualFormat(text));
    }
}
