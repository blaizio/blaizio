using Blaizio.Ui;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>GradientValue: the gradient the color picker edits, written as CSS and read back.</summary>
public class GradientValueTests
{
    private static readonly GradientValue RedToBlue =
        new(GradientType.Linear, 90, [new("#ff0000", 0), new("#0000ff", 1)]);

    private static GradientValue Parse(string text)
    {
        Assert.True(GradientValue.TryParse(text, out var gradient), $"'{text}' did not parse");
        return gradient;
    }

    private static (string Color, double Position)[] Stops(GradientValue gradient) =>
        [.. gradient.Stops.Select(stop => (stop.Color, Math.Round(stop.Position, 6)))];

    // ---- writing ------------------------------------------------------------------------------

    [Fact]
    public void A_color_becomes_a_ramp_to_its_own_transparent()
    {
        var gradient = GradientValue.FromColor("#ff0000");

        Assert.Equal(GradientType.Linear, gradient.Type);
        Assert.Equal(180, gradient.Angle);
        Assert.Equal([("#ff0000", 0d), ("#ff000000", 1d)], Stops(gradient));
        Assert.Equal("linear-gradient(180deg, #ff0000 0%, #ff000000 100%)", gradient.ToCss());
    }

    [Fact]
    public void A_color_that_cannot_be_read_still_gets_a_transparent_end()
    {
        var gradient = GradientValue.FromColor("tomato");

        Assert.Equal([("tomato", 0d), ("#00000000", 1d)], Stops(gradient));
    }

    [Theory]
    [InlineData(GradientType.Linear, "linear-gradient(90deg, #ff0000 0%, #0000ff 100%)")]
    [InlineData(GradientType.Radial, "radial-gradient(circle at center, #ff0000 0%, #0000ff 100%)")]
    [InlineData(GradientType.Angular, "conic-gradient(from 90deg at center, #ff0000 0%, #0000ff 100%)")]
    [InlineData(GradientType.Diamond,
        "linear-gradient(to top left, #ff0000 0%, #0000ff 100%) left top / 50% 50% no-repeat, " +
        "linear-gradient(to top right, #ff0000 0%, #0000ff 100%) right top / 50% 50% no-repeat, " +
        "linear-gradient(to bottom left, #ff0000 0%, #0000ff 100%) left bottom / 50% 50% no-repeat, " +
        "linear-gradient(to bottom right, #ff0000 0%, #0000ff 100%) right bottom / 50% 50% no-repeat")]
    public void Each_shape_has_its_own_paint(GradientType type, string css)
    {
        var gradient = RedToBlue with { Type = type };

        Assert.Equal(css, gradient.ToCss());
        Assert.Equal(css, gradient.ToString());
    }

    [Fact]
    public void The_ramp_preview_ignores_shape_and_angle()
    {
        var gradient = RedToBlue with { Type = GradientType.Angular, Angle = 33 };

        Assert.Equal("linear-gradient(to right, #ff0000 0%, #0000ff 100%)", gradient.ToRampCss());
    }

    [Fact]
    public void Stops_are_painted_in_ramp_order_however_they_are_stored()
    {
        var gradient = new GradientValue(GradientType.Linear, 0,
            [new("#0000ff", 1), new("#00ff00", 0.335), new("#ff0000", 0)]);

        Assert.Equal("linear-gradient(0deg, #ff0000 0%, #00ff00 33.5%, #0000ff 100%)", gradient.ToCss());
        Assert.Equal(["#ff0000", "#00ff00", "#0000ff"], gradient.Sorted.Select(stop => stop.Color));
        // The stored order is the editor's (a stop keeps its index while it is dragged past another).
        Assert.Equal("#0000ff", gradient.Stops[0].Color);
    }

    [Fact]
    public void Angles_are_written_to_two_decimals()
    {
        Assert.Equal("linear-gradient(12.35deg, #ff0000 0%, #0000ff 100%)", (RedToBlue with { Angle = 12.3456 }).ToCss());
    }

