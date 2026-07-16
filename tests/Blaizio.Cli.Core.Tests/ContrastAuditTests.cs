using Blaizio.Cli.Core.Styling.Accessibility;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class ContrastAuditTests
{
    [Theory]
    [InlineData("#000000", "#ffffff", 21.0)]
    [InlineData("#fff", "#fff", 1.0)]
    [InlineData("white", "black", 21.0)]
    [InlineData("rgb(255, 255, 255)", "rgb(0 0 0)", 21.0)]
    [InlineData("hsl(0 0% 100%)", "hsl(0, 0%, 0%)", 21.0)]
    [InlineData("oklch(1 0 0)", "oklch(0 0 0)", 21.0)]
    public void Parses_common_syntaxes_and_measures(string fg, string bg, double expected)
    {
        var a = CssColor.Parse(fg);
        var b = CssColor.Parse(bg);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(expected, CssColor.Contrast(a.Value, b.Value), 1);
    }

    [Fact]
    public void Oklch_matches_known_reference()
    {
        // Nova dark primary on nova dark background - browser-verified at ~4.57.
        var primary = CssColor.Parse("oklch(0.61 0.2 304)")!.Value;
        var background = CssColor.Parse("oklch(0.176 0.017 302)")!.Value;
        Assert.Equal(4.57, CssColor.Contrast(primary, background), 1);
    }

    [Fact]
    public void Unsupported_values_return_null()
    {
        Assert.Null(CssColor.Parse("var(--primary)"));
        Assert.Null(CssColor.Parse("linear-gradient(red, blue)"));
        Assert.Null(CssColor.Parse("color-mix(in oklab, red, blue)"));
    }

    [Fact]
    public void MixBlack_reproduces_the_dark_fill_formula()
    {
        // oklab mix with black scales L: nova dark primary 0.61 * 0.85 = 0.5185 - the value the
        // browser computed for the button's dark fill.
        var primary = CssColor.Parse("oklch(0.61 0.2 304)")!.Value;
        var white = CssColor.Parse("oklch(0.985 0.005 304)")!.Value;
        var fill = primary.MixBlackOklab(0.85);
        // Browser-measured contrast for the derived fill under the near-white label: ~5.7.
        Assert.Equal(5.7, CssColor.Contrast(white, fill), 0);
    }

    private const string PassingTokens =
        """
        :root {
          --background: oklch(1 0 0);
          --foreground: oklch(0.2 0 0);
          --primary: oklch(0.45 0.2 304);
          --primary-foreground: oklch(0.99 0 0);
          --muted: oklch(0.96 0 0);
          --muted-foreground: oklch(0.45 0 0);
          --ring: oklch(0.45 0.2 304);
        }

        .dark {
          --background: oklch(0.15 0 0);
          --foreground: oklch(0.98 0 0);
          --primary: oklch(0.65 0.18 304);
          --primary-foreground: oklch(0.98 0 0);
          --muted: oklch(0.25 0 0);
          --muted-foreground: oklch(0.75 0 0);
          --ring: oklch(0.65 0.18 304);
        }
        """;

    [Fact]
    public void A_clean_palette_passes_everything_it_defines()
    {
        var report = ContrastAudit.Run(PassingTokens);

        Assert.True(report.AllPass);
        Assert.Empty(report.Unparsed);
        // Pairs whose tokens are absent (card, sidebar, statuses) are skipped, not failed.
        Assert.DoesNotContain(report.Findings, f => f.Label.Contains("card"));
        // Both modes measured, including the derived dark button fill.
        Assert.Contains(report.Findings, f => f is { Mode: "light", Label: "foreground on background" });
        Assert.Contains(report.Findings, f => f is { Mode: "dark", Label: "foreground on background" });
        Assert.Contains(report.Findings, f => f.Label.Contains("button fill"));
        Assert.Contains(report.Findings, f => f is { Label: "ring on background (focus)", Required: 3.0 });
    }

    [Fact]
    public void A_washed_out_customization_fails_with_the_right_pairs()
    {
        // The classic re-theme mistake: a pale primary under white text.
        var css = PassingTokens.Replace(
            "--primary: oklch(0.45 0.2 304);", "--primary: oklch(0.8 0.1 304);");

        var report = ContrastAudit.Run(css);

        Assert.False(report.AllPass);
        var failure = report.Findings.Single(f => f is { Mode: "light", Label: "primary-foreground on primary" });
        Assert.False(failure.Pass);
        Assert.False(failure.Advisory); // light raw fill is a hard requirement
        Assert.True(failure.Ratio < 2.5);
    }

    [Fact]
    public void Dark_raw_primary_fill_is_advisory_not_a_failure()
    {
        // The design tension the audit must not cry wolf about: dark primary bright enough for
        // link text on the page background reads under 4.5 as a RAW fill beneath its label. The
        // button derives a deeper fill (checked separately); the raw pair reports as advisory.
        var report = ContrastAudit.Run(PassingTokens.Replace(
            "--primary-foreground: oklch(0.98 0 0);", "--primary-foreground: oklch(0.99 0 0);"));

        var raw = report.Findings.Single(f => f is { Mode: "dark", Label: "primary-foreground on primary" });
        Assert.True(raw.Advisory);
        Assert.True(report.AllPass); // advisory misses never fail the audit
        var derived = report.Findings.Single(f => f.Label.Contains("button fill"));
        Assert.False(derived.Advisory); // the fill the button actually paints IS a requirement
    }

    [Fact]
    public void Dark_inherits_root_values_for_tokens_it_does_not_override()
    {
        // No .dark --ring: the audit must fall back to the :root value (CSS inheritance) - here
        // the light purple ring fails 3:1 against the dark background it inherits into.
        var css =
            """
            :root {
              --background: oklch(1 0 0);
              --ring: oklch(0.9 0.05 304);
            }

            .dark {
              --background: oklch(0.15 0 0);
            }
            """;

        var report = ContrastAudit.Run(css);

        Assert.False(report.Findings.Single(f => f.Mode == "light").Pass); // 0.9 on white: invisible
        Assert.True(report.Findings.Single(f => f.Mode == "dark").Pass);   // inherited 0.9 on dark: fine
    }

    [Fact]
    public void Var_indirection_is_followed_and_junk_is_reported_unchecked()
    {
        var css =
            """
            :root {
              --background: oklch(1 0 0);
              --brand: oklch(0.4 0.1 250);
              --primary: var(--brand);
              --primary-foreground: white;
              --ring: url(nope.png);
            }
            """;

        var report = ContrastAudit.Run(css);

        Assert.True(report.Findings.Single(f => f is { Mode: "light", Label: "primary-foreground on primary" }).Pass);
        Assert.Contains(report.Unparsed, u => u.Contains("--ring"));
    }
}
