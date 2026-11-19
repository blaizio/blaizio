using System.Globalization;
using AngleSharp.Dom;
using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// The geometry BzChart draws from: the value domain and its ticks, bands and numeric x, stacking,
/// reading direction and the hover parts. The layout type is internal, so it is read off the drawing.
/// </summary>
public class ChartLayoutTests : BunitContext
{
    public ChartLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private sealed record Row(string Name, double A, double B = 0, double X = 0);

    private static List<Row> Rows(params double[] values) =>
        [.. values.Select((value, i) => new Row($"R{i + 1}", value))];

    // The default drawing is 640 by 360: with both axes the plot runs 42..632 across and 12..334 down.
    private const double Left = 42, Right = 632, Top = 12, Bottom = 334;

    private IRenderedComponent<BzChart<Row>> Chart(
        IReadOnlyList<Row>? data, Action<ComponentParameterCollectionBuilder<BzChart<Row>>> parts, bool axes = true) =>
        Render<BzChart<Row>>(p =>
        {
            p.Add(x => x.Data, data).Add(x => x.Label, row => row.Name);
            if (axes) p.AddChildContent<BzChartYAxis>().AddChildContent<BzChartXAxis>();
            parts(p);
        });

    private static void Bars(ComponentParameterCollectionBuilder<BzChart<Row>> p, bool second = false)
    {
        p.AddChildContent<BzChartBarSeries<Row>>(s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A));
        if (second) p.AddChildContent<BzChartBarSeries<Row>>(s => s.Add(x => x.Name, "B").Add(x => x.Value, row => row.B));
    }

    private static double Num(IElement element, string attribute) =>
        double.Parse(element.GetAttribute(attribute)!, CultureInfo.InvariantCulture);

    private static string[] Ticks(IRenderedComponent<BzChart<Row>> cut, string anchor) =>
        [.. cut.FindAll($"text.bz-chart-tick[text-anchor={anchor}]").Select(t => t.TextContent)];

    private static (double X, double Y, double W, double H)[] BarRects(IRenderedComponent<BzChart<Row>> cut) =>
        [.. cut.FindAll("rect.bz-chart-bar").Select(b => (Num(b, "x"), Num(b, "y"), Num(b, "width"), Num(b, "height")))];

    // ---- the value domain and its ticks -------------------------------------------------------

    [Theory]
    [InlineData(new double[] { 20, 100, 60 }, 4, new[] { "0", "50", "100" })]
    [InlineData(new double[] { 20, 100, 60 }, 5, new[] { "0", "20", "40", "60", "80", "100" })]
    [InlineData(new double[] { 3, 7 }, 4, new[] { "0", "2", "4", "6", "8" })]
    [InlineData(new double[] { 0.2, 0.9 }, 4, new[] { "0", "0.5", "1" })]
    [InlineData(new double[] { 1200, 4700 }, 4, new[] { "0", "2000", "4000", "6000" })]
    // Bars grow from zero, so zero stays in the domain when every value is below it.
    [InlineData(new double[] { -10, -30 }, 4, new[] { "-30", "-20", "-10", "0" })]
    [InlineData(new double[] { -50, 100 }, 4, new[] { "-50", "0", "50", "100" })]
    // Nothing to measure: the domain is made one unit tall rather than none.
    [InlineData(new double[] { 0, 0 }, 4, new[] { "0", "0.5", "1" })]
    [InlineData(new double[] { 5 }, 4, new[] { "0", "2", "4", "6" })]
    public void Ticks_are_round_numbers_that_cover_the_values(double[] values, int count, string[] ticks)
    {
        var cut = Render<BzChart<Row>>(p => p
            .Add(x => x.Data, Rows(values))
            .AddChildContent<BzChartYAxis>(a => a.Add(x => x.TickCount, count).Add(x => x.Formatter, v => v.ToString("0.###", CultureInfo.InvariantCulture)))
            .AddChildContent<BzChartBarSeries<Row>>(s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A)));

        Assert.Equal(ticks, Ticks(cut, "end"));
        Assert.DoesNotContain("NaN", cut.Markup);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_tick_count_below_one_still_draws_both_ends(int count)
    {
        var cut = Render<BzChart<Row>>(p => p
            .Add(x => x.Data, Rows(20, 100))
            .AddChildContent<BzChartYAxis>(a => a.Add(x => x.TickCount, count))
            .AddChildContent<BzChartBarSeries<Row>>(s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A)));

        Assert.Equal(["0", "100"], Ticks(cut, "end"));
    }

    [Fact]
    public void The_value_axis_formatter_writes_the_tick_labels()
    {
        var cut = Render<BzChart<Row>>(p => p
            .Add(x => x.Data, Rows(20, 100))
            .AddChildContent<BzChartYAxis>(a => a.Add(x => x.Formatter, v => $"${v:0}k"))
            .AddChildContent<BzChartBarSeries<Row>>(s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A)));

        Assert.Equal(["$0k", "$50k", "$100k"], Ticks(cut, "end"));
    }

    [Fact]
    public void Grid_lines_sit_on_the_ticks_across_the_plot()
    {
        var cut = Chart(Rows(20, 100, 60), p => { Bars(p); p.AddChildContent<BzChartGrid>(); });

        var lines = cut.FindAll("line.bz-chart-grid-line");
        Assert.Equal(3, lines.Count);
        Assert.All(lines, line => Assert.Equal((Left, Right), (Num(line, "x1"), Num(line, "x2"))));
        // 0 at the bottom of the plot, 100 at its top, 50 half way.
        Assert.Equal([Bottom, (Top + Bottom) / 2, Top], lines.Select(line => Num(line, "y1")));
    }

    [Fact]
    public void A_vertical_grid_separates_the_categories()
    {
        var cut = Chart(Rows(20, 100, 60, 40), p => { Bars(p); p.AddChildContent<BzChartGrid>(g => g.Add(x => x.Vertical, true)); });

        var columns = cut.FindAll("line.bz-chart-grid-line").Where(line => line.GetAttribute("x1") == line.GetAttribute("x2")).ToList();
        var band = (Right - Left) / 4;
        Assert.Equal([Left + band, Left + 2 * band, Left + 3 * band], columns.Select(line => Num(line, "x1")));
        Assert.All(columns, line => Assert.Equal((Top, Bottom), (Num(line, "y1"), Num(line, "y2"))));
    }

    [Fact]
    public void The_plot_takes_the_room_the_axes_leave()
    {
        var bare = Chart(Rows(100, 100), p => { Bars(p); p.AddChildContent<BzChartGrid>(); }, axes: false);

        var line = bare.FindAll("line.bz-chart-grid-line")[0];
        Assert.Equal((8d, 632d), (Num(line, "x1"), Num(line, "x2")));
        // No x axis: the plot reaches down to 8 from the bottom edge.
        Assert.Equal(352, Num(line, "y1"));
    }

    // ---- bars ---------------------------------------------------------------------------------

    [Fact]
    public void A_bar_is_as_tall_as_its_share_of_the_domain()
    {
        var bars = BarRects(Chart(Rows(100, 50, 0), p => Bars(p)));

        var plot = Bottom - Top;
        Assert.Equal([plot, plot / 2, 0], bars.Select(b => b.H));
        // Every bar ends on the zero line.
        Assert.All(bars, b => Assert.Equal(Bottom, b.Y + b.H, 2));
    }

    [Fact]
    public void A_single_bar_is_centered_in_the_plot()
    {
        var cut = Chart(Rows(40), p => Bars(p));

        var bar = Assert.Single(BarRects(cut));
        Assert.Equal((Left + Right) / 2, bar.X + bar.W / 2, 2);
        Assert.True(bar.H > 0);
        Assert.DoesNotContain("NaN", cut.Markup);
    }

    [Fact]
    public void Bars_below_zero_hang_from_the_zero_line()
    {
        var cut = Chart(Rows(-10, -30), p => Bars(p));

        var bars = BarRects(cut);
        var plot = Bottom - Top;
        // The domain is -30..0, so zero is the top of the plot.
        Assert.All(bars, b => Assert.Equal(Top, b.Y));
        Assert.Equal([Math.Round(plot / 3, 2), plot], bars.Select(b => b.H));
    }

    [Fact]
    public void Bars_on_both_sides_of_zero_meet_at_the_zero_line()
    {
        var bars = BarRects(Chart(Rows(100, -50), p => Bars(p)));

        // -50..100 over 322 units: zero is a third of the way up.
        var zero = Bottom - (Bottom - Top) / 3;
        Assert.Equal(zero, bars[0].Y + bars[0].H, 2);
        Assert.Equal(zero, bars[1].Y, 2);
        Assert.Equal(Top, bars[0].Y, 2);
        Assert.Equal(Bottom, bars[1].Y + bars[1].H, 2);
    }

    [Fact]
    public void Two_series_stand_side_by_side_inside_their_band()
    {
        List<Row> data = [new("Q1", 40, 80), new("Q2", 60, 20)];

        var bars = BarRects(Chart(data, p => Bars(p, second: true)));

        // Drawn band by band: A then B of the first, A then B of the second.
        Assert.Equal(4, bars.Length);
        var band = (Right - Left) / 2;
        Assert.True(bars[0].X + bars[0].W <= bars[1].X);
        Assert.InRange(bars[0].X, Left, Left + band);
        Assert.InRange(bars[1].X + bars[1].W, Left, Left + band);
        Assert.InRange(bars[2].X, Left + band, Right);
        // Both stand on the zero line: side by side, not on top of each other.
        Assert.All(bars, b => Assert.Equal(Bottom, b.Y + b.H, 2));
    }

    [Fact]
    public void Stacked_series_stand_on_each_other_and_the_domain_covers_the_total()
    {
        List<Row> data = [new("Q1", 30, 70), new("Q2", 10, 10)];

        var cut = Chart(data, p => { p.Add(x => x.Stacked, true); Bars(p, second: true); });

        // 30 + 70: the top tick is the total, not the tallest single value.
        Assert.Equal("100", Ticks(cut, "end")[^1]);
        var bars = BarRects(cut);
        Assert.Equal(bars[0].X, bars[1].X);
        Assert.Equal(bars[0].Y, bars[1].Y + bars[1].H, 2);
        Assert.Equal(Top, bars[1].Y, 2);
        Assert.Equal(Bottom, bars[0].Y + bars[0].H, 2);
    }

    [Fact]
    public void Stacked_values_below_zero_stack_downwards()
    {
        List<Row> data = [new("Q1", -30, -70), new("Q2", 50, 50)];

        var cut = Chart(data, p => { p.Add(x => x.Stacked, true); Bars(p, second: true); });

        Assert.Equal(["-100", "-50", "0", "50", "100"], Ticks(cut, "end"));
        var bars = BarRects(cut);
        var zero = (Top + Bottom) / 2;
        Assert.Equal(zero, bars[0].Y, 2);
        Assert.Equal(bars[0].Y + bars[0].H, bars[1].Y, 2);
        Assert.Equal(Bottom, bars[1].Y + bars[1].H, 2);
    }

    // ---- categories and reading direction -----------------------------------------------------

    [Fact]
    public void Category_labels_sit_under_their_bands()
    {
        var cut = Chart(Rows(1, 2, 3, 4), p => Bars(p));

        var labels = cut.FindAll("text.bz-chart-tick[text-anchor=middle]");
        Assert.Equal(["R1", "R2", "R3", "R4"], labels.Select(l => l.TextContent));
        var band = (Right - Left) / 4;
        Assert.Equal([Left + band / 2, Left + band * 1.5, Left + band * 2.5, Left + band * 3.5], labels.Select(l => Num(l, "x")));
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(11, 6)]  // every second label
    [InlineData(25, 9)]  // every third
    public void A_long_series_shows_only_every_nth_label(int categories, int labels)
    {
        var data = Enumerable.Range(1, categories).Select(i => new Row($"R{i}", i)).ToList();

        var cut = Chart(data, p => Bars(p));

        Assert.Equal(labels, cut.FindAll("text.bz-chart-tick[text-anchor=middle]").Count);
        Assert.Equal(categories, cut.FindAll("rect.bz-chart-bar").Count);
    }

    [Fact]
    public void Items_with_no_label_are_numbered()
    {
        var cut = Render<BzChart<Row>>(p => p
            .Add(x => x.Data, Rows(1, 2, 3))
            .AddChildContent<BzChartXAxis>()
            .AddChildContent<BzChartBarSeries<Row>>(s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A)));

        Assert.Equal(["1", "2", "3"], Ticks(cut, "middle"));
    }

    [Fact]
    public void Right_to_left_starts_the_categories_at_the_right_and_moves_the_value_axis_there()
    {
        var cut = Chart(Rows(10, 20, 30), p => { p.Add(x => x.Dir, Direction.Rtl); Bars(p); p.AddChildContent<BzChartGrid>(); });

        Assert.Equal("rtl", cut.Find("[data-slot=chart]").GetAttribute("dir"));
        var bars = BarRects(cut);
        Assert.True(bars[0].X > bars[1].X && bars[1].X > bars[2].X);
        // The gutter for the value labels is on the right now.
        var line = cut.FindAll("line.bz-chart-grid-line")[0];
        Assert.Equal((8d, 598d), (Num(line, "x1"), Num(line, "x2")));
        var ticks = cut.FindAll("text.bz-chart-tick[text-anchor=start]");
        Assert.Equal(["0", "10", "20", "30"], ticks.Select(t => t.TextContent));
        Assert.All(ticks, t => Assert.Equal(606, Num(t, "x")));
    }

    // ---- lines and areas ----------------------------------------------------------------------

    [Fact]
    public void A_line_runs_through_the_band_centers_at_the_values()
    {
        var cut = Chart(Rows(0, 100, 50), p => p.AddChildContent<BzChartLineSeries<Row>>(
            s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A)));

        var band = (Right - Left) / 3;
        Assert.Equal(
            $"M {F(Left + band / 2)} {F(Bottom)} L {F(Left + band * 1.5)} {F(Top)} L {F(Left + band * 2.5)} {F((Top + Bottom) / 2)}",
            cut.Find("path.bz-chart-line").GetAttribute("d"));
    }

    [Fact]
    public void A_line_over_one_item_is_a_single_point()
    {
        var cut = Chart(Rows(5), p => p.AddChildContent<BzChartLineSeries<Row>>(
            s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A).Add(x => x.Curved, true)));

        var d = cut.Find("path.bz-chart-line").GetAttribute("d")!;
        Assert.StartsWith($"M {F((Left + Right) / 2)} ", d);
        Assert.DoesNotContain("L", d);
        Assert.DoesNotContain("C", d);
        Assert.DoesNotContain("NaN", cut.Markup);
    }

    [Fact]
    public void A_curved_line_passes_through_every_point_and_never_overshoots_them()
    {
        // A plateau, a jump and a dip: where a plain spline would swing past the data.
        var cut = Chart(Rows(10, 10, 100, 100, 0, 40), p => p.AddChildContent<BzChartLineSeries<Row>>(
            s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A).Add(x => x.Curved, true)));
        var straight = Chart(Rows(10, 10, 100, 100, 0, 40), p => p.AddChildContent<BzChartLineSeries<Row>>(
            s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A)));

        var d = cut.Find("path.bz-chart-line").GetAttribute("d")!;
        var segments = d.Split(" C ");
        Assert.Equal(6, segments.Length); // the start and five curves

        // The end of each curve is the next data point, the same one the straight line goes to.
        var corners = straight.Find("path.bz-chart-line").GetAttribute("d")!.Replace("M ", "").Split(" L ");
        Assert.Equal(corners[0], segments[0].Replace("M ", ""));
        for (var i = 1; i < segments.Length; i++)
            Assert.Equal(corners[i], segments[i].Split(", ")[2]);

        // Every control point stays between the lowest and the highest value drawn.
        var ys = segments.Skip(1)
            .SelectMany(segment => segment.Split(", "))
            .Select(point => double.Parse(point.Split(' ')[1], CultureInfo.InvariantCulture));
        Assert.All(ys, y => Assert.InRange(y, Top, Bottom));
    }

    [Fact]
    public void An_area_is_its_line_closed_down_to_the_zero_line()
    {
        var cut = Chart(Rows(50, 100), p => p.AddChildContent<BzChartAreaSeries<Row>>(
            s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A).Add(x => x.Color, "#123456")));

        var band = (Right - Left) / 2;
        var (x1, x2) = (F(Left + band / 2), F(Left + band * 1.5));
        var line = $"M {x1} {F((Top + Bottom) / 2)} L {x2} {F(Top)}";
        Assert.Equal(line, cut.Find("path.bz-chart-line").GetAttribute("d"));
        var area = cut.Find("path.bz-chart-area");
        Assert.Equal($"{line} L {x2} {F(Bottom)} L {x1} {F(Bottom)} Z", area.GetAttribute("d"));
        Assert.Equal("#123456", area.GetAttribute("fill"));
    }

    [Fact]
    public void Stacked_areas_close_along_the_layer_beneath()
    {
        List<Row> data = [new("Q1", 20, 30), new("Q2", 40, 60)];

        var cut = Chart(data, p =>
        {
            p.Add(x => x.Stacked, true);
            p.AddChildContent<BzChartAreaSeries<Row>>(s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A));
            p.AddChildContent<BzChartAreaSeries<Row>>(s => s.Add(x => x.Name, "B").Add(x => x.Value, row => row.B));
        });

        Assert.Equal("100", Ticks(cut, "end")[^1]);
        var lines = cut.FindAll("path.bz-chart-line").Select(l => l.GetAttribute("d")!).ToList();
        var areas = cut.FindAll("path.bz-chart-area").Select(a => a.GetAttribute("d")!).ToList();
        // The upper layer's top is the running total.
        Assert.EndsWith($" {F(Top)}", lines[1]);
        // And its fill comes back along the lower layer's top, right to left.
        var lower = lines[0].Replace("M ", "").Split(" L ");
        Assert.Equal($"{lines[1]} L {lower[1]} L {lower[0]} Z", areas[1]);
    }

    [Fact]
    public void A_gradient_area_fills_from_its_own_definition()
    {
        var cut = Chart(Rows(50, 100), p => p.AddChildContent<BzChartAreaSeries<Row>>(
            s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A).Add(x => x.Gradient, true)));

        var gradient = cut.Find("svg defs linearGradient");
        Assert.Equal($"url(#{gradient.Id})", cut.Find("path.bz-chart-area-gradient").GetAttribute("fill"));
        Assert.All(gradient.Children, stop => Assert.Equal("var(--chart-1)", stop.GetAttribute("stop-color")));
    }

    // ---- a numeric x axis ---------------------------------------------------------------------

    private IRenderedComponent<BzChart<Row>> Scatter(params (double X, double Y)[] points) =>
        Render<BzChart<Row>>(p => p
            .Add(x => x.Data, [.. points.Select(point => new Row("", point.Y, X: point.X))])
            .Add(x => x.XValue, row => row.X)
            .Add(x => x.XFormatter, v => v.ToString("0.###", CultureInfo.InvariantCulture))
            .AddChildContent<BzChartYAxis>()
            .AddChildContent<BzChartXAxis>()
            .AddChildContent<BzChartTooltip>()
            .AddChildContent<BzChartScatterSeries<Row>>(s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A)));

    [Fact]
    public void Points_are_placed_by_their_x_value_between_round_ticks()
    {
        var cut = Scatter((0, 10), (2.5, 20), (10, 30));

        Assert.Equal(["0", "2", "4", "6", "8", "10"], Ticks(cut, "middle"));
        var points = cut.FindAll("circle.bz-chart-point");
        Assert.Equal([Left, Left + (Right - Left) / 4, Right], points.Select(point => Num(point, "cx")));
    }

    [Fact]
    public void An_x_axis_does_not_have_to_start_at_zero()
    {
        var cut = Scatter((1010, 10), (1090, 30));

        var ticks = Ticks(cut, "middle");
        Assert.Equal("1000", ticks[0]);
        Assert.Equal("1100", ticks[^1]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)] // every point at the same x
    public void Points_that_share_one_x_sit_in_the_middle(int count)
    {
        var cut = Scatter([.. Enumerable.Range(1, count).Select(i => (7d, i * 10d))]);

        Assert.DoesNotContain("NaN", cut.Markup);
        var points = cut.FindAll("circle.bz-chart-point");
        Assert.Equal(count, points.Count);
        Assert.All(points, point => Assert.Equal((Left + Right) / 2, Num(point, "cx"), 2));
        // The axis is given room on both sides of the lone value.
        Assert.Contains("7", Ticks(cut, "middle"));
        Assert.True(Ticks(cut, "middle").Length >= 2);
    }

    [Fact]
    public void An_x_that_is_not_a_number_is_placed_at_zero()
    {
        var cut = Scatter((double.NaN, 10), (10, 30));

        Assert.DoesNotContain("NaN", cut.Markup);
        Assert.Equal(Left, Num(cut.FindAll("circle.bz-chart-point")[0], "cx"));
    }

    [Fact]
    public void Hover_targets_on_a_numeric_axis_split_the_plot_half_way_between_neighbours()
    {
        // Out of order on purpose: the strips follow the positions, not the list.
        var cut = Scatter((10, 1), (0, 2), (4, 3));

        var strips = cut.FindAll("rect.bz-chart-hit").Select(s => (X: Num(s, "x"), W: Num(s, "width"))).ToList();
        var unit = (Right - Left) / 10;
        Assert.Equal(
            [(Left, 2 * unit), (Left + 2 * unit, 5 * unit), (Left + 7 * unit, 3 * unit)],
            strips.Select(s => (Math.Round(s.X, 2), Math.Round(s.W, 2))));
        // Together they cover the plot exactly once.
        Assert.Equal(Right - Left, strips.Sum(s => s.W), 2);
    }

    [Fact]
    public void A_tooltip_on_a_numeric_axis_is_labelled_with_the_formatted_x()
    {
        var cut = Scatter((0, 10), (2.5, 20), (10, 30));

        cut.FindAll("rect.bz-chart-hit")[1].TriggerEvent("onpointerenter", new PointerEventArgs());

        Assert.Equal("2.5", cut.Find(".bz-chart-tooltip-label").TextContent);
    }

    // ---- hover --------------------------------------------------------------------------------

    private IRenderedComponent<BzChart<Row>> Hoverable(IReadOnlyList<Row> data, bool line = false) =>
        Chart(data, p =>
        {
            p.Add(x => x.ValueFormatter, v => $"{v:0} units");
            p.AddChildContent<BzChartTooltip>();
            if (line)
                p.AddChildContent<BzChartLineSeries<Row>>(s => s.Add(x => x.Name, "A").Add(x => x.Value, row => row.A));
            else
                Bars(p, second: true);
        });

    [Fact]
    public void There_is_no_hover_target_without_a_tooltip()
    {
        Assert.Empty(Chart(Rows(1, 2), p => Bars(p)).FindAll("rect.bz-chart-hit"));
    }

    [Fact]
    public void Hovering_a_category_shows_its_label_and_every_series_value()
    {
        List<Row> data = [new("Jan", 40, 80), new("Feb", 60, 20), new("Mar", 10, 30)];
        var cut = Hoverable(data);
        Assert.Empty(cut.FindAll("[data-slot=chart-tooltip]"));

        cut.FindAll("rect.bz-chart-hit")[1].TriggerEvent("onpointerenter", new PointerEventArgs());

        var tooltip = cut.Find("[data-slot=chart-tooltip]");
        Assert.Equal("Feb", tooltip.QuerySelector(".bz-chart-tooltip-label")!.TextContent);
        Assert.Contains("60 units", tooltip.TextContent);
        Assert.Contains("20 units", tooltip.TextContent);
        // The band under the pointer is marked, and the other bars step back.
        var band = (Right - Left) / 3;
        var cursor = cut.Find("rect.bz-chart-cursor");
        Assert.Equal(Left + band, Num(cursor, "x"), 2);
        Assert.Equal(band, Num(cursor, "width"), 2);
        Assert.Equal(["0.55", "0.55", "1", "1", "0.55", "0.55"], cut.FindAll("rect.bz-chart-bar").Select(b => b.GetAttribute("opacity")));
    }

    [Fact]
    public void A_mouse_leaving_hides_the_tooltip_and_a_lifted_finger_does_not()
    {
        var cut = Hoverable([new("Jan", 40, 80), new("Feb", 60, 20)]);
        var svg = cut.Find("svg.bz-chart-svg");

        cut.FindAll("rect.bz-chart-hit")[0].TriggerEvent("onpointerdown", new PointerEventArgs { PointerType = "touch" });
        svg.TriggerEvent("onpointerleave", new PointerEventArgs { PointerType = "touch" });
        Assert.Single(cut.FindAll("[data-slot=chart-tooltip]"));

        svg.TriggerEvent("onpointerleave", new PointerEventArgs { PointerType = "mouse" });
        Assert.Empty(cut.FindAll("[data-slot=chart-tooltip]"));

        // Focus leaving the chart (a tap elsewhere) hides it for touch too.
        cut.FindAll("rect.bz-chart-hit")[0].TriggerEvent("onpointerdown", new PointerEventArgs { PointerType = "touch" });
        cut.Find("svg.bz-chart-svg").TriggerEvent("onfocusout", new FocusEventArgs());
        Assert.Empty(cut.FindAll("[data-slot=chart-tooltip]"));
    }

    [Fact]
    public void A_line_chart_marks_the_hovered_point_with_a_hairline_and_a_dot()
    {
        var cut = Hoverable(Rows(0, 100, 50), line: true);

        cut.FindAll("rect.bz-chart-hit")[1].TriggerEvent("onpointerenter", new PointerEventArgs());

        var x = Left + (Right - Left) / 2;
        var hairline = cut.Find("line.bz-chart-cursor-line");
        Assert.Equal((x, x), (Num(hairline, "x1"), Num(hairline, "x2")));
        var dot = Assert.Single(cut.FindAll("circle.bz-chart-dot"));
        Assert.Equal((x, Top), (Num(dot, "cx"), Num(dot, "cy")));
        Assert.Empty(cut.FindAll("rect.bz-chart-cursor"));
    }

    [Theory]
    [InlineData(0, false, false)] // near the left edge: starts at the anchor
    [InlineData(2, true, false)]  // in the middle: centred on it
    [InlineData(4, false, true)]  // near the right edge: ends at it
    public void The_tooltip_stays_inside_the_chart_at_its_edges(int index, bool centred, bool ends)
    {
        var cut = Hoverable(Rows(10, 20, 30, 40, 50), line: true);

        cut.FindAll("rect.bz-chart-hit")[index].TriggerEvent("onpointerenter", new PointerEventArgs());

        var anchor = cut.Find(".bz-chart-tooltip-anchor");
        Assert.Equal(centred, anchor.ClassList.Contains("-translate-x-1/2"));
        Assert.Equal(ends, anchor.ClassList.Contains("-translate-x-full"));
    }

    [Fact]
    public void A_tooltip_template_gets_the_label_and_the_rows()
    {
        List<Row> data = [new("Jan", 40, 80)];
        var cut = Chart(data, p =>
        {
            p.AddChildContent<BzChartTooltip>(t => t.Add(x => x.ChildContent,
                tip => $"<i id=\"tip\">{tip.Label}: {string.Join(", ", tip.Rows.Select(r => $"{r.Name}={r.Value} ({r.Color})"))}</i>"));
            Bars(p, second: true);
        });

        cut.Find("rect.bz-chart-hit").TriggerEvent("onpointerenter", new PointerEventArgs());

        Assert.Equal("Jan: A=40 (var(--chart-1)), B=80 (var(--chart-2))", cut.Find("#tip").TextContent);
        Assert.Empty(cut.FindAll("[data-slot=chart-tooltip]"));
    }

    // ---- legend, colors, naming ---------------------------------------------------------------

    [Fact]
    public void The_legend_lists_the_series_in_order_with_their_colors()
    {
        var cut = Chart(Rows(1, 2), p =>
        {
            p.AddChildContent<BzChartLegend>();
            p.AddChildContent<BzChartBarSeries<Row>>(s => s.Add(x => x.Name, "Desktop").Add(x => x.Value, row => row.A));
            p.AddChildContent<BzChartLineSeries<Row>>(s => s.Add(x => x.Name, "Mobile").Add(x => x.Value, row => row.B).Add(x => x.Color, "#ff8800"));
        });

        var legend = cut.Find("[data-slot=chart-legend]");
        Assert.Equal(["Desktop", "Mobile"], legend.Children.Select(entry => entry.TextContent.Trim()));
        Assert.Equal(
            ["background:var(--chart-1)", "background:#ff8800"],
            legend.QuerySelectorAll(".bz-chart-swatch").Select(swatch => swatch.GetAttribute("style")));
        // Below the drawing unless asked otherwise.
        Assert.True(cut.Markup.IndexOf("data-slot=\"chart-legend\"", StringComparison.Ordinal) > cut.Markup.IndexOf("<svg", StringComparison.Ordinal));
        Assert.Equal("#ff8800", cut.Find("path.bz-chart-line").GetAttribute("stroke"));
        Assert.All(cut.FindAll("rect.bz-chart-bar"), bar => Assert.Equal("var(--chart-1)", bar.GetAttribute("fill")));
    }

    [Fact]
    public void A_top_legend_comes_before_the_drawing()
    {
        var cut = Chart(Rows(1, 2), p =>
        {
            p.AddChildContent<BzChartLegend>(l => l.Add(x => x.Position, ChartLegendPosition.Top));
            Bars(p);
        });

        Assert.InRange(
            cut.Markup.IndexOf("data-slot=\"chart-legend\"", StringComparison.Ordinal),
            0, cut.Markup.IndexOf("<svg", StringComparison.Ordinal));
    }

    [Fact]
    public void A_sixth_series_starts_the_palette_again()
    {
        var cut = Chart(Rows(1, 2), p =>
        {
            p.AddChildContent<BzChartLegend>();
            for (var i = 1; i <= 6; i++)
            {
                var name = $"S{i}";
                p.AddChildContent<BzChartLineSeries<Row>>(s => s.Add(x => x.Name, name).Add(x => x.Value, row => row.A));
            }
        });

        var strokes = cut.FindAll("path.bz-chart-line").Select(line => line.GetAttribute("stroke")).ToList();
        Assert.Equal("var(--chart-5)", strokes[4]);
        Assert.Equal("var(--chart-1)", strokes[5]);
    }

    [Fact]
    public void A_named_chart_is_one_image_and_an_unnamed_one_is_hidden_from_assistive_tech()
    {
        var named = Chart(Rows(1, 2), p => { p.Add(x => x.AriaLabel, "Revenue by month"); Bars(p); }).Find("svg");
        Assert.Equal("img", named.GetAttribute("role"));
        Assert.Equal("Revenue by month", named.GetAttribute("aria-label"));
        Assert.False(named.HasAttribute("aria-hidden"));

        var unnamed = Chart(Rows(1, 2), p => Bars(p)).Find("svg");
        Assert.Equal("true", unnamed.GetAttribute("aria-hidden"));
        Assert.False(unnamed.HasAttribute("role"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void No_data_draws_an_empty_frame(bool empty)
    {
        var cut = Chart(empty ? [] : null, p => { Bars(p); p.AddChildContent<BzChartGrid>(); p.AddChildContent<BzChartTooltip>(); });

        var svg = cut.Find("svg.bz-chart-svg");
        Assert.Equal("0 0 640 360", svg.GetAttribute("viewBox"));
        Assert.Equal(0, svg.ChildElementCount);
    }

    [Fact]
    public void The_drawing_is_sized_by_width_and_height()
    {
        var cut = Chart(Rows(100), p => { p.Add(x => x.Width, 400).Add(x => x.Height, 200); Bars(p); p.AddChildContent<BzChartGrid>(); });

        Assert.Equal("0 0 400 200", cut.Find("svg").GetAttribute("viewBox"));
        var line = cut.FindAll("line.bz-chart-grid-line")[0];
        Assert.Equal((42d, 392d, 174d), (Num(line, "x1"), Num(line, "x2"), Num(line, "y1")));
    }

    [Fact]
    public void Animate_off_is_written_on_the_root()
    {
        Assert.Equal("true", Chart(Rows(1), p => Bars(p)).Find("[data-slot=chart]").GetAttribute("data-animate"));
        Assert.Equal("false", Chart(Rows(1), p => { p.Add(x => x.Animate, false); Bars(p); }).Find("[data-slot=chart]").GetAttribute("data-animate"));
    }

    [Fact]
    public void A_series_that_goes_away_takes_its_drawing_and_its_legend_entry_along()
    {
        var cut = Render<SeriesToggle>(p => p.Add(x => x.Second, true));
        Assert.Equal(2, cut.FindAll("path.bz-chart-line").Count);

        cut.Render(p => p.Add(x => x.Second, false));

        Assert.Single(cut.FindAll("path.bz-chart-line"));
        Assert.Equal(["A"], cut.Find("[data-slot=chart-legend]").Children.Select(entry => entry.TextContent.Trim()));
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    // A chart whose second series comes and goes.
    private sealed class SeriesToggle : Microsoft.AspNetCore.Components.ComponentBase
    {
        [Microsoft.AspNetCore.Components.Parameter] public bool Second { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<BzChart<Row>>(0);
            builder.AddComponentParameter(1, nameof(BzChart<Row>.Data), (IReadOnlyList<Row>)Rows(1, 2));
            builder.AddComponentParameter(2, nameof(BzChart<Row>.ChildContent), (Microsoft.AspNetCore.Components.RenderFragment)(inner =>
            {
                inner.OpenComponent<BzChartLegend>(0);
                inner.CloseComponent();
                inner.OpenComponent<BzChartLineSeries<Row>>(1);
                inner.AddComponentParameter(2, "Name", "A");
                inner.AddComponentParameter(3, "Value", (Func<Row, double>)(row => row.A));
                inner.CloseComponent();
                if (Second)
                {
                    inner.OpenComponent<BzChartLineSeries<Row>>(4);
                    inner.AddComponentParameter(5, "Name", "B");
                    inner.AddComponentParameter(6, "Value", (Func<Row, double>)(row => row.B));
                    inner.CloseComponent();
                }
            }));
            builder.CloseComponent();
        }
    }
}
