using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Covers the C# contract of the sortable trio. The pointer drag lives in ts/sortable.ts and is
/// verified in-browser; here we test the pure reorder math (<see cref="SortableChange.Apply{T}(System.Collections.Generic.IList{T})"/>),
/// the rendered attributes/state, and the keyboard reordering the group drives. JSInterop is Loose so
/// the module import in OnAfterRender is a no-op.
/// </summary>
public class SortableRenderTests : TestContext
{
    public SortableRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // ---- SortableChange.Apply (pure reorder logic) --------------------------------------------

    [Fact]
    public void Sort_moves_item_down()
    {
        var list = new List<string> { "a", "b", "c", "d" };
        Sort(0, 2).Apply(list);
        Assert.Equal(new[] { "b", "c", "a", "d" }, list);
    }

    [Fact]
    public void Sort_moves_item_up()
    {
        var list = new List<string> { "a", "b", "c", "d" };
        Sort(3, 1).Apply(list);
        Assert.Equal(new[] { "a", "d", "b", "c" }, list);
    }

    [Fact]
    public void Sort_to_same_index_is_noop()
    {
        var list = new List<string> { "a", "b", "c" };
        Sort(1, 1).Apply(list);
        Assert.Equal(new[] { "a", "b", "c" }, list);
    }

    [Fact]
    public void Sort_clamps_out_of_range_target()
    {
        var list = new List<string> { "a", "b", "c" };
        Sort(0, 99).Apply(list);
        Assert.Equal(new[] { "b", "c", "a" }, list);
    }

    [Fact]
    public void Sort_ignores_out_of_range_source()
    {
        var list = new List<string> { "a", "b" };
        Sort(5, 0).Apply(list);
        Assert.Equal(new[] { "a", "b" }, list);
    }

    [Fact]
    public void Swap_exchanges_two_items()
    {
        var list = new List<string> { "a", "b", "c", "d" };
        Swap(0, 3).Apply(list);
        Assert.Equal(new[] { "d", "b", "c", "a" }, list);
    }

    [Fact]
    public void Cross_list_transfer_moves_between_lists()
    {
        var from = new List<string> { "a", "b", "c" };
        var to = new List<string> { "x", "y" };
        var change = new SortableChange { From = 1, To = 1, FromId = "l", ToId = "r", Strategy = SortableStrategy.Sort };

        Assert.True(change.IsCrossList);
        change.Apply(from, to);

        Assert.Equal(new[] { "a", "c" }, from);
        Assert.Equal(new[] { "x", "b", "y" }, to);
    }

    [Fact]
    public void Same_list_single_arg_apply_is_ignored_when_cross_list()
    {
        var list = new List<string> { "a", "b", "c" };
        var change = new SortableChange { From = 0, To = 1, FromId = "l", ToId = "r", Strategy = SortableStrategy.Sort };
        change.Apply(list); // cross-list: the single-list overload must not touch it
        Assert.Equal(new[] { "a", "b", "c" }, list);
    }

    // ---- Rendering + keyboard -----------------------------------------------------------------

    [Fact]
    public void Renders_slot_and_state_attributes()
    {
        var cut = RenderList(SortableStrategy.Swap, SortableOrientation.Horizontal, ["a", "b"]);
        var root = cut.Find("[data-slot=sortable]");
        Assert.Equal("horizontal", root.GetAttribute("data-orientation"));
        Assert.Equal("swap", root.GetAttribute("data-strategy"));

        var items = cut.FindAll("[data-sortable-item]");
        Assert.Equal(2, items.Count);
        // Roving tab stop: only the first item is tabbable initially.
        Assert.Equal("0", items[0].GetAttribute("tabindex"));
        Assert.Equal("-1", items[1].GetAttribute("tabindex"));
    }

    [Fact]
    public void Keyboard_grab_then_arrow_reorders()
    {
        SortableChange? change = null;
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical,
            ["a", "b", "c"], c => change = c);

        var first = cut.FindAll("[data-sortable-item]")[0];
        first.KeyDown(new KeyboardEventArgs { Key = " " });      // pick up
        // The grabbed state is reflected in the DOM so the styled layer can highlight it.
        Assert.NotNull(cut.FindAll("[data-sortable-item]")[0].GetAttribute("data-grabbed"));
        first.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }); // move down one

        Assert.NotNull(change);
        Assert.Equal(0, change!.From);
        Assert.Equal(1, change.To);
        Assert.Equal(SortableStrategy.Sort, change.Strategy);
    }

    [Fact]
    public void Arrow_without_grab_does_not_reorder()
    {
        var reordered = false;
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical,
            ["a", "b", "c"], _ => reordered = true);

        cut.FindAll("[data-sortable-item]")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.False(reordered); // arrows only move focus until an item is grabbed
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static SortableChange Sort(int from, int to) =>
        new() { From = from, To = to, FromId = "x", ToId = "x", Strategy = SortableStrategy.Sort };

    private static SortableChange Swap(int from, int to) =>
        new() { From = from, To = to, FromId = "x", ToId = "x", Strategy = SortableStrategy.Swap };

    private IRenderedComponent<BaseSortable> RenderList(
        SortableStrategy strategy, SortableOrientation orientation,
        string[] items, Action<SortableChange>? onReorder = null)
    {
        RenderFragment content = builder =>
        {
            var seq = 0;
            for (var i = 0; i < items.Length; i++)
            {
                var index = i;
                builder.OpenComponent<BaseSortableItem>(seq++);
                builder.AddAttribute(seq++, nameof(BaseSortableItem.Index), index);
                builder.AddAttribute(seq++, nameof(BaseSortableItem.ChildContent),
                    (RenderFragment)(b => b.AddContent(0, items[index])));
                builder.CloseComponent();
            }
        };

        return RenderComponent<BaseSortable>(ps => ps
            .Add(x => x.Strategy, strategy)
            .Add(x => x.Orientation, orientation)
            .Add(x => x.OnReorder, EventCallback.Factory.Create<SortableChange>(this, c => onReorder?.Invoke(c)))
            .AddChildContent(content));
    }
}