    [Theory]
    [InlineData("hsb(0 100% 100%)", "rgb(255 0 0)")]
    [InlineData("hsb(0 100% 100% / 0.5)", "rgb(255 0 0 / 0.5)")]
    [InlineData("device-cmyk(0% 100% 100% 0%)", "rgb(255 0 0)")]
    public void A_stop_in_a_format_no_browser_paints_is_painted_as_rgb_and_kept_as_it_is(string stop, string painted)
    {
        var gradient = RedToBlue.WithStopColor(0, stop);

        Assert.Equal($"linear-gradient(90deg, {painted} 0%, #0000ff 100%)", gradient.ToCss());
        Assert.Equal($"linear-gradient(to right, {painted} 0%, #0000ff 100%)", gradient.ToRampCss());
        Assert.Equal(stop, gradient.Stops[0].Color);
    }

    [Fact]
    public void A_stop_in_a_format_browsers_paint_is_written_as_it_is()
    {
        var gradient = RedToBlue.WithStopColor(0, "oklch(0.7 0.1 50)").WithStopColor(1, "hsl(240 100% 50% / 0.5)");

        Assert.Equal("linear-gradient(90deg, oklch(0.7 0.1 50) 0%, hsl(240 100% 50% / 0.5) 100%)", gradient.ToCss());
    }

    // ---- reading ------------------------------------------------------------------------------

    [Theory]
    [InlineData(GradientType.Linear, 45)]
    [InlineData(GradientType.Linear, 0)]
    [InlineData(GradientType.Angular, 270)]
    [InlineData(GradientType.Angular, 0)]
    // Rings have no direction: the angle comes back as the default.
    [InlineData(GradientType.Radial, 180)]
    [InlineData(GradientType.Diamond, 180)]
    public void What_is_written_reads_back_the_same(GradientType type, double angle)
    {
        var gradient = new GradientValue(type, angle,
            [new("#ff0000", 0), new("rgba(0, 255, 0, 0.5)", 0.25), new("hsl(240 100% 50%)", 1)]);

        var back = Parse(gradient.ToCss());

        Assert.Equal(type, back.Type);
        Assert.Equal(angle, back.Angle);
        Assert.Equal(Stops(gradient), Stops(back));
        Assert.Equal(gradient.ToCss(), back.ToCss());
    }

    [Theory]
    [InlineData("45deg", 45)]
    [InlineData("-45deg", -45)]
    [InlineData("0.25turn", 90)]
    [InlineData("100grad", 90)]
    [InlineData("3.14159265rad", 180)]
    [InlineData("90DEG", 90)]
    [InlineData("to top", 0)]
    [InlineData("to right", 90)]
    [InlineData("to bottom", 180)]
    [InlineData("to left", 270)]
    [InlineData("to top right", 45)]
    [InlineData("to right top", 45)]
    [InlineData("to bottom right", 135)]
    [InlineData("to bottom left", 225)]
    [InlineData("to left top", 315)]
    [InlineData("TO LEFT", 270)]
    public void A_linear_direction_is_read_in_any_unit_or_keyword(string direction, double degrees)
    {
        var gradient = Parse($"linear-gradient({direction}, #ff0000, #0000ff)");

        Assert.Equal(GradientType.Linear, gradient.Type);
        Assert.Equal(degrees, gradient.Angle, 4);
        Assert.Equal([("#ff0000", 0d), ("#0000ff", 1d)], Stops(gradient));
    }

    [Fact]
    public void A_linear_gradient_with_no_direction_runs_top_to_bottom()
    {
        Assert.Equal(180, Parse("linear-gradient(#ff0000, #0000ff)").Angle);
    }

    [Theory]
    [InlineData("conic-gradient(#ff0000, #0000ff)", 0)]
    [InlineData("conic-gradient(at center, #ff0000, #0000ff)", 0)]
    [InlineData("conic-gradient(from 90deg, #ff0000, #0000ff)", 90)]
    [InlineData("conic-gradient(from 0.5turn at 25% 25%, #ff0000, #0000ff)", 180)]
    [InlineData("CONIC-GRADIENT(FROM 45DEG AT CENTER, #ff0000, #0000ff)", 45)]
    public void A_sweep_starts_from_its_own_angle_or_from_the_top(string css, double degrees)
    {
        var gradient = Parse(css);

        Assert.Equal(GradientType.Angular, gradient.Type);
        Assert.Equal(degrees, gradient.Angle, 4);
        Assert.Equal(2, gradient.Stops.Count);
    }

