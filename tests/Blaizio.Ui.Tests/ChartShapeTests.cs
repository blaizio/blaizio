using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// Shapes the charts used to lose: an arc that ends where it starts draws nothing (a slice that is
/// the whole pie, a radial track close to a full turn, both at small sizes), and one value that is
/// not a number turned every coordinate of a chart into NaN.
/// </summary>
public sealed class ChartShapeTests : BunitContext
{
    public ChartShapeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private sealed record Slice(string Name, double Value);

    private static int Arcs(string path) => path.Split(' ').Count(token => token == "A");

    // Arcs that end on the point they start from, as the path's text has it.
    private static int DeadArcs(string path)
    {
        var tokens = path.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var dead = 0;
        var (x, y) = ("", "");
        for (var i = 0; i < tokens.Length;)
        {
            switch (tokens[i])
            {
                case "M" or "L":
                    (x, y) = (tokens[i + 1], tokens[i + 2]);
                    i += 3;
                    break;
                case "A":
                    if (tokens[i + 6] == x && tokens[i + 7] == y) dead++;
                    (x, y) = (tokens[i + 6], tokens[i + 7]);
                    i += 8;
                    break;
                default:
                    i++; // Z
                    break;
            }
        }
        return dead;
    }

    [Theory]
    [InlineData(0, 2)]   // a pie: the rim as two half turns
    [InlineData(0.6, 4)] // a donut: the rim and the hole
    public void A_slice_that_is_the_whole_pie_is_drawn_at_any_size(double inner, int arcs)
    {
        var cut = Render<BzPieChart<Slice>>(p => p
            .Add(x => x.Data, [new Slice("All", 5)])
            .Add(x => x.Value, s => s.Value)
            .Add(x => x.InnerRadius, inner)
            .Add(x => x.Size, 40));

        var path = cut.Find("path.bz-chart-slice").GetAttribute("d")!;
        Assert.Equal(arcs, Arcs(path));
        Assert.Equal(0, DeadArcs(path));
    }

    [Fact]
    public void Slices_that_share_a_pie_keep_their_single_arc()
    {
        var cut = Render<BzPieChart<Slice>>(p => p
            .Add(x => x.Data, [new Slice("A", 3), new Slice("B", 1)])
            .Add(x => x.Value, s => s.Value));

        Assert.All(cut.FindAll("path.bz-chart-slice"), slice => Assert.Equal(1, Arcs(slice.GetAttribute("d")!)));
    }

    [Fact]
    public void A_radial_track_close_to_a_full_turn_is_drawn_at_any_size()
    {
        var cut = Render<BzRadialBarChart<Slice>>(p => p
            .Add(x => x.Data, [new Slice("A", 10), new Slice("B", 4)])
            .Add(x => x.Value, s => s.Value)
            .Add(x => x.MaxAngle, 360)
            .Add(x => x.Size, 60));

        var tracks = cut.FindAll("path.bz-chart-radial-track");
        Assert.Equal(2, tracks.Count);
        Assert.All(tracks, track =>
        {
            var path = track.GetAttribute("d")!;
            Assert.Equal(2, Arcs(path));
            Assert.Equal(0, DeadArcs(path));
        });
        // The bar that fills its track is as long as the track.
        Assert.Equal(0, DeadArcs(cut.FindAll("path.bz-chart-radial-bar")[0].GetAttribute("d")!));
    }

    [Theory]
    [InlineData(false)] // cartesian
    [InlineData(true)]  // radar
    public void A_value_that_is_not_a_number_does_not_blank_a_chart(bool radar)
    {
        List<HoverChart.Point> data = [new("Jan", 100), new("Feb", double.NaN), new("Mar", 80)];

        var cut = Render<HoverChart>(p => p.Add(x => x.Data, data).Add(x => x.Radar, radar));

        Assert.DoesNotContain("NaN", cut.Markup);
        Assert.Equal(3, cut.FindAll(".bz-chart-hit").Count);
    }

    [Fact]
    public void A_value_that_is_not_a_number_is_an_empty_slice_or_ring()
    {
        List<Slice> data = [new("A", 3), new("B", double.NaN), new("C", double.PositiveInfinity), new("D", 1)];

        var pie = Render<BzPieChart<Slice>>(p => p.Add(x => x.Data, data).Add(x => x.Value, s => s.Value));
        Assert.DoesNotContain("NaN", pie.Markup);
        Assert.Equal(2, pie.FindAll("path.bz-chart-slice").Count);

        var radial = Render<BzRadialBarChart<Slice>>(p => p.Add(x => x.Data, data).Add(x => x.Value, s => s.Value));
        Assert.DoesNotContain("NaN", radial.Markup);
        Assert.Equal(4, radial.FindAll("path.bz-chart-radial-track").Count);
    }

    [Fact]
    public void Progress_with_a_value_that_is_not_a_number_shows_an_empty_bar()
    {
        // 0.0 / 0 for a job with nothing to do: the bar used to show as finished.
        var bar = Render<BzProgress>(p => p.Add(x => x.Value, double.NaN));
        Assert.DoesNotContain("NaN", bar.Markup);
        Assert.Contains("--bz-progress-remaining:100%", bar.Markup);

        var ring = Render<BzCircularProgress>(p => p.Add(x => x.Value, double.NaN));
        Assert.DoesNotContain("NaN", ring.Markup);
    }
}
