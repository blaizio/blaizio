using System.Globalization;
using Bunit;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The aspect-ratio box: a wrapper whose bottom padding encodes the ratio as a percentage of its
/// width, and an inner element that fills it. Both are inline styles, so a wrong number here is a
/// wrong shape on screen.
/// </summary>
public class AspectRatioRenderTests : BunitContext
{
    private static string Padding(IRenderedComponent<BaseAspectRatio> cut) =>
        cut.Find("[data-bz-aspect-ratio-wrapper]").GetAttribute("style")!.Split("padding-bottom:")[1];

    [Fact]
    public void Defaults_to_a_square()
    {
        var cut = Render<BaseAspectRatio>();

        var wrapper = cut.Find("[data-bz-aspect-ratio-wrapper]");
        Assert.Equal("position:relative;width:100%;padding-bottom:100%", wrapper.GetAttribute("style"));
    }

    [Theory]
    [InlineData(16.0 / 9, "56.25%")]
    [InlineData(4.0 / 3, "75%")]
    [InlineData(2, "50%")]
    [InlineData(0.5, "200%")]
    [InlineData(21.0 / 9, "42.8571%")]
    public void The_ratio_becomes_the_height_as_a_share_of_the_width(double ratio, string expected)
    {
        var cut = Render<BaseAspectRatio>(p => p.Add(x => x.Ratio, ratio));

        Assert.Equal(expected, Padding(cut));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(double.NaN)]
    public void A_ratio_that_is_not_a_positive_number_falls_back_to_a_square(double ratio)
    {
        var cut = Render<BaseAspectRatio>(p => p.Add(x => x.Ratio, ratio));

        Assert.Equal("100%", Padding(cut));
    }

    [Fact]
    public void The_percentage_uses_a_dot_whatever_the_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var cut = Render<BaseAspectRatio>(p => p.Add(x => x.Ratio, 16.0 / 9));

            // "56,25%" is not a length: the browser would drop the declaration and the box collapse.
            Assert.Equal("56.25%", Padding(cut));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void The_content_fills_the_box_and_takes_class_style_and_attributes()
    {
        var cut = Render<BaseAspectRatio>(p => p
            .Add(x => x.Ratio, 16.0 / 9)
            .Add(x => x.Class, "overflow-hidden rounded")
            .Add(x => x.Style, "background:black")
            .AddUnmatched("data-slot", "aspect-ratio")
            .AddChildContent("<img src=\"/cover.jpg\" alt=\"Cover\" />"));

        var content = cut.Find("[data-bz-aspect-ratio-wrapper] > div");
        Assert.Equal("overflow-hidden rounded", content.GetAttribute("class"));
        Assert.Equal("aspect-ratio", content.GetAttribute("data-slot"));
        var style = content.GetAttribute("style")!;
        Assert.Contains("position:absolute", style);
        Assert.Contains("inset:0", style);
        Assert.Contains("background:black", style);
        Assert.Equal("/cover.jpg", content.QuerySelector("img")!.GetAttribute("src"));
        // The consumer's class and attributes go on the content, never on the sizing wrapper.
        var wrapper = cut.Find("[data-bz-aspect-ratio-wrapper]");
        Assert.False(wrapper.HasAttribute("class"));
        Assert.False(wrapper.HasAttribute("data-slot"));
    }
}
