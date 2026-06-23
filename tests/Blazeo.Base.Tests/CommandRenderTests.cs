using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render + interaction tests for the headless command palette. Keyboard navigation and the virtual
/// highlight (ts/command.ts: aria-activedescendant, data-selected, scroll, reset-to-first on refilter)
/// are JS, verified in-browser; these cover the C# contract: the combobox input wired to the listbox,
/// the search-driven filtering (matching items stay, the rest get `hidden`), group + separator hiding,
/// the empty state, selection via the item's click, disabled items, keyword matching, and controlled
/// search. JSInterop is Loose so the list module import is a no-op.
/// </summary>
public class CommandRenderTests : TestContext
{
    public CommandRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Body(RenderFragment listContent) => b =>
    {
        b.OpenComponent<BzCommandInput>(0);
        b.AddComponentParameter(1, nameof(BzCommandInput.Placeholder), "Search");
        b.CloseComponent();

        b.OpenComponent<BzCommandList>(2);
        b.AddComponentParameter(3, nameof(BzCommandList.ChildContent), listContent);
        b.CloseComponent();
    };

    private static RenderFragment Item(string value, bool disabled = false, string[]? keywords = null,
        EventCallback<string>? onSelect = null) => b =>
    {
        b.OpenComponent<BzCommandItem>(0);
        b.AddComponentParameter(1, nameof(BzCommandItem.Value), value);
        if (keywords is not null) b.AddComponentParameter(2, nameof(BzCommandItem.Keywords), (IReadOnlyList<string>)keywords);
        if (disabled) b.AddComponentParameter(3, nameof(BzCommandItem.Disabled), true);
        if (onSelect is { } cb) b.AddComponentParameter(4, nameof(BzCommandItem.OnSelect), cb);
        b.AddComponentParameter(5, nameof(BzCommandItem.ChildContent), (RenderFragment)(x => x.AddContent(0, value)));
        b.CloseComponent();
    };

    private static RenderFragment Group(string heading, params RenderFragment[] items) => b =>
    {
        b.OpenComponent<BzCommandGroup>(0);
        b.AddComponentParameter(1, nameof(BzCommandGroup.Heading), heading);
        b.AddComponentParameter(2, nameof(BzCommandGroup.ChildContent), Fragments(items));
        b.CloseComponent();
    };

    private static RenderFragment Empty(string text) => b =>
    {
        b.OpenComponent<BzCommandEmpty>(0);
        b.AddComponentParameter(1, nameof(BzCommandEmpty.ChildContent), (RenderFragment)(x => x.AddContent(0, text)));
        b.CloseComponent();
    };

    private static RenderFragment Separator() => b =>
    {
        b.OpenComponent<BzCommandSeparator>(0);
        b.CloseComponent();
    };

    private static RenderFragment Fragments(params RenderFragment[] fragments) => b =>
    {
        var i = 0;
        foreach (var fragment in fragments)
        {
            b.OpenRegion(i++);
            fragment(b);
            b.CloseRegion();
        }
    };

    private static void Type(IRenderedComponent<BzCommand> cut, string value) =>
        cut.Find("[data-bz-command-input]").Input(new ChangeEventArgs { Value = value });

    [Fact]
    public void Input_is_a_combobox_wired_to_the_listbox()
    {
        var cut = RenderComponent<BzCommand>(p => p.AddChildContent(Body(Item("Calendar"))));

        var input = cut.Find("[data-bz-command-input]");
        var list = cut.Find("[role=listbox]");
        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("true", input.GetAttribute("aria-expanded"));
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
        Assert.Equal(list.GetAttribute("id"), input.GetAttribute("aria-controls"));
    }

