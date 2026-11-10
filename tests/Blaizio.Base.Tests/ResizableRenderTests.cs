using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render tests for the headless resizable trio. The pointer drag lives in ts/resizable.ts and is
/// verified in-browser; here we cover the C# contract - the group hooks, size initialisation from
/// DefaultSize, and the resize math (shift, min/max clamp, collapse) driven through the group's
/// drag methods. JSInterop is Loose so the handle's module import is a no-op.
/// </summary>
public class ResizableRenderTests : BunitContext
{
    public ResizableRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<BasePanelGroup> RenderGroup(
        Orientation dir, params (double? Def, double Min, double Max, bool Collapsible)[] panels)
    {
        RenderFragment content = builder =>
        {
            var seq = 0;
            for (var i = 0; i < panels.Length; i++)
            {
                var p = panels[i];
                builder.OpenComponent<BasePanel>(seq++);
                builder.AddAttribute(seq++, nameof(BasePanel.DefaultSize), p.Def);
                builder.AddAttribute(seq++, nameof(BasePanel.MinSize), p.Min);
                builder.AddAttribute(seq++, nameof(BasePanel.MaxSize), p.Max);
                builder.AddAttribute(seq++, nameof(BasePanel.Collapsible), p.Collapsible);
                builder.AddAttribute(seq++, nameof(BasePanel.CollapsedSize), 0d);
                builder.CloseComponent();
                if (i < panels.Length - 1)
                {
                    builder.OpenComponent<BaseResizeHandle>(seq++);
                    builder.CloseComponent();
                }
            }
        };
        var cut = Render<BasePanelGroup>(ps => ps.Add(x => x.Orientation, dir).AddChildContent(content));
        // OnAfterRender timing in bUnit isn't guaranteed; drive the (idempotent) size init explicitly.
        cut.InvokeAsync(() => cut.Instance.EnsureInitialized()).GetAwaiter().GetResult();
        return cut;
    }

    // Assert on the group's own sizes (the DOM data-panel-size is verified in-browser); avoids the
    // bUnit after-render/DOM-flush nuance and tests the resize math directly.
    private static int[] Sizes(IRenderedComponent<BasePanelGroup> cut, int n) =>
        Enumerable.Range(0, n).Select(i => (int)System.Math.Round(cut.Instance.GetPanelSize(i))).ToArray();

    [Fact]
    public void Renders_group_hooks()
    {
        var cut = RenderGroup(Orientation.Vertical, (50, 10, 100, false), (50, 10, 100, false));
        var group = cut.Find("[data-slot=resizable-panel-group]");
        Assert.Equal("vertical", group.GetAttribute("data-direction"));
        // the handle mirrors the perpendicular orientation
        Assert.Equal("horizontal", cut.Find("[data-slot=resizable-handle]").GetAttribute("aria-orientation"));
    }

    [Fact]
    public void Handle_emits_its_typed_aria_name_and_controls()
    {
        RenderFragment content = builder =>
        {
            builder.OpenComponent<BasePanel>(0);
            builder.AddAttribute(1, nameof(BasePanel.DefaultSize), (double?)50);
            builder.CloseComponent();
            builder.OpenComponent<BaseResizeHandle>(2);
            builder.AddComponentParameter(3, nameof(BaseResizeHandle.AriaLabel), "Resize sidebar");
            builder.AddComponentParameter(4, nameof(BaseResizeHandle.AriaControls), "sidebar-panel");
            builder.CloseComponent();
            builder.OpenComponent<BasePanel>(5);
            builder.AddAttribute(6, nameof(BasePanel.DefaultSize), (double?)50);
            builder.CloseComponent();
        };
        var cut = Render<BasePanelGroup>(ps => ps.AddChildContent(content));

        var handle = cut.Find("[data-slot=resizable-handle]");
        Assert.Equal("Resize sidebar", handle.GetAttribute("aria-label"));
        Assert.Equal("sidebar-panel", handle.GetAttribute("aria-controls"));
    }

    [Fact]
    public void Initializes_sizes_from_default()
    {
        var cut = RenderGroup(Orientation.Horizontal, (50, 10, 100, false), (50, 10, 100, false));
        Assert.Equal(new[] { 50, 50 }, Sizes(cut, 2));
    }

