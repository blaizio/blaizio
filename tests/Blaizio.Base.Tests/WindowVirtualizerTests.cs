using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

// The window virtualizer's geometry: the C# owns the fixed-mode spacer math + the clamping/re-seeding;
// the actual scroll measurement is ts/virtualizer.ts (a no-op under bUnit's loose interop, verified
// in-browser). These pin the range -> window/spacer reconciliation the JS feeds.
public class WindowVirtualizerTests : TestContext
{
    public WindowVirtualizerTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static readonly RenderFragment<VirtualWindow> NoChild = _ => _ => { };

    private IRenderedComponent<BaseWindowVirtualizer> Render(
        int count, double rowHeight = 40, bool dynamic = false, int overscan = 4, int initial = 30, string @as = "div") =>
        RenderComponent<BaseWindowVirtualizer>(p => p
            .Add(x => x.Count, count)
            .Add(x => x.ItemSize, rowHeight)
            .Add(x => x.Dynamic, dynamic)
            .Add(x => x.Overscan, overscan)
            .Add(x => x.InitialCount, initial)
            .Add(x => x.Element, @as)
            .Add(x => x.ChildContent, NoChild));

    [Fact]
    public void Initial_window_is_a_top_slice_with_an_estimated_bottom_spacer()
    {
        var cut = Render(count: 1000, rowHeight: 40, initial: 30);

        var window = cut.Instance.Window;
        Assert.Equal(0, window.Start);
        Assert.Equal(30, window.End);
        Assert.Equal(0, window.PaddingTop);
        Assert.Equal((1000 - 30) * 40, window.PaddingBottom); // the unrendered tail, as estimate
    }

    [Fact]
    public void Initial_window_caps_at_the_row_count_when_fewer_than_the_initial_slice()
    {
        var cut = Render(count: 10, initial: 30);

        Assert.Equal(10, cut.Instance.Window.End);
        Assert.Equal(0, cut.Instance.Window.PaddingBottom);
    }

    [Fact]
    public void Fixed_mode_derives_both_spacers_from_the_row_height()
    {
        var cut = Render(count: 1000, rowHeight: 50);

        // -1 spacers are the fixed-mode sentinel: C# computes them from the clamped range.
        cut.InvokeAsync(() => cut.Instance.OnRangeChanged(100, 130, -1, -1));

        var window = cut.Instance.Window;
        Assert.Equal(100, window.Start);
        Assert.Equal(130, window.End);
        Assert.Equal(100 * 50, window.PaddingTop);
        Assert.Equal((1000 - 130) * 50, window.PaddingBottom);
    }

    [Fact]
    public void Dynamic_mode_trusts_the_measured_spacers_from_the_module()
    {
        var cut = Render(count: 1000, rowHeight: 40, dynamic: true);

        cut.InvokeAsync(() => cut.Instance.OnRangeChanged(50, 80, 1234.5, 6789.0));

        var window = cut.Instance.Window;
        Assert.Equal(50, window.Start);
        Assert.Equal(80, window.End);
        Assert.Equal(1234.5, window.PaddingTop);
        Assert.Equal(6789.0, window.PaddingBottom);
    }

    [Fact]
    public void Range_is_clamped_to_the_domain()
    {
        var cut = Render(count: 100, rowHeight: 40);

        cut.InvokeAsync(() => cut.Instance.OnRangeChanged(-10, 500, -1, -1));

        var window = cut.Instance.Window;
        Assert.Equal(0, window.Start);
        Assert.Equal(100, window.End);
        Assert.Equal(0, window.PaddingTop);
        Assert.Equal(0, window.PaddingBottom);
    }

    [Fact]
    public void End_never_precedes_start()
    {
        var cut = Render(count: 100, rowHeight: 40);

        cut.InvokeAsync(() => cut.Instance.OnRangeChanged(50, 10, -1, -1));

        var window = cut.Instance.Window;
        Assert.True(window.Start <= window.End);
        Assert.Equal(window.End, window.Start); // start collapses onto the (lower) end
    }

    [Fact]
    public void A_count_change_reseeds_the_window_to_the_top()
    {
        var cut = Render(count: 1000, rowHeight: 40, initial: 20);
        cut.InvokeAsync(() => cut.Instance.OnRangeChanged(500, 520, -1, -1));

        // Filtering down to 5 rows: the window must snap back to a valid top slice.
        cut.SetParametersAndRender(p => p.Add(x => x.Count, 5));

        var window = cut.Instance.Window;
        Assert.Equal(0, window.Start);
        Assert.Equal(5, window.End);
        Assert.Equal(0, window.PaddingTop);
        Assert.Equal(0, window.PaddingBottom);
    }

    [Fact]
    public void Root_exposes_the_config_the_module_reads_live()
    {
        var cut = Render(count: 1234, rowHeight: 42.5, overscan: 7, dynamic: true);

        var root = cut.Find("[data-bz-virtualizer]");
        Assert.Equal("1234", root.GetAttribute("data-bz-virtual-count"));
        Assert.Equal("42.5", root.GetAttribute("data-bz-virtual-row-height"));
        Assert.Equal("7", root.GetAttribute("data-bz-virtual-overscan"));
        Assert.Equal("true", root.GetAttribute("data-bz-virtual-dynamic"));
    }

    [Fact]
    public void Renders_the_requested_element()
    {
        var cut = Render(count: 5, @as: "tbody");
        Assert.NotNull(cut.Find("tbody[data-bz-virtualizer]"));
    }

    [Fact]
    public void Empty_data_yields_an_empty_window()
    {
        var cut = Render(count: 0);

        var window = cut.Instance.Window;
        Assert.Equal(0, window.Start);
        Assert.Equal(0, window.End);
        Assert.Equal(0, window.PaddingBottom);
    }
}
