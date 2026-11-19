using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Net.Codecrete.QrCodeGenerator;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>BzQrCode: what is encoded, how large the symbol comes out, and how it is drawn.</summary>
public partial class QrCodeTests : BunitContext
{
    public QrCodeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private IRenderedComponent<BzQrCode> Qr(string? value, Action<ComponentParameterCollectionBuilder<BzQrCode>>? more = null) =>
        Render<BzQrCode>(p =>
        {
            p.Add(x => x.Value, value);
            more?.Invoke(p);
        });

    // The side of the drawing in modules, quiet zone included.
    private static double Side(IRenderedComponent<BzQrCode> cut)
    {
        var box = cut.Find("svg").GetAttribute("viewBox")!.Split(' ');
        Assert.Equal(["0", "0"], box[..2]);
        Assert.Equal(box[2], box[3]);
        return double.Parse(box[2], CultureInfo.InvariantCulture);
    }

    private static IElement Modules(IRenderedComponent<BzQrCode> cut) => cut.FindAll("svg > path")[0];
    private static IElement Eyes(IRenderedComponent<BzQrCode> cut) => cut.FindAll("svg > path")[1];

    [GeneratedRegex(@"M(-?[\d.]+) (-?[\d.]+)h1v1h-1z")]
    private static partial Regex SquareModule();

    // Where the square modules were drawn, in drawing units.
    private static HashSet<(double X, double Y)> Drawn(IRenderedComponent<BzQrCode> cut) =>
        [.. SquareModule().Matches(Modules(cut).GetAttribute("d")!).Select(m => (
            double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))];

    // ---- what is encoded ----------------------------------------------------------------------

    [Theory]
    [InlineData("https://blaiz.io", QrCodeErrorCorrection.Low, QrCode.Ecc.Low)]
    [InlineData("https://blaiz.io", QrCodeErrorCorrection.Medium, QrCode.Ecc.Medium)]
    [InlineData("https://blaiz.io", QrCodeErrorCorrection.Quartile, QrCode.Ecc.Quartile)]
    [InlineData("https://blaiz.io", QrCodeErrorCorrection.High, QrCode.Ecc.High)]
    [InlineData("WIFI:T:WPA;S:Guest;P:correct horse battery staple;;", QrCodeErrorCorrection.Medium, QrCode.Ecc.Medium)]
    [InlineData("", QrCodeErrorCorrection.Medium, QrCode.Ecc.Medium)]
    public void Every_dark_module_of_the_symbol_is_drawn_once_where_it_belongs(
        string value, QrCodeErrorCorrection level, QrCode.Ecc ecc)
    {
        const int Quiet = 2;
        var symbol = QrCode.EncodeText(value, ecc);
        var n = symbol.Size;
        var expected = new HashSet<(double, double)>();
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            // The three corner patterns are drawn apart, as the eyes.
            var eye = (x < 7 && y < 7) || (x >= n - 7 && y < 7) || (x < 7 && y >= n - 7);
            if (symbol.GetModule(x, y) && !eye) expected.Add((x + Quiet, y + Quiet));
        }

        var cut = Qr(value, p => p.Add(x => x.ErrorCorrection, level));

