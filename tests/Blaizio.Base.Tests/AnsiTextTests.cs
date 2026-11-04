using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>ANSI terminal output parsing: SGR styles, dropped escapes, rewinds and streaming tails.</summary>
public class AnsiTextTests
{
    [Fact]
    public void Plain_text_is_one_unstyled_run()
    {
        var run = Assert.Single(AnsiText.ParseLine("Build succeeded."));

        Assert.Equal("Build succeeded.", run.Text);
        Assert.True(run.Style.IsDefault);
    }

    [Fact]
    public void Sgr_colors_split_the_line_into_runs()
    {
        var runs = AnsiText.ParseLine("\u001b[31merror\u001b[0m CS1002");

        Assert.Equal(2, runs.Count);
        Assert.Equal("error", runs[0].Text);
        Assert.Equal(1, runs[0].Style.Foreground?.Index);
        Assert.Equal(" CS1002", runs[1].Text);
        Assert.True(runs[1].Style.IsDefault);
    }

    [Fact]
    public void Bright_and_background_colors_map_to_named_indexes()
    {
        var run = Assert.Single(AnsiText.ParseLine("\u001b[92;44mpassed"));

        Assert.Equal(10, run.Style.Foreground?.Index);
        Assert.Equal(4, run.Style.Background?.Index);
    }

    [Fact]
    public void Decorations_switch_on_and_off()
    {
        var runs = AnsiText.ParseLine("\u001b[1;3mA\u001b[22mB\u001b[23;4mC\u001b[24;7;32m PASS \u001b[27mD");

        Assert.Equal(AnsiDecoration.Bold | AnsiDecoration.Italic, runs[0].Style.Decoration);
        Assert.Equal(AnsiDecoration.Italic, runs[1].Style.Decoration);
        Assert.Equal(AnsiDecoration.Underline, runs[2].Style.Decoration);
        Assert.Equal(AnsiDecoration.Inverse, runs[3].Style.Decoration);
        Assert.Equal(2, runs[3].Style.Foreground?.Index);
        Assert.Equal(AnsiDecoration.None, runs[4].Style.Decoration);
    }

    [Fact]
    public void Extended_colors_resolve_to_exact_rgb_or_a_named_color()
    {
        var runs = AnsiText.ParseLine("\u001b[38;5;196mA\u001b[38;2;10;20;30mB\u001b[38;5;3mC\u001b[48:2::1:2:3mD");

        Assert.False(runs[0].Style.Foreground!.Value.IsNamed);
        Assert.Equal("rgb(255 0 0)", runs[0].Style.Foreground!.Value.ToCss());
        Assert.Equal("rgb(10 20 30)", runs[1].Style.Foreground!.Value.ToCss());
        Assert.Equal(3, runs[2].Style.Foreground?.Index);
        Assert.Equal("rgb(1 2 3)", runs[3].Style.Background!.Value.ToCss());
    }

    [Fact]
    public void Other_escapes_are_dropped_and_their_text_kept()
    {
        var run = Assert.Single(AnsiText.ParseLine(
            "\u001b[2K\u001b]8;;https://example.com\u0007link\u001b]8;;\u001b\\ done\u0007"));

        Assert.Equal("link done", run.Text);
    }

    [Fact]
    public void A_carriage_return_keeps_only_the_last_frame()
    {
        Assert.Equal("100%", Assert.Single(AnsiText.ParseLine("10%\r50%\r100%")).Text);
        Assert.Equal("done", Assert.Single(AnsiText.ParseLine("done\r")).Text);
    }

    [Fact]
    public void Styles_do_not_carry_from_one_line_to_the_next()
    {
        var lines = AnsiText.ParseLines("\u001b[32mgreen\r\nplain");

        Assert.Equal(2, lines.Count);
        Assert.Equal(2, lines[0][0].Style.Foreground?.Index);
        Assert.True(Assert.Single(lines[1]).Style.IsDefault);
    }

    [Fact]
    public void An_unfinished_sequence_at_the_end_is_left_out()
    {
        Assert.Equal("ok ", Assert.Single(AnsiText.ParseLine("ok \u001b[3")).Text);
    }

    [Theory]
    [InlineData(16, 0, 0, 0)]
    [InlineData(231, 255, 255, 255)]
    [InlineData(232, 8, 8, 8)]
    [InlineData(255, 238, 238, 238)]
    public void The_256_color_palette_covers_the_cube_and_the_gray_ramp(int n, byte r, byte g, byte b)
    {
        var color = AnsiColor.FromPalette(n);

        Assert.False(color.IsNamed);
        Assert.Equal((r, g, b), (color.R, color.G, color.B));
    }
}