    [Theory]
    [InlineData("radial-gradient(#ff0000, #0000ff)")]
    [InlineData("radial-gradient(circle, #ff0000, #0000ff)")]
    [InlineData("radial-gradient(ellipse at top left, #ff0000, #0000ff)")]
    [InlineData("radial-gradient(at 20% 30%, #ff0000, #0000ff)")]
    public void Rings_are_read_with_or_without_a_shape(string css)
    {
        var gradient = Parse(css);

        Assert.Equal(GradientType.Radial, gradient.Type);
        Assert.Equal([("#ff0000", 0d), ("#0000ff", 1d)], Stops(gradient));
    }

    [Fact]
    public void Stops_without_a_position_are_spread_evenly()
    {
        var gradient = Parse("linear-gradient(90deg, #ff0000, #00ff00, #0000ff, #ffffff, #000000)");

        Assert.Equal([0, 0.25, 0.5, 0.75, 1], gradient.Stops.Select(stop => stop.Position));
    }

    [Fact]
    public void A_stop_with_a_position_keeps_it_among_ones_without()
    {
        var gradient = Parse("linear-gradient(90deg, #ff0000, #00ff00 30%, #0000ff)");

        Assert.Equal([("#ff0000", 0d), ("#00ff00", 0.3), ("#0000ff", 1d)], Stops(gradient));
    }

    [Fact]
    public void Colors_written_as_functions_keep_their_commas_and_their_text()
    {
        var gradient = Parse(
            "linear-gradient(90deg, rgba(255, 0, 0, 0.5) 10%, hsl(120 100% 50% / 0.5) 50%, rgb(0 0 255) 90%)");

        Assert.Equal(
            [("rgba(255, 0, 0, 0.5)", 0.1), ("hsl(120 100% 50% / 0.5)", 0.5), ("rgb(0 0 255)", 0.9)],
            Stops(gradient));
    }

    [Fact]
    public void A_position_may_be_a_bare_fraction()
    {
        Assert.Equal([0.2, 0.8], Parse("linear-gradient(#ff0000 0.2, #0000ff 0.8)").Stops.Select(stop => stop.Position));
    }

    [Fact]
    public void Positions_past_either_end_are_brought_to_the_end()
    {
        Assert.Equal([0, 1], Parse("linear-gradient(#ff0000 -20%, #0000ff 150%)").Stops.Select(stop => stop.Position));
    }

    [Fact]
    public void Stops_written_out_of_order_are_read_in_ramp_order()
    {
        var gradient = Parse("linear-gradient(90deg, #0000ff 100%, #ff0000 0%, #00ff00 50%)");

        Assert.Equal(["#ff0000", "#00ff00", "#0000ff"], gradient.Stops.Select(stop => stop.Color));
    }