        Assert.Equal(n + 2 * Quiet, Side(cut));
        var drawn = Drawn(cut);
        Assert.Equal(expected.Count, SquareModule().Matches(Modules(cut).GetAttribute("d")!).Count);
        Assert.True(expected.SetEquals(drawn));
    }

    [Theory]
    // Twelve letters: a 21-module symbol holds them at the two lower levels only.
    [InlineData(QrCodeErrorCorrection.Low, 21)]
    [InlineData(QrCodeErrorCorrection.Medium, 21)]
    [InlineData(QrCodeErrorCorrection.Quartile, 25)]
    [InlineData(QrCodeErrorCorrection.High, 25)]
    public void More_error_correction_needs_a_larger_symbol_for_the_same_text(QrCodeErrorCorrection level, int modules)
    {
        var cut = Qr("abcdefghijkl", p => p.Add(x => x.ErrorCorrection, level).Add(x => x.QuietZone, 0));

        Assert.Equal(modules, Side(cut));
    }

    [Fact]
    public void Longer_text_needs_a_larger_symbol()
    {
        var sides = new[] { 1, 20, 60, 150, 400 }
            .Select(length => Side(Qr(new string('a', length), p => p.Add(x => x.QuietZone, 0))))
            .ToList();

        Assert.Equal(21, sides[0]);
        Assert.Equal(sides.Order(), sides);
        Assert.Equal(sides.Count, sides.Distinct().Count());
        // A symbol grows four modules a version.
        Assert.All(sides, side => Assert.Equal(1, side % 4));
    }

    [Fact]
    public void Digits_pack_tighter_than_letters()
    {
        var digits = Side(Qr("12345678901234567890", p => p.Add(x => x.QuietZone, 0)));
        var letters = Side(Qr("abcdefghijklmnopqrst", p => p.Add(x => x.QuietZone, 0)));

        Assert.Equal(21, digits);
        Assert.Equal(25, letters);
    }

    [Fact]
    public void Text_too_long_for_any_symbol_says_so_under_the_same_name()
    {
        var cut = Qr(new string('x', 3000), p => p
            .Add(x => x.ErrorCorrection, QrCodeErrorCorrection.High)
            .Add(x => x.Label, "Ticket code"));

        Assert.Empty(cut.FindAll("svg"));
        var fallback = cut.Find("[role=img]");
        Assert.Equal("Ticket code", fallback.GetAttribute("aria-label"));
        Assert.Equal("Value too long for a QR code", fallback.TextContent.Trim());
    }

    [Fact]
    public void No_value_draws_nothing_inside_the_box()
    {
        var cut = Qr(null);

        var root = cut.Find("[data-slot=qr-code]");
        Assert.Equal(0, root.ChildElementCount);
    }

    [Fact]
    public void A_new_value_redraws_the_symbol()
    {
        var cut = Qr("first");
        var before = Modules(cut).GetAttribute("d");

        cut.Render(p => p.Add(x => x.Value, "second"));

        Assert.NotEqual(before, Modules(cut).GetAttribute("d"));

        cut.Render(p => p.Add(x => x.Value, "first"));
        Assert.Equal(before, Modules(cut).GetAttribute("d"));
    }

    // ---- sizing -------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 21)]
    [InlineData(2, 25)]
    [InlineData(4, 29)]
    [InlineData(-3, 21)] // a negative margin is no margin
    public void The_quiet_zone_is_added_on_every_side(int quiet, int side)
    {
        var cut = Qr("A", p => p.Add(x => x.QuietZone, quiet));

        Assert.Equal(side, Side(cut));
        // The first eye sits right inside the margin.
        var origin = Math.Max(0, quiet).ToString(CultureInfo.InvariantCulture);
        Assert.StartsWith($"M{origin} {origin}h7v7h-7z", Eyes(cut).GetAttribute("d"));
    }

    [Theory]
    [InlineData(QrCodeSize.Sm, "sm")]
    [InlineData(QrCodeSize.Default, "md")]
    [InlineData(QrCodeSize.Lg, "lg")]
    [InlineData(QrCodeSize.Xl, "xl")]
    public void Each_size_is_named_on_the_box(QrCodeSize size, string name)
    {
        var cut = Qr("A", p => p.Add(x => x.Size, size));

        var root = cut.Find("[data-slot=qr-code]");
        Assert.Equal(name, root.GetAttribute("data-size"));
        Assert.Contains($"bz-qr-code-size-{name}", root.ClassList);
        // The drawing fills whatever box the size gives it.
        Assert.Contains("size-full", cut.Find("svg").ClassList);
    }

    // ---- drawing ------------------------------------------------------------------------------

    [Fact]
    public void The_symbol_is_one_named_image()
    {
        var cut = Qr("https://blaiz.io", p => p.Add(x => x.Label, "QR code linking to blaiz.io"));

        var svg = cut.Find("svg");
        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal("QR code linking to blaiz.io", svg.GetAttribute("aria-label"));
        Assert.Equal("QR code linking to blaiz.io", cut.Find("svg > title").TextContent);

        Assert.Equal("QR code", Qr("x").Find("svg").GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData(QrCodeModuleStyle.Square, "square", "h1v1h-1z")]
    [InlineData(QrCodeModuleStyle.Rounded, "rounded", "a0.3 0.3 0 0 1")]
    [InlineData(QrCodeModuleStyle.Dots, "dots", "a0.45 0.45 0 1 0")]
    [InlineData(QrCodeModuleStyle.Diamond, "diamond", "L")]
    public void Each_module_style_draws_its_own_shape_for_the_same_modules(QrCodeModuleStyle style, string name, string mark)
    {
        var square = Qr("https://blaiz.io");
        var cut = Qr("https://blaiz.io", p => p.Add(x => x.ModuleStyle, style));

        Assert.Equal(name, cut.Find("[data-slot=qr-code]").GetAttribute("data-module-style"));
        var path = Modules(cut).GetAttribute("d")!;
        Assert.Contains(mark, path);
        // One closed shape a module, whatever the shape.
        Assert.Equal(Drawn(square).Count, path.Count(c => c == 'M'));
        // The eyes are not the modules' business.
        Assert.Equal(Eyes(square).GetAttribute("d"), Eyes(cut).GetAttribute("d"));
    }

    [Theory]
    [InlineData(QrCodeEyeStyle.Square, "square", "h7v7h-7z")]
    [InlineData(QrCodeEyeStyle.Rounded, "rounded", "a2.2 2.2 0 0 1")]
    [InlineData(QrCodeEyeStyle.Circle, "circle", "a3.5 3.5 0 1 0")]
    public void Each_eye_style_draws_three_eyes_of_three_rings(QrCodeEyeStyle style, string name, string mark)
    {
        var cut = Qr("https://blaiz.io", p => p.Add(x => x.EyeStyle, style));

        Assert.Equal(name, cut.Find("[data-slot=qr-code]").GetAttribute("data-eye-style"));
        var eyes = Eyes(cut);
        Assert.Contains(mark, eyes.GetAttribute("d"));
        Assert.Equal(9, eyes.GetAttribute("d")!.Count(c => c == 'M'));
        // Frame, hole and pupil alternate through one path.
        Assert.Equal("evenodd", eyes.GetAttribute("fill-rule"));
    }

    [Fact]
    public void The_eyes_sit_in_three_corners()
    {
        var cut = Qr("A", p => p.Add(x => x.QuietZone, 0)); // 21 modules

        var d = Eyes(cut).GetAttribute("d")!;
        Assert.Contains("M0 0h7v7h-7z", d);
        Assert.Contains("M14 0h7v7h-7z", d);
        Assert.Contains("M0 14h7v7h-7z", d);
        Assert.DoesNotContain("M14 14h7v7h-7z", d);
    }

    [Theory]
    [InlineData(QrCodeModuleStyle.Square, QrCodeEyeStyle.Square, true)]
    [InlineData(QrCodeModuleStyle.Dots, QrCodeEyeStyle.Square, false)]
    [InlineData(QrCodeModuleStyle.Square, QrCodeEyeStyle.Circle, false)]
    public void Only_an_all_square_symbol_asks_for_crisp_edges(QrCodeModuleStyle modules, QrCodeEyeStyle eyes, bool crisp)
    {
        var cut = Qr("A", p => p.Add(x => x.ModuleStyle, modules).Add(x => x.EyeStyle, eyes));

        Assert.Equal(crisp ? "crispEdges" : null, cut.Find("svg").GetAttribute("shape-rendering"));
    }

    // ---- colors -------------------------------------------------------------------------------

    [Fact]
    public void With_no_colors_the_symbol_takes_the_text_color_and_no_background()
    {
        var cut = Qr("A");

        Assert.Equal("currentColor", Modules(cut).GetAttribute("fill"));
        Assert.Equal("currentColor", Eyes(cut).GetAttribute("fill"));
        Assert.Empty(cut.FindAll("svg > rect"));
        Assert.Empty(cut.FindAll("svg > defs"));
    }

    [Fact]
    public void Colors_reach_the_modules_the_eyes_and_the_background()
    {
        var cut = Qr("A", p => p
            .Add(x => x.Foreground, "#111111")
            .Add(x => x.EyeColor, "#aa0000")
            .Add(x => x.Background, "#ffffff"));

        Assert.Equal("#111111", Modules(cut).GetAttribute("fill"));
        Assert.Equal("#aa0000", Eyes(cut).GetAttribute("fill"));
        // The background covers the quiet zone too.
        var background = cut.Find("svg > rect");
        Assert.Equal("#ffffff", background.GetAttribute("fill"));
        Assert.Equal("25", background.GetAttribute("width"));
        Assert.Equal("25", background.GetAttribute("height"));
    }

    [Theory]
    [InlineData(0, "0", "0.5", "1", "0.5")]   // left to right
    [InlineData(90, "0.5", "0", "0.5", "1")]  // top to bottom
    [InlineData(180, "1", "0.5", "0", "0.5")] // right to left
    public void A_second_color_fills_the_symbol_with_a_gradient_along_its_angle(
        double angle, string x1, string y1, string x2, string y2)
    {
        var cut = Qr("A", p => p
            .Add(x => x.Foreground, "#111111")
            .Add(x => x.ForegroundEnd, "#0000aa")
            .Add(x => x.GradientAngle, angle));

        var gradient = cut.Find("svg > defs > linearGradient");
        Assert.Equal((x1, y1, x2, y2), (
            gradient.GetAttribute("x1"), gradient.GetAttribute("y1"),
            gradient.GetAttribute("x2"), gradient.GetAttribute("y2")));
        Assert.Equal(["#111111", "#0000aa"], gradient.Children.Select(stop => stop.GetAttribute("stop-color")));

        var fill = $"url(#{gradient.Id})";
        Assert.Equal(fill, Modules(cut).GetAttribute("fill"));
        // The eyes follow the gradient unless they have a color of their own.
        Assert.Equal(fill, Eyes(cut).GetAttribute("fill"));
    }

    [Fact]
    public void Two_symbols_on_a_page_do_not_share_a_gradient()
    {
        var first = Qr("A", p => p.Add(x => x.ForegroundEnd, "#0000aa")).Find("linearGradient").Id;
        var second = Qr("A", p => p.Add(x => x.ForegroundEnd, "#0000aa")).Find("linearGradient").Id;

        Assert.NotEqual(first, second);
    }

    // ---- the center ---------------------------------------------------------------------------

    [Theory]
    [InlineData(0.22, "9.75", "5.5")]
    [InlineData(0.9, "6.25", "12.5")]  // never more than half the symbol
    [InlineData(0.001, "11.875", "1.25")] // never less than a twentieth
    public void A_center_image_is_centered_at_its_share_of_the_symbol(double scale, string position, string side)
    {
        var cut = Qr("A", p => p.Add(x => x.ImageSrc, "/logo.svg").Add(x => x.CenterScale, scale)); // 25 units

        var image = cut.Find("svg > image");
        Assert.Equal("/logo.svg", image.GetAttribute("href"));
        Assert.Equal(position, image.GetAttribute("x"));
        Assert.Equal(position, image.GetAttribute("y"));
        Assert.Equal(side, image.GetAttribute("width"));
        Assert.Equal(side, image.GetAttribute("height"));
    }

    [Fact]
    public void Modules_under_a_center_image_are_left_out()
    {
        var value = "https://blaiz.io/docs/components/qr-code";
        var plain = Qr(value, p => p.Add(x => x.ErrorCorrection, QrCodeErrorCorrection.High));
        var cut = Qr(value, p => p
            .Add(x => x.ErrorCorrection, QrCodeErrorCorrection.High)
            .Add(x => x.ImageSrc, "/logo.svg"));

        var image = cut.Find("svg > image");
        var from = double.Parse(image.GetAttribute("x")!, CultureInfo.InvariantCulture);
        var to = from + double.Parse(image.GetAttribute("width")!, CultureInfo.InvariantCulture);

        var kept = Drawn(cut);
        Assert.True(kept.Count < Drawn(plain).Count);
        Assert.True(kept.IsSubsetOf(Drawn(plain)));
        // Nothing drawn touches the image.
        Assert.DoesNotContain(kept, m => m.X + 1 > from && m.X < to && m.Y + 1 > from && m.Y < to);
        // The eyes are never cleared: without them nothing scans.
        Assert.Equal(Eyes(plain).GetAttribute("d"), Eyes(cut).GetAttribute("d"));
    }

    [Fact]
    public void Excavate_off_keeps_every_module_under_the_image()
    {
        var plain = Qr("https://blaiz.io");
        var cut = Qr("https://blaiz.io", p => p.Add(x => x.ImageSrc, "/logo.svg").Add(x => x.Excavate, false));

        Assert.Equal(Modules(plain).GetAttribute("d"), Modules(cut).GetAttribute("d"));
    }

    [Fact]
    public void Center_content_is_laid_over_the_symbol_and_clears_the_modules_beneath()
    {
        var plain = Qr("https://blaiz.io");
        var cut = Qr("https://blaiz.io", p => p
            .Add(x => x.CenterScale, 0.3)
            .Add(x => x.CenterContent, "<b id=\"mark\">B</b>"));

        var mark = cut.Find("#mark");
        var box = mark.ParentElement!;
        Assert.Contains("width:30%", box.GetAttribute("style"));
        Assert.Contains("height:30%", box.GetAttribute("style"));
        // Decoration: the image already has its name.
        Assert.Equal("true", box.ParentElement!.GetAttribute("aria-hidden"));
        Assert.True(Drawn(cut).Count < Drawn(plain).Count);
    }

    // ---- the box ------------------------------------------------------------------------------

    [Fact]
    public void Class_and_other_attributes_reach_the_box()
    {
        var cut = Qr("A", p => p
            .Add(x => x.Class, "size-40 rounded-lg")
            .AddUnmatched("id", "ticket")
            .AddUnmatched("data-testid", "qr"));

        var root = cut.Find("[data-slot=qr-code]");
        Assert.Equal("ticket", root.Id);
        Assert.Equal("qr", root.GetAttribute("data-testid"));
        Assert.Contains("size-40", root.ClassList);
        Assert.Contains("rounded-lg", root.ClassList);
        Assert.Contains("bz-qr-code", root.ClassList);
    }
}
