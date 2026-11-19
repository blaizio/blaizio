using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// A panel or handle that is rendered later (it is conditional) registers last, whatever its place
/// in the markup. The group used to keep that order: after a first panel was hidden and shown
/// again, the handle next to it had no neighbour and the others moved the wrong pair. The group now
/// asks the script for the document order after such a render and follows it. These cover the C#
/// half: the group asks, applies the answer, and no size changes hands in the reorder. The script
/// half (reading the document) runs in a browser, in the docs E2E suite.
/// </summary>
public class ResizableOrderTests : BunitContext
{
    private const string Module = "./_content/blaizio.base/dist/resizable.js";

    private static BasePanelGroup.DocumentOrder Order(int[] panels, int[] before) => new(panels, before);

    // ---- the reorder itself ------------------------------------------------------------------------

    // A group with three panels of 50, 30 and 20 and a handle after the first two, registered by
    // hand so the test holds the panels it asks about.
    private (IRenderedComponent<BasePanelGroup> Cut, PanelInfo[] Panels) Group()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<BasePanelGroup>();
        var panels = new[]
        {
            new PanelInfo { Id = "a", DefaultSize = 50 },
            new PanelInfo { Id = "b", DefaultSize = 30 },
            new PanelInfo { Id = "c", DefaultSize = 20 },
        };
        cut.InvokeAsync(() =>
        {
            cut.Instance.RegisterPanel(panels[0]);
            cut.Instance.RegisterHandle();
            cut.Instance.RegisterPanel(panels[1]);
            cut.Instance.RegisterHandle();
            cut.Instance.RegisterPanel(panels[2]);
            cut.Instance.EnsureInitialized();
        }).GetAwaiter().GetResult();
        return (cut, panels);
    }

    private static int[] Sizes(IRenderedComponent<BasePanelGroup> cut, int count) =>
        [.. Enumerable.Range(0, count).Select(i => (int)Math.Round(cut.Instance.GetPanelSize(i)))];

    [Fact]
    public void Panels_follow_the_document_order_and_keep_their_sizes()
    {
        var (cut, panels) = Group();

        // The document says: c, a, b. Handle 0 now follows c, handle 1 follows a.
        var moved = cut.InvokeAsync(() => cut.Instance.ApplyOrder(panels, [0, 1], Order([2, 0, 1], [2, 0]))).GetAwaiter().GetResult();

        Assert.True(moved);
        Assert.Equal([20, 50, 30], Sizes(cut, 3));
        Assert.Equal((20, 50, 30), ((int)panels[2].Size, (int)panels[0].Size, (int)panels[1].Size));
        Assert.Equal(20, (int)cut.Instance.GetHandleValueNow(0));
        Assert.Equal(50, (int)cut.Instance.GetHandleValueNow(1));
    }

    [Fact]
    public void An_order_that_matches_moves_nothing()
    {
        var (cut, panels) = Group();

        var moved = cut.InvokeAsync(() => cut.Instance.ApplyOrder(panels, [0, 1], Order([0, 1, 2], [0, 1]))).GetAwaiter().GetResult();

        Assert.False(moved);
        Assert.Equal([50, 30, 20], Sizes(cut, 3));
    }

    [Fact]
    public async Task A_handle_resizes_the_pair_the_document_puts_around_it()
    {
        var (cut, panels) = Group();
        await cut.InvokeAsync(() => cut.Instance.ApplyOrder(panels, [0, 1], Order([2, 0, 1], [2, 0])));

        // Handle 1 sits between a (50) and b (30) now.
        await cut.InvokeAsync(() => cut.Instance.KeyResize(1, 10));

        Assert.Equal([20, 60, 20], Sizes(cut, 3));
    }

    [Fact]
    public void A_panel_that_joined_after_the_question_stays_at_the_end()
    {
        var (cut, panels) = Group();
        var late = new PanelInfo { Id = "late", DefaultSize = 20 };
        cut.InvokeAsync(() => cut.Instance.RegisterPanel(late)).GetAwaiter().GetResult();

        // The answer is about the first three only.
        cut.InvokeAsync(() => cut.Instance.ApplyOrder(panels, [0, 1], Order([1, 0, 2], [1, 0]))).GetAwaiter().GetResult();

        // b, a, c as the answer says, then the newcomer (it took 20 out of the others).
        Assert.Equal([24, 40, 16, 20], Sizes(cut, 4));
        Assert.Equal(20, (int)late.Size);
    }

    [Fact]
    public void A_panel_and_a_handle_that_left_after_the_question_are_skipped()
    {
        var (cut, panels) = Group();
        cut.InvokeAsync(() =>
        {
            cut.Instance.UnregisterPanel(panels[2]);
            cut.Instance.UnregisterHandle(1);
        }).GetAwaiter().GetResult();

        // The answer still names all three panels and both handles.
        cut.InvokeAsync(() => cut.Instance.ApplyOrder(panels, [0, 1], Order([2, 1, 0], [1, 2]))).GetAwaiter().GetResult();

        // b (37.5) before a (62.5); the departed panel is not brought back and its handle reports nothing.
        Assert.Equal(37.5, cut.Instance.GetPanelSize(0), 3);
        Assert.Equal(62.5, cut.Instance.GetPanelSize(1), 3);
        Assert.Equal(0, (int)cut.Instance.GetPanelSize(2));
        Assert.Equal(37.5, cut.Instance.GetHandleValueNow(0), 3);
        Assert.Equal(0, (int)cut.Instance.GetHandleValueNow(1));
    }

    [Fact]
    public void A_handle_with_no_panel_before_it_moves_nothing()
    {
        var (cut, panels) = Group();

        cut.InvokeAsync(() => cut.Instance.ApplyOrder(panels, [0, 1], Order([0, 1, 2], [-1, 1]))).GetAwaiter().GetResult();
        cut.InvokeAsync(() => cut.Instance.KeyResize(0, 10)).GetAwaiter().GetResult();

        Assert.Equal(0, (int)cut.Instance.GetHandleValueNow(0));
        Assert.Equal([50, 30, 20], Sizes(cut, 3));
    }

    [Fact]
    public void An_answer_with_indices_out_of_range_is_ignored_where_it_is_wrong()
    {
        var (cut, panels) = Group();

        var moved = cut.InvokeAsync(() => cut.Instance.ApplyOrder(panels, [0, 1], Order([7, -1, 0, 1, 2], [9, 1]))).GetAwaiter().GetResult();

        // Handle 0 loses its panel (the answer names none it knows); the panels stay put.
        Assert.True(moved);
        Assert.Equal([50, 30, 20], Sizes(cut, 3));
        Assert.Equal(30, (int)cut.Instance.GetHandleValueNow(1));
    }

    // ---- asking the script -------------------------------------------------------------------------

    // [first panel + its handle, both conditional] [second] [handle] [third]
    private sealed class Page : ComponentBase
    {
        [Parameter] public bool ShowFirst { get; set; } = true;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<BasePanelGroup>(0);
            builder.AddComponentParameter(1, nameof(BasePanelGroup.ChildContent), (RenderFragment)(content =>
            {
                if (ShowFirst)
                {
                    content.OpenComponent<BasePanel>(0);
                    content.AddComponentParameter(1, nameof(BasePanel.DefaultSize), (double?)50);
                    content.CloseComponent();
                    content.OpenComponent<BaseResizeHandle>(2);
                    content.AddComponentParameter(3, nameof(BaseResizeHandle.AriaLabel), "Resize first");
                    content.CloseComponent();
                }
                content.OpenComponent<BasePanel>(4);
                content.AddComponentParameter(5, nameof(BasePanel.DefaultSize), (double?)30);
                content.CloseComponent();
                content.OpenComponent<BaseResizeHandle>(6);
                content.AddComponentParameter(7, nameof(BaseResizeHandle.AriaLabel), "Resize second");
                content.CloseComponent();
                content.OpenComponent<BasePanel>(8);
                content.AddComponentParameter(9, nameof(BasePanel.DefaultSize), (double?)20);
                content.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    [Fact]
    public void A_first_panel_shown_again_goes_back_to_the_front_and_its_handle_moves_its_own_pair()
    {
        var module = JSInterop.SetupModule(Module);
        module.Mode = JSRuntimeMode.Loose;
        // What the script answers once the first panel and its handle are back. The group asks about
        // its panels in registration order (second, third, first) and its live handles (the one
        // after second, then the new one): the document has first, second, third, with the new
        // handle after first and the old one after second. One flat list: the panel order, then
        // the panel before each handle.
        module.Setup<int[]?>("readPanelOrder", _ => true).SetResult([2, 0, 1, 0, 2]);

        var cut = Render<Page>();
        var group = cut.FindComponent<BasePanelGroup>();
        cut.InvokeAsync(() => group.Instance.EnsureInitialized()).GetAwaiter().GetResult();
        Assert.Equal([50, 30, 20], Sizes(group, 3));
        Assert.DoesNotContain(module.Invocations, i => i.Identifier == "readPanelOrder");   // nothing to ask on the first render

        cut.Render(p => p.Add(x => x.ShowFirst, false));
        Assert.Equal([60, 40], Sizes(group, 2));

        cut.Render(p => p.Add(x => x.ShowFirst, true));

        // Back in front with its declared 50; the other two keep their 3:2 inside what is left.
        cut.WaitForAssertion(() => Assert.Equal([50, 30, 20], Sizes(group, 3)));
        var ask = Assert.Single(module.Invocations, i => i.Identifier == "readPanelOrder");
        Assert.Equal(3, ((ElementReference[])ask.Arguments[0]!).Length);
        Assert.Equal(2, ((ElementReference[])ask.Arguments[1]!).Length);

        // The handle that came back with the panel is the third one registered (slot 2). It sits
        // between first and second: before the fix it had no neighbour and moved nothing.
        var handles = cut.FindAll("[role=separator]");
        Assert.Equal("Resize first", handles[0].GetAttribute("aria-label"));
        Assert.Equal("50", handles[0].GetAttribute("aria-valuenow"));
        Assert.Equal("30", handles[1].GetAttribute("aria-valuenow"));

        cut.InvokeAsync(() => group.Instance.KeyResize(2, 10)).GetAwaiter().GetResult();
        Assert.Equal([60, 20, 20], Sizes(group, 3));
    }

    [Fact]
    public void Without_an_answer_the_group_keeps_working_in_registration_order()
    {
        // No script (a test host, a circuit that is going away): the newcomer stays at the end.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<Page>();
        var group = cut.FindComponent<BasePanelGroup>();
        cut.InvokeAsync(() => group.Instance.EnsureInitialized()).GetAwaiter().GetResult();

        cut.Render(p => p.Add(x => x.ShowFirst, false));
        cut.Render(p => p.Add(x => x.ShowFirst, true));

        Assert.Equal([30, 20, 50], Sizes(group, 3));
    }
}