    [Fact]
    public void All_items_render_and_are_visible_without_a_search()
    {
        var cut = RenderComponent<BzCommand>(p => p.AddChildContent(
            Body(Fragments(Item("Calendar"), Item("Calculator"), Item("Profile")))));

        var items = cut.FindAll("[data-bz-command-item]");
        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.False(i.HasAttribute("hidden")));
    }

    [Fact]
    public void Typing_hides_the_items_that_do_not_match()
    {
        var cut = RenderComponent<BzCommand>(p => p.AddChildContent(
            Body(Fragments(Item("Calendar"), Item("Calculator"), Item("Profile")))));

        Type(cut, "cal");

        Assert.False(cut.Find("[data-value=Calendar]").HasAttribute("hidden"));
        Assert.False(cut.Find("[data-value=Calculator]").HasAttribute("hidden"));
        Assert.True(cut.Find("[data-value=Profile]").HasAttribute("hidden"));
    }

    [Fact]
    public void A_group_hides_when_all_of_its_items_are_filtered_out()
    {
        var cut = RenderComponent<BzCommand>(p => p.AddChildContent(Body(Fragments(
            Group("Suggestions", Item("Calendar")),
            Group("Settings", Item("Profile"))))));

        var groups = cut.FindAll("[data-bz-command-group]");
        Assert.All(groups, g => Assert.False(g.HasAttribute("hidden"))); // both visible at first

        Type(cut, "cal");

        groups = cut.FindAll("[data-bz-command-group]");
        Assert.False(groups[0].HasAttribute("hidden")); // Suggestions has Calendar
        Assert.True(groups[1].HasAttribute("hidden"));  // Settings is now empty
    }

    [Fact]
    public void The_empty_state_shows_only_while_filtering_with_no_match()
    {
        var cut = RenderComponent<BzCommand>(p => p.AddChildContent(
            Body(Fragments(Empty("No results."), Item("Calendar")))));

        Assert.Empty(cut.FindAll("[data-bz-command-empty]")); // hidden before any search

        Type(cut, "zzz");
        Assert.Contains("No results.", cut.Find("[data-bz-command-empty]").TextContent);

        Type(cut, "cal");
        Assert.Empty(cut.FindAll("[data-bz-command-empty]")); // a match hides it again
    }

    [Fact]
    public void Separators_hide_while_filtering()
    {
        var cut = RenderComponent<BzCommand>(p => p.AddChildContent(Body(Fragments(
            Item("Calendar"), Separator(), Item("Profile")))));

        Assert.False(cut.Find("[data-bz-command-separator]").HasAttribute("hidden"));

        Type(cut, "cal");
        Assert.True(cut.Find("[data-bz-command-separator]").HasAttribute("hidden"));
    }

    [Fact]
    public void Clicking_an_item_reports_its_value()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string>(this, v => selected = v);
        var cut = RenderComponent<BzCommand>(p => p.AddChildContent(
            Body(Item("Calendar", onSelect: cb))));

        cut.Find("[data-value=Calendar]").Click();

        Assert.Equal("Calendar", selected);
    }

    [Fact]
    public void A_disabled_item_is_marked_and_ignores_clicks()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string>(this, v => selected = v);
        var cut = RenderComponent<BzCommand>(p => p.AddChildContent(
            Body(Item("Calculator", disabled: true, onSelect: cb))));

        var item = cut.Find("[data-value=Calculator]");
        Assert.True(item.HasAttribute("data-disabled"));
        Assert.Equal("true", item.GetAttribute("aria-disabled"));

        item.Click();
        Assert.Null(selected);
    }

    [Fact]
    public void Keywords_let_an_item_match_when_the_value_does_not()
    {
        var cut = RenderComponent<BzCommand>(p => p.AddChildContent(
            Body(Fragments(Item("Profile", keywords: new[] { "account" }), Item("Calendar")))));

        Type(cut, "account");

        Assert.False(cut.Find("[data-value=Profile]").HasAttribute("hidden"));
        Assert.True(cut.Find("[data-value=Calendar]").HasAttribute("hidden"));
    }

    [Fact]
    public void Controlled_search_filters_from_the_parameter()
    {
        var cut = RenderComponent<BzCommand>(p => p
            .Add(x => x.Search, "cal")
            .Add(x => x.SearchChanged, EventCallback.Factory.Create<string?>(this, _ => { }))
            .AddChildContent(Body(Fragments(Item("Calendar"), Item("Profile")))));

        Assert.False(cut.Find("[data-value=Calendar]").HasAttribute("hidden"));
        Assert.True(cut.Find("[data-value=Profile]").HasAttribute("hidden"));
    }

    [Fact]
    public void ShouldFilter_false_keeps_every_item_visible_while_typing()
    {
        var cut = RenderComponent<BzCommand>(p => p
            .Add(x => x.ShouldFilter, false)
            .AddChildContent(Body(Fragments(Item("Calendar"), Item("Profile")))));

        Type(cut, "zzz");

        Assert.False(cut.Find("[data-value=Calendar]").HasAttribute("hidden"));
        Assert.False(cut.Find("[data-value=Profile]").HasAttribute("hidden"));
    }
}