    [Fact]
    public async Task Drag_shifts_size_between_neighbours()
    {
        var cut = RenderGroup(Orientation.Horizontal, (50, 10, 100, false), (50, 10, 100, false));
        await cut.InvokeAsync(async () =>
        {
            cut.Instance.BeginResize();
            await cut.Instance.DragResize(0, 20); // grow panel 0 by 20%
        });
        Assert.Equal(new[] { 70, 30 }, Sizes(cut, 2));
    }

    [Fact]
    public async Task Drag_respects_min_size()
    {
        var cut = RenderGroup(Orientation.Horizontal, (50, 30, 100, false), (50, 10, 100, false));
        await cut.InvokeAsync(async () =>
        {
            cut.Instance.BeginResize();
            await cut.Instance.DragResize(0, -50); // would take panel 0 to -0; clamps at its min 30
        });
        Assert.Equal(new[] { 30, 70 }, Sizes(cut, 2));
    }

    [Fact]
    public async Task Collapsible_panel_snaps_closed_past_min()
    {
        var cut = RenderGroup(Orientation.Horizontal, (25, 15, 100, true), (75, 10, 100, false));
        await cut.InvokeAsync(async () =>
        {
            cut.Instance.BeginResize();
            await cut.Instance.DragResize(0, -60); // well past min -> collapses to 0
        });
        Assert.Equal(new[] { 0, 100 }, Sizes(cut, 2));
    }

    // Handles and empty panels have no ChildContent and unchanged parameters, so the group's own
    // re-render skips them; they must follow the group's sizes through LayoutChanged.
    [Fact]
    public async Task Handle_aria_and_empty_panel_size_follow_the_layout()
    {
        var cut = RenderGroup(Orientation.Horizontal, (30, 10, 80, false), (70, 10, 100, false));
        var handle = () => cut.Find("[data-slot=resizable-handle]");
        var firstPanel = () => cut.FindAll("[data-slot=resizable-panel]")[0];

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("30", handle().GetAttribute("aria-valuenow"));
            Assert.Equal("10", handle().GetAttribute("aria-valuemin"));
            Assert.Equal("80", handle().GetAttribute("aria-valuemax"));
            Assert.Equal("30", firstPanel().GetAttribute("data-panel-size"));
        });

        await cut.InvokeAsync(async () =>
        {
            cut.Instance.BeginResize();
            await cut.Instance.DragResize(0, 25);
        });
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("55", handle().GetAttribute("aria-valuenow"));
            Assert.Equal("55", firstPanel().GetAttribute("data-panel-size"));
        });
    }

    [Fact]
    public void A_panel_that_appears_later_takes_its_size_out_of_the_others()
    {
        var cut = RenderGroup(Orientation.Horizontal, (50, 10, 100, false), (25, 10, 100, false), (25, 10, 100, false));
        var late = new PanelInfo { Id = "late", DefaultSize = 20 };

        cut.InvokeAsync(() => cut.Instance.RegisterPanel(late)).GetAwaiter().GetResult();

        // 20 for the newcomer; the rest keep their proportions inside the remaining 80.
        Assert.Equal([40, 20, 20, 20], Sizes(cut, 4));
    }

    [Fact]
    public void A_panel_that_leaves_gives_its_space_back()
    {
        var cut = RenderGroup(Orientation.Horizontal, (50, 10, 100, false), (25, 10, 100, false), (25, 10, 100, false));
        var late = new PanelInfo { Id = "late", DefaultSize = 20 };
        cut.InvokeAsync(() => cut.Instance.RegisterPanel(late)).GetAwaiter().GetResult();

        cut.InvokeAsync(() => cut.Instance.UnregisterPanel(late)).GetAwaiter().GetResult();

        Assert.Equal([50, 25, 25], Sizes(cut, 3));
        Assert.Equal(0, (int)cut.Instance.GetPanelSize(3)); // no ghost left behind
    }

    [Fact]
    public void A_handle_keeps_its_panel_when_another_handle_leaves()
    {
        var cut = RenderGroup(Orientation.Horizontal, (50, 10, 100, false), (25, 10, 100, false), (25, 10, 100, false));

        cut.InvokeAsync(() => cut.Instance.UnregisterHandle(0)).GetAwaiter().GetResult();

        // Handle 1 still sits after the second panel; the freed slot reports nothing.
        Assert.Equal(25, (int)cut.Instance.GetHandleValueNow(1));
        Assert.Equal(0, (int)cut.Instance.GetHandleValueNow(0));
    }
}
