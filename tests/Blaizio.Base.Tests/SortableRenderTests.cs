using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Covers the C# contract of the sortable trio. The pointer drag, and the choice of which keys are an
/// item's own, live in ts/sortable.ts and are verified in-browser; here we test the pure reorder math
/// (<see cref="SortableChange.Apply{T}(System.Collections.Generic.IList{T})"/>), the rendered
/// attributes/state, the keyboard reordering the group drives (through NotifyItemKeyDown, the bridge
/// that script calls), and that keys typed into a control inside an item never reach the group.
/// JSInterop is Loose so the module import in OnAfterRender is a no-op.
/// </summary>
public class SortableRenderTests : BunitContext
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
        // Each item carries its own id, which ts/sortable.js forwards the item's keys by.
        var ids = items.Select(i => i.GetAttribute("data-sortable-item-id")).ToList();
        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // Keys reach the group through ts/sortable.js, which forwards only the keys pressed on an item
    // itself (or its handle) to NotifyItemKeyDown; PressOnItem does what that listener does.

    [Fact]
    public async Task Keyboard_grab_then_arrow_reorders()
    {
        SortableChange? change = null;
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical,
            ["a", "b", "c"], c => change = c);

        await PressOnItem(cut, 0, " ");         // pick up
        // The grabbed state is reflected in the DOM so the styled layer can highlight it.
        Assert.NotNull(Item(cut, 0).GetAttribute("data-grabbed"));
        await PressOnItem(cut, 0, "ArrowDown"); // move down one

        Assert.NotNull(change);
        Assert.Equal(0, change!.From);
        Assert.Equal(1, change.To);
        Assert.Equal(SortableStrategy.Sort, change.Strategy);
    }

    [Fact]
    public async Task Arrow_without_grab_does_not_reorder()
    {
        var reordered = false;
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical,
            ["a", "b", "c"], _ => reordered = true);

        await PressOnItem(cut, 0, "ArrowDown");

        Assert.False(reordered); // arrows only move focus until an item is grabbed
        Assert.Equal("0", Item(cut, 1).GetAttribute("tabindex")); // ...which they did
    }

    [Fact]
    public async Task Enter_grabs_and_drops_and_End_moves_to_the_last_slot()
    {
        SortableChange? change = null;
        int? started = null, ended = null;
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical,
            ["a", "b", "c"], c => change = c, i => started = i, i => ended = i);

        await PressOnItem(cut, 0, "Enter");
        Assert.Equal(0, started);
        Assert.NotNull(Item(cut, 0).GetAttribute("data-grabbed"));

        await PressOnItem(cut, 0, "End");
        Assert.Equal((0, 2), (change!.From, change.To));

        await PressOnItem(cut, 0, "Enter");
        Assert.Equal(0, ended);
        Assert.Empty(cut.FindAll("[data-grabbed]"));
    }

    [Fact]
    public async Task Keys_typed_into_an_input_inside_an_item_stay_the_inputs()
    {
        // The QA case: an item holding a search box. Space must type a space and the arrows must stay the
        // input's (caret, its own menu), so none of them may grab the item, move it, or move focus off it.
        var typed = new List<string>();
        var reordered = false;
        int? started = null;
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical,
            ["a", "b", "c"], _ => reordered = true, i => started = i,
            itemContent: text => b =>
            {
                b.OpenElement(0, "input");
                b.AddAttribute(1, "value", text);
                b.AddAttribute(2, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e => typed.Add(e.Key)));
                b.CloseElement();
            });

        string[] keys = [" ", "Enter", "ArrowDown", "ArrowUp", "ArrowLeft", "ArrowRight", "Home", "End", "Escape", " "];
        foreach (var key in keys)
            cut.FindAll("[data-sortable-item] input")[0].KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Equal(keys, typed);   // the input got every key...
        Assert.Null(started);        // ...and the sortable none: no grab,
        Assert.Empty(cut.FindAll("[data-grabbed]"));
        Assert.False(reordered);     // no move,
        Assert.Equal("0", Item(cut, 0).GetAttribute("tabindex")); // no focus move.

        // The item itself still answers the same keys.
        await PressOnItem(cut, 0, " ");
        Assert.Equal(0, started);
        Assert.NotNull(Item(cut, 0).GetAttribute("data-grabbed"));
        await PressOnItem(cut, 0, "ArrowDown");
        Assert.True(reordered);
    }

    [Fact]
    public async Task Key_for_an_item_the_group_no_longer_has_is_ignored()
    {
        int? started = null;
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical,
            ["a", "b"], onDragStart: i => started = i);

        await cut.InvokeAsync(() => cut.Instance.NotifyItemKeyDown("bz-sortable-item-gone", " "));

        Assert.Null(started);
        Assert.Empty(cut.FindAll("[data-grabbed]"));
    }

    // ---- labels and announcements -------------------------------------------------------------

    [Fact]
    public void Items_are_sortable_items_in_English_by_default()
    {
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical, ["a", "b"]);

        Assert.All(cut.FindAll("[data-sortable-item]"), i => Assert.Equal("sortable item", i.GetAttribute("aria-roledescription")));
    }

    [Fact]
    public void Items_take_their_role_description_from_the_list()
    {
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical, ["a", "b"],
            configure: ps => ps.Add(x => x.ItemRoleDescription, "عنصر قابل للترتيب"));

        Assert.All(cut.FindAll("[data-sortable-item]"), i => Assert.Equal("عنصر قابل للترتيب", i.GetAttribute("aria-roledescription")));
    }

    [Fact]
    public async Task Keyboard_moves_are_announced_in_English_by_default()
    {
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical, ["a", "b", "c"]);

        await PressOnItem(cut, 0, " ");
        Assert.Equal("Grabbed item 1 of 3. Use the arrow keys to move, space to drop, escape to cancel.", LiveText(cut));
        await PressOnItem(cut, 0, "ArrowDown");
        Assert.Equal("Moved to position 2 of 3.", LiveText(cut));
        await PressOnItem(cut, 0, "Escape");
        Assert.Equal("Move cancelled.", LiveText(cut));
        await PressOnItem(cut, 0, " ");
        await PressOnItem(cut, 0, " ");
        Assert.Equal("Dropped at position 1 of 3.", LiveText(cut));
    }

    [Fact]
    public async Task Announcement_words_every_keyboard_move()
    {
        var seen = new List<string>();
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical, ["a", "b", "c"],
            configure: ps => ps.Add(x => x.Announcement, a =>
            {
                seen.Add($"{a.Kind} {a.Position}/{a.Count}");
                return a.Kind switch
                {
                    SortableAnnouncementKind.Grabbed => $"تم التقاط العنصر {a.Position} من {a.Count}",
                    SortableAnnouncementKind.Moved => $"نقل إلى الموضع {a.Position} من {a.Count}",
                    SortableAnnouncementKind.Dropped => $"أفلت في الموضع {a.Position} من {a.Count}",
                    _ => "ألغي النقل",
                };
            }));

        await PressOnItem(cut, 1, " ");
        Assert.Equal("تم التقاط العنصر 2 من 3", LiveText(cut));
        await PressOnItem(cut, 1, "ArrowDown");
        Assert.Equal("نقل إلى الموضع 3 من 3", LiveText(cut));
        // Nothing applied the move (the item is still second), so Escape has nowhere to go back to.
        await PressOnItem(cut, 1, "Escape");
        Assert.Equal("ألغي النقل", LiveText(cut));
        await PressOnItem(cut, 2, "Enter");
        await PressOnItem(cut, 2, "Enter");
        Assert.Equal("أفلت في الموضع 3 من 3", LiveText(cut));

        Assert.Equal(new[] { "Grabbed 2/3", "Moved 3/3", "Cancelled 2/3", "Grabbed 3/3", "Dropped 3/3" }, seen);
    }

    [Fact]
    public async Task An_empty_announcement_keeps_the_English_message()
    {
        var cut = RenderList(SortableStrategy.Sort, SortableOrientation.Vertical, ["a", "b"],
            configure: ps => ps.Add(x => x.Announcement, _ => ""));

        await PressOnItem(cut, 0, " ");

        Assert.StartsWith("Grabbed item 1 of 2.", LiveText(cut));
    }

    // ---- helpers ------------------------------------------------------------------------------

    // A disabled item renders tabindex -1, so it cannot hold the tab stop. With the first item
    // disabled the list used to have no tab stop at all and Tab skipped it whole.
    [Fact]
    public void A_disabled_first_item_hands_the_tab_stop_to_the_next_enabled_item()
    {
        var cut = RenderWithDisabled(disabled: [0]);

        Assert.Equal("-1", Item(cut, 0).GetAttribute("tabindex"));
        Assert.Equal("0", Item(cut, 1).GetAttribute("tabindex"));
        Assert.Equal("-1", Item(cut, 2).GetAttribute("tabindex"));
    }

    // Focus landing on a disabled item would strand the keyboard there: it ignores every key.
    [Fact]
    public async Task Arrow_focus_steps_over_a_disabled_item()
    {
        var cut = RenderWithDisabled(disabled: [1]);

        await PressOnItem(cut, 0, "ArrowDown");

        Assert.Equal("-1", Item(cut, 1).GetAttribute("tabindex"));
        Assert.Equal("0", Item(cut, 2).GetAttribute("tabindex"));
    }

    private IRenderedComponent<BaseSortable> RenderWithDisabled(int[] disabled) =>
        Render<BaseSortable>(ps => ps.AddChildContent(builder =>
        {
            for (var i = 0; i < 3; i++)
            {
                builder.OpenComponent<BaseSortableItem>(0);
                builder.SetKey(i);
                builder.AddAttribute(1, nameof(BaseSortableItem.Index), i);
                builder.AddAttribute(2, nameof(BaseSortableItem.Disabled), disabled.Contains(i));
                // Content, as real items have: it is also what makes an item re-render with its group.
                builder.AddAttribute(3, nameof(BaseSortableItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "item")));
                builder.CloseComponent();
            }
        }));

    private static AngleSharp.Dom.IElement Item(IRenderedComponent<BaseSortable> cut, int index) =>
        cut.FindAll("[data-sortable-item]")[index];

    // What ts/sortable.js does for a key pressed on the item itself: forward it with the item's id.
    private static Task PressOnItem(IRenderedComponent<BaseSortable> cut, int index, string key)
    {
        var id = Item(cut, index).GetAttribute("data-sortable-item-id")!;
        return cut.InvokeAsync(() => cut.Instance.NotifyItemKeyDown(id, key));
    }

    private static SortableChange Sort(int from, int to) =>
        new() { From = from, To = to, FromId = "x", ToId = "x", Strategy = SortableStrategy.Sort };

    private static SortableChange Swap(int from, int to) =>
        new() { From = from, To = to, FromId = "x", ToId = "x", Strategy = SortableStrategy.Swap };

    private IRenderedComponent<BaseSortable> RenderList(
        SortableStrategy strategy, SortableOrientation orientation,
        string[] items, Action<SortableChange>? onReorder = null,
        Action<int>? onDragStart = null, Action<int>? onDragEnd = null,
        Func<string, RenderFragment>? itemContent = null,
        Action<ComponentParameterCollectionBuilder<BaseSortable>>? configure = null)
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
                    itemContent?.Invoke(items[index]) ?? (RenderFragment)(b => b.AddContent(0, items[index])));
                builder.CloseComponent();
            }
        };

        return Render<BaseSortable>(ps =>
        {
            ps.Add(x => x.Strategy, strategy)
              .Add(x => x.Orientation, orientation)
              .Add(x => x.OnReorder, EventCallback.Factory.Create<SortableChange>(this, c => onReorder?.Invoke(c)))
              .Add(x => x.OnDragStart, EventCallback.Factory.Create<int>(this, i => onDragStart?.Invoke(i)))
              .Add(x => x.OnDragEnd, EventCallback.Factory.Create<int>(this, i => onDragEnd?.Invoke(i)))
              .AddChildContent(content);
            configure?.Invoke(ps);
        });
    }

    private static string LiveText(IRenderedComponent<BaseSortable> cut) => cut.Find("[role=status]").TextContent.Trim();
}
