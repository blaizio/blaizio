using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// The hovered index of a chart belongs to the data it was taken from. When the data is replaced
/// by a shorter list while the pointer rests on a point, the tooltip used to index past the end
/// and the render threw.
/// </summary>
public sealed class ChartHoverTests : BunitContext
{
    public ChartHoverTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    [Theory]
    [InlineData(false)] // cartesian
    [InlineData(true)]  // radar
    public void A_chart_survives_its_data_getting_shorter_under_the_pointer(bool radar)
    {
        var cut = Render<HoverChart>(p => p.Add(x => x.Data, HoverChart.Months(10)).Add(x => x.Radar, radar));
        Assert.Equal(10, cut.FindAll(".bz-chart-hit").Count);

        // The pointer rests on the last category, so its tooltip is up.
        cut.FindAll(".bz-chart-hit")[9].TriggerEvent("onpointerenter", new PointerEventArgs());

        cut.Render(p => p.Add(x => x.Data, HoverChart.Months(5)).Add(x => x.Radar, radar));

        Assert.Equal(5, cut.FindAll(".bz-chart-hit").Count);
    }
}
