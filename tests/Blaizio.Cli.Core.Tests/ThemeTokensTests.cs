using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class ThemeTokensTests
{
    [Theory]
    [InlineData("oklch(0.62 0.13 155)", 0.62, 0.13, 155)]
    [InlineData("oklch(62% .13 155)", 0.62, 0.13, 155)]
    [InlineData("oklch(0.985 0 0)", 0.985, 0, 0)]
    public void Oklch_parses_unit_and_percent(string css, double l, double c, double h)
    {
        Assert.True(OklchColor.TryParse(css, out var color));
        Assert.Equal(l, color.L, 4);
        Assert.Equal(c, color.C, 4);
        Assert.Equal(h, color.H, 1);
    }

    [Theory]
    [InlineData("rgb(1 2 3)")]
    [InlineData("not a color")]
    [InlineData("oklch()")]
    [InlineData(null)]
    public void Oklch_rejects_non_oklch(string? css)
    {
        Assert.False(OklchColor.TryParse(css, out _));
    }

    [Fact]
    public void Contrast_matches_known_pairs()
    {
        // The audited zenith pairs: white on the old mid-green primary was 3.29, the flipped
        // pairing 8.58 - the reference numbers the palette work was verified against.
        var oldPrimary = new OklchColor(0.62, 0.13, 155);
        var white = new OklchColor(0.985, 0.005, 155);
        Assert.Equal(3.29, OklchColor.Contrast(oldPrimary, white), 2);

        var newPrimary = new OklchColor(0.74, 0.14, 155);
        var label = new OklchColor(0.18, 0.035, 160);
        Assert.Equal(8.58, OklchColor.Contrast(newPrimary, label), 2);
    }

    [Fact]
    public void Derived_foreground_always_clears_AA()
    {
        // Sweep the surface space: every derived pairing must be >= 4.5 including the worst
        // mid-lightness band.
        for (var l = 0.05; l <= 0.95; l += 0.05)
        for (var h = 0; h < 360; h += 30)
        {
            var surface = new OklchColor(l, 0.15, h);
            var fg = ThemeTokens.DeriveForeground(surface);
            Assert.True(OklchColor.Contrast(surface, fg) >= 4.5,
                $"AA failed for {surface.ToCss()}: {OklchColor.Contrast(surface, fg):0.00}");
        }
    }

    [Fact]
    public void BuildCss_writes_per_mode_blocks_with_derived_partners()
    {
        var css = ThemeTokens.BuildCss(
        [
            new TokenOverride("primary", Dark: false, new OklchColor(0.5, 0.13, 155)),
            new TokenOverride("border", Dark: true, new OklchColor(0.3, 0.03, 245)),
        ]);

        Assert.Contains(":root:not(.dark) {", css);
        Assert.Contains(":root.dark {", css);
        Assert.Contains("--primary: oklch(0.5 0.13 155) !important;", css);
        Assert.Contains("--primary-foreground:", css);
        Assert.Contains("--ring: oklch(0.5 0.13 155) !important;", css);
        Assert.Contains("--border: oklch(0.3 0.03 245) !important;", css);
        // border has no text partner - nothing derived for it
        Assert.DoesNotContain("--border-foreground", css);
    }

    [Fact]
    public void BuildCss_of_nothing_is_empty()
    {
        Assert.Equal("", ThemeTokens.BuildCss([]));
    }
}