    [Fact]
    public void Surrounding_space_is_ignored()
    {
        Assert.Equal(90, Parse("   linear-gradient( 90deg ,  #ff0000  0% ,  #0000ff  100% )  ").Angle);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#ff0000")]
    [InlineData("none")]
    [InlineData("linear-gradient")]
    [InlineData("linear-gradient(")]
    [InlineData("linear-gradient()")]
    [InlineData("linear-gradient(90deg)")]
    [InlineData("linear-gradient(90deg, #ff0000)")] // one end is not a ramp
    [InlineData("linear-gradient(90deg, red, blue)")] // named colors are not read
    [InlineData("linear-gradient(90deg, #ff0000 0%, #0000ff 100%")]
    [InlineData("repeating-linear-gradient(90deg, #ff0000, #0000ff)")]
    [InlineData("url(photo.png)")]
    public void What_is_not_a_gradient_is_refused_and_leaves_a_usable_default(string? text)
    {
        Assert.False(GradientValue.TryParse(text, out var gradient));
        Assert.Equal(GradientType.Linear, gradient.Type);
        Assert.Equal(2, gradient.Stops.Count);
    }

    // ---- editing ------------------------------------------------------------------------------

    [Fact]
    public void Recoloring_a_stop_changes_that_stop_only()
    {
        var gradient = RedToBlue.WithStopColor(1, "#00ff00");

        Assert.Equal([("#ff0000", 0d), ("#00ff00", 1d)], Stops(gradient));
        Assert.Equal([("#ff0000", 0d), ("#0000ff", 1d)], Stops(RedToBlue));
    }

    [Fact]
    public void Moving_a_stop_keeps_its_index_and_stays_on_the_ramp()
    {
        Assert.Equal([0.4, 1], RedToBlue.WithStopPosition(0, 0.4).Stops.Select(stop => stop.Position));
        Assert.Equal([1, 1], RedToBlue.WithStopPosition(0, 7).Stops.Select(stop => stop.Position));
        Assert.Equal([0, 0], RedToBlue.WithStopPosition(1, -7).Stops.Select(stop => stop.Position));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void An_index_that_is_not_a_stop_changes_nothing(int index)
    {
        Assert.Same(RedToBlue, RedToBlue.WithStopColor(index, "#00ff00"));
        Assert.Same(RedToBlue, RedToBlue.WithStopPosition(index, 0.5));
        Assert.Same(RedToBlue, RedToBlue.WithStopRemoved(index));
    }

    [Fact]
    public void A_new_stop_takes_the_color_the_ramp_has_there()
    {
        var gradient = RedToBlue.WithStopAdded(0.5, out var index);

        Assert.Equal(1, index);
        Assert.Equal([("#ff0000", 0d), ("#800080", 0.5), ("#0000ff", 1d)], Stops(gradient));
        // The gradient it was added to is untouched.
        Assert.Equal(2, RedToBlue.Stops.Count);
    }

    [Theory]
    [InlineData(0, 1, 0, "#ff0000")]
    [InlineData(1, 2, 1, "#0000ff")]
    [InlineData(-3, 1, 0, "#ff0000")] // before the ramp: at its start
    [InlineData(9, 2, 1, "#0000ff")]  // past the ramp: at its end
    public void A_new_stop_at_or_past_an_end_lands_on_the_ramp(double position, int index, double landed, string color)
    {
        var gradient = RedToBlue.WithStopAdded(position, out var at);

        Assert.Equal(index, at);
        Assert.Equal(3, gradient.Stops.Count);
        Assert.Equal(landed, gradient.Stops[at].Position);
        Assert.Equal(color, gradient.Stops[at].Color);
    }

    [Fact]
    public void A_ramp_keeps_two_stops_at_least()
    {
        var three = RedToBlue.WithStopAdded(0.5, out _);

        var two = three.WithStopRemoved(1);
        Assert.Equal([("#ff0000", 0d), ("#0000ff", 1d)], Stops(two));

        Assert.Same(two, two.WithStopRemoved(0));
        Assert.Same(two, two.WithStopRemoved(1));
    }

    // ---- sampling -----------------------------------------------------------------------------

    [Theory]
    [InlineData(0, "#ff0000")]
    [InlineData(0.25, "#bf0040")]
    [InlineData(0.5, "#800080")]
    [InlineData(1, "#0000ff")]
    [InlineData(-1, "#ff0000")]
    [InlineData(2, "#0000ff")]
    public void The_ramp_is_mixed_channel_by_channel(double position, string color)
    {
        Assert.Equal(color, RedToBlue.ColorAt(position));
    }

    [Fact]
    public void Alpha_is_mixed_along_with_the_color()
    {
        var fade = GradientValue.FromColor("#ff0000");

        Assert.Equal("#ff000080", fade.ColorAt(0.5));
        Assert.Equal("#ff000000", fade.ColorAt(1));
    }

    [Fact]
    public void Sampling_reads_stops_in_ramp_order_and_between_inner_stops()
    {
        var gradient = new GradientValue(GradientType.Linear, 0,
            [new("#0000ff", 1), new("#ffffff", 0.5), new("#000000", 0)]);

        Assert.Equal("#808080", gradient.ColorAt(0.25));
        Assert.Equal("#ffffff", gradient.ColorAt(0.5));
        Assert.Equal("#8080ff", gradient.ColorAt(0.75));
    }

    [Fact]
    public void A_stop_that_cannot_be_read_is_sampled_as_its_own_text()
    {
        var gradient = new GradientValue(GradientType.Linear, 0, [new("tomato", 0), new("#0000ff", 1)]);

        Assert.Equal("tomato", gradient.ColorAt(0.5));
    }

    [Fact]
    public void A_gradient_with_no_stops_samples_as_black()
    {
        Assert.Equal("#000000", new GradientValue(GradientType.Linear, 0, []).ColorAt(0.5));
    }
}
