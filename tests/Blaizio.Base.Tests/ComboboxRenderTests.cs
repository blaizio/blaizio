using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + interaction tests for the headless combobox. The virtual highlight + keyboard navigation
/// (ts/combobox.ts: aria-activedescendant, data-highlighted, scroll, reset-on-refilter), the popup
/// positioning (ts/positioning.ts), the presence animation (ts/presence.ts) and outside-pointer-down
/// dismissal (ts/dismissableLayer.ts) are JS, verified in-browser. These cover the C# contract: the
/// combobox input wired to the listbox, opening on click / typing / ArrowDown (but NOT on a focus-only
/// Tab) and closing on Escape, the query-driven filtering (matches stay, the rest get `hidden`) inside the
/// open popup that resets on every open so a reopened selection shows the whole list, group + separator
/// hiding, the empty state, single-select picking (reports + collapses, the closed input showing the
/// chosen label),
/// multi-select toggling (stays open) with chips + backspace removal, the selected/indicator state, the
/// clear button, disabled items, keyword matching, and controlled query. JSInterop is Loose so module
/// imports are no-ops.
/// </summary>
public class ComboboxRenderTests : BunitContext
{
    public ComboboxRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // input + content(empty? + list(items)) - the standard single-select composition.
    private static RenderFragment Body(RenderFragment items, RenderFragment? empty = null) => b =>
    {
        b.OpenRegion(0);
        Input()(b);
        b.CloseRegion();

        b.OpenRegion(1);
        Content(Fragments(empty ?? Nothing, List(items)))(b);
        b.CloseRegion();
    };

    private static readonly RenderFragment Nothing = _ => { };

    private static RenderFragment Input(bool? anchor = null) => b =>
    {
        b.OpenComponent<BaseComboboxInput>(0);
        b.AddComponentParameter(1, nameof(BaseComboboxInput.Placeholder), "Search");
        if (anchor is { } a) b.AddComponentParameter(2, nameof(BaseComboboxInput.Anchor), (bool?)a);
        b.CloseComponent();
    };

    private static RenderFragment Content(RenderFragment inner) => b =>
    {
        b.OpenComponent<BaseComboboxContent>(0);
        b.AddComponentParameter(1, nameof(BaseComboboxContent.ChildContent), inner);
        b.CloseComponent();
    };

    private static RenderFragment List(RenderFragment items) => b =>
    {
        b.OpenComponent<BaseComboboxList>(0);
        b.AddComponentParameter(1, nameof(BaseComboboxList.ChildContent), items);
        b.CloseComponent();
    };

    private static RenderFragment Item(string value, bool disabled = false, string[]? keywords = null,
        bool indicator = false) => b =>
    {
        b.OpenComponent<BaseComboboxItem>(0);
        b.AddComponentParameter(1, nameof(BaseComboboxItem.Value), value);
        if (keywords is not null) b.AddComponentParameter(2, nameof(BaseComboboxItem.Keywords), (IReadOnlyList<string>)keywords);
        if (disabled) b.AddComponentParameter(3, nameof(BaseComboboxItem.Disabled), true);
        b.AddComponentParameter(4, nameof(BaseComboboxItem.ChildContent), (RenderFragment)(x =>
        {
            x.AddContent(0, value);
            if (indicator)
            {
                x.OpenComponent<BaseComboboxItemIndicator>(1);
                x.AddComponentParameter(2, nameof(BaseComboboxItemIndicator.ChildContent), (RenderFragment)(i => i.AddContent(0, "check")));
                x.CloseComponent();
            }
        }));
        b.CloseComponent();
    };

    private static RenderFragment Group(string heading, params RenderFragment[] items) => b =>
    {
        b.OpenComponent<BaseComboboxGroup>(0);
        b.AddComponentParameter(1, nameof(BaseComboboxGroup.ChildContent), (RenderFragment)(g =>
        {
            g.OpenComponent<BaseComboboxGroupLabel>(0);
            g.AddComponentParameter(1, nameof(BaseComboboxGroupLabel.ChildContent), (RenderFragment)(l => l.AddContent(0, heading)));
            g.CloseComponent();
            g.OpenRegion(2);
            Fragments(items)(g);
            g.CloseRegion();
        }));
        b.CloseComponent();
    };

    private static RenderFragment EmptyState(string text) => b =>
    {
        b.OpenComponent<BaseComboboxEmpty>(0);
        b.AddComponentParameter(1, nameof(BaseComboboxEmpty.ChildContent), (RenderFragment)(x => x.AddContent(0, text)));
        b.CloseComponent();
    };

    private static RenderFragment Separator() => b =>
    {
        b.OpenComponent<BaseComboboxSeparator>(0);
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

    private static void Type(IRenderedComponent<BaseCombobox> cut, string value) =>
        cut.Find("[data-bz-combobox-input]").Input(new ChangeEventArgs { Value = value });

    // ---- aria wiring + open state ----

    [Fact]
    public void Input_is_a_combobox_wired_to_the_listbox()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Body(Item("Next.js"))));

        var input = cut.Find("[data-bz-combobox-input]");
        var list = cut.Find("[role=listbox]");
        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("true", input.GetAttribute("aria-expanded"));
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
        Assert.Equal(list.GetAttribute("id"), input.GetAttribute("aria-controls"));
    }

    [Fact]
    public void Closed_by_default_renders_no_popup()
    {
        var cut = Render<BaseCombobox>(p => p.AddChildContent(Body(Item("Next.js"))));

        Assert.Equal("false", cut.Find("[data-bz-combobox-input]").GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[role=listbox]"));
        Assert.Empty(cut.FindAll("[role=option]"));
    }

    [Fact]
    public void Clicking_the_input_opens_the_popup()
    {
        var cut = Render<BaseCombobox>(p => p.AddChildContent(Body(Item("Next.js"))));

        cut.Find("[data-bz-combobox-input]").Click();

        Assert.Equal("true", cut.Find("[data-bz-combobox-input]").GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll("[role=option]"));
    }

    [Fact]
    public void Focus_alone_does_not_open_the_popup()
    {
        // A Tab that only moves focus into the field must NOT open it - only a click / typing / ArrowDown
        // do. The input deliberately has no onfocus handler, so focusing it is a no-op: bUnit surfaces the
        // absent handler (which also guards against anyone re-introducing open-on-focus), and the popup
        // stays closed.
        var cut = Render<BaseCombobox>(p => p.AddChildContent(Body(Item("Next.js"))));

        Assert.Throws<MissingEventHandlerException>(() => cut.Find("[data-bz-combobox-input]").Focus());
        Assert.Equal("false", cut.Find("[data-bz-combobox-input]").GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[role=option]"));
    }

    [Fact]
    public void Typing_opens_the_popup()
    {
        var cut = Render<BaseCombobox>(p => p.AddChildContent(Body(Item("Next.js"))));

        Type(cut, "ne");

        Assert.Equal("true", cut.Find("[data-bz-combobox-input]").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void ArrowDown_opens_a_closed_popup()
    {
        var cut = Render<BaseCombobox>(p => p.AddChildContent(Body(Item("Next.js"))));

        cut.Find("[data-bz-combobox-input]").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Equal("true", cut.Find("[data-bz-combobox-input]").GetAttribute("aria-expanded"));
    }

    [Theory]
    [InlineData("Escape")]
    [InlineData("Tab")]
    public void Escape_or_tab_closes_an_open_popup(string key)
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Body(Item("Next.js"))));

        cut.Find("[data-bz-combobox-input]").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Equal("false", cut.Find("[data-bz-combobox-input]").GetAttribute("aria-expanded"));
    }

    // ---- filtering ----

    [Fact]
    public void All_items_render_and_are_visible_without_a_query()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Body(Fragments(Item("Next.js"), Item("Nuxt.js"), Item("Astro")))));

        var items = cut.FindAll("[data-bz-combobox-item]");
        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.False(i.HasAttribute("hidden")));
    }

    [Fact]
    public void Typing_hides_the_items_that_do_not_match()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Body(Fragments(Item("Next.js"), Item("Nuxt.js"), Item("Astro")))));

        Type(cut, "nu");

        Assert.True(cut.Find("[data-value='Next.js']").HasAttribute("hidden"));
        Assert.False(cut.Find("[data-value='Nuxt.js']").HasAttribute("hidden"));
        Assert.True(cut.Find("[data-value=Astro]").HasAttribute("hidden"));
    }

    [Fact]
    public void A_group_hides_when_all_of_its_items_are_filtered_out()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Body(Fragments(
                Group("Frameworks", Item("Next.js")),
                Group("Builders", Item("Astro"))))));

        Type(cut, "next");

        var groups = cut.FindAll("[data-bz-combobox-group]");
        Assert.False(groups[0].HasAttribute("hidden")); // Frameworks has Next.js
        Assert.True(groups[1].HasAttribute("hidden"));  // Builders is now empty
    }

    [Fact]
    public void The_empty_state_shows_only_while_filtering_with_no_match()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Body(Item("Next.js"), EmptyState("No items found."))));

        Assert.Empty(cut.FindAll("[data-bz-combobox-empty]")); // hidden before any query

        Type(cut, "zzz");
        Assert.Contains("No items found.", cut.Find("[data-bz-combobox-empty]").TextContent);

        Type(cut, "next");
        Assert.Empty(cut.FindAll("[data-bz-combobox-empty]")); // a match hides it again
    }

    [Fact]
    public void Separators_hide_while_filtering()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Body(Fragments(Item("Next.js"), Separator(), Item("Astro")))));

        Assert.False(cut.Find("[data-bz-combobox-separator]").HasAttribute("hidden"));

        Type(cut, "next");
        Assert.True(cut.Find("[data-bz-combobox-separator]").HasAttribute("hidden"));
    }

    [Fact]
    public void Keywords_let_an_item_match_when_the_value_does_not()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Body(Fragments(
                Item("Remix", keywords: new[] { "react" }), Item("Astro")))));

        Type(cut, "react");

        Assert.False(cut.Find("[data-value=Remix]").HasAttribute("hidden"));
        Assert.True(cut.Find("[data-value=Astro]").HasAttribute("hidden"));
    }

    [Fact]
    public void Controlled_query_filters_from_the_parameter()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.Search, "nu")
            .Add(x => x.SearchChanged, EventCallback.Factory.Create<string?>(this, _ => { }))
            .AddChildContent(Body(Fragments(Item("Next.js"), Item("Nuxt.js")))));

        Assert.True(cut.Find("[data-value='Next.js']").HasAttribute("hidden"));
        Assert.False(cut.Find("[data-value='Nuxt.js']").HasAttribute("hidden"));
    }

    [Fact]
    public void ShouldFilter_false_keeps_every_item_visible_while_typing()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.ShouldFilter, false)
            .AddChildContent(Body(Fragments(Item("Next.js"), Item("Astro")))));

        Type(cut, "zzz");

        Assert.False(cut.Find("[data-value='Next.js']").HasAttribute("hidden"));
        Assert.False(cut.Find("[data-value=Astro]").HasAttribute("hidden"));
    }

    // ---- single-select ----

    [Fact]
    public void Picking_an_item_reports_it_and_collapses()
    {
        string? value = null;
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => value = v))
            .AddChildContent(Body(Fragments(Item("Next.js"), Item("Astro")))));

        cut.FindAll("[role=option]")[1].Click(); // pick Astro

        Assert.Equal("Astro", value);
        Assert.Equal("false", cut.Find("[data-bz-combobox-input]").GetAttribute("aria-expanded")); // single-select closes
    }

    [Fact]
    public void Picking_an_item_shows_it_in_the_closed_input()
    {
        // Uncontrolled, so the internal value updates and the closed input echoes the chosen label.
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Body(Fragments(Item("Next.js"), Item("Astro")))));

        cut.FindAll("[role=option]")[1].Click(); // pick Astro

        var input = cut.Find("[data-bz-combobox-input]");
        Assert.Equal("false", input.GetAttribute("aria-expanded"));
        Assert.Equal("Astro", input.GetAttribute("value"));
    }

    [Fact]
    public void Closed_single_select_shows_the_selected_value_in_the_input()
    {
        // The chosen value stays visible in the closed field - it is not blanked just because the popup is.
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultValue, "Astro")
            .AddChildContent(Body(Item("Astro"))));

        var input = cut.Find("[data-bz-combobox-input]");
        Assert.Equal("false", input.GetAttribute("aria-expanded"));
        Assert.Equal("Astro", input.GetAttribute("value"));
    }

    [Fact]
    public void Reopening_after_a_selection_shows_every_item_again()
    {
        // Reopening lists every item (not just the chosen one), while the input keeps the selected value
        // visible - the value is only an echo, so it does not filter the list until the user types.
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultValue, "Astro")
            .AddChildContent(Body(Fragments(Item("Next.js"), Item("Astro")))));

        cut.Find("[data-bz-combobox-input]").Click(); // open

        var items = cut.FindAll("[data-bz-combobox-item]");
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.False(i.HasAttribute("hidden")));
        Assert.Equal("Astro", cut.Find("[data-bz-combobox-input]").GetAttribute("value"));
    }

    [Fact]
    public void Typing_after_a_selection_filters_and_shows_the_query()
    {
        // Once the user actually types, the input switches from echoing the value to the live query, and
        // the list filters by it.
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultValue, "Astro")
            .AddChildContent(Body(Fragments(Item("Next.js"), Item("Astro")))));

        cut.Find("[data-bz-combobox-input]").Click(); // open (input echoes "Astro")
        Type(cut, "next");

        Assert.Equal("next", cut.Find("[data-bz-combobox-input]").GetAttribute("value"));
        Assert.False(cut.Find("[data-value='Next.js']").HasAttribute("hidden"));
        Assert.True(cut.Find("[data-value=Astro]").HasAttribute("hidden"));
    }

    [Fact]
    public void Selected_item_is_marked_and_shows_its_indicator()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.DefaultValue, "Astro")
            .AddChildContent(Body(Fragments(Item("Next.js", indicator: true), Item("Astro", indicator: true)))));

        var options = cut.FindAll("[role=option]");
        Assert.Equal("false", options[0].GetAttribute("aria-selected"));
        Assert.Equal("true", options[1].GetAttribute("aria-selected"));
        Assert.True(options[1].HasAttribute("data-selected"));

        // The indicator renders only inside the chosen item.
        var indicators = cut.FindAll("[data-bz-combobox-item-indicator]");
        Assert.Single(indicators);
        Assert.Equal("Astro", indicators[0].ParentElement!.GetAttribute("data-value"));
    }

    [Fact]
    public void A_disabled_item_is_marked_and_ignores_clicks()
    {
        string? value = null;
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => value = v))
            .AddChildContent(Body(Item("Nuxt.js", disabled: true))));

        var item = cut.Find("[data-value='Nuxt.js']");
        Assert.True(item.HasAttribute("data-disabled"));
        Assert.Equal("true", item.GetAttribute("aria-disabled"));

        item.Click();
        Assert.Null(value);
    }

    // ---- multi-select + chips ----

    [Fact]
    public void Multiple_toggles_each_item_and_keeps_the_popup_open()
    {
        // Uncontrolled (DefaultValues seeds internal state) so toggles accumulate; read the selection
        // off the options' aria-selected, like the select tests do.
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.SelectionMode, SelectionMode.Multiple)
            .Add(x => x.DefaultOpen, true)
            .AddChildContent(Body(Fragments(Item("Next.js"), Item("Astro")))));

        cut.FindAll("[role=option]")[0].Click(); // select Next.js
        cut.FindAll("[role=option]")[1].Click(); // select Astro

        var options = cut.FindAll("[role=option]");
        Assert.Equal("true", options[0].GetAttribute("aria-selected"));
        Assert.Equal("true", options[1].GetAttribute("aria-selected"));
        // Toggling does not dismiss a multi-select - it stays open.
        Assert.Equal("true", cut.Find("[data-bz-combobox-input]").GetAttribute("aria-expanded"));

        // Clicking an already-selected item toggles it back off.
        cut.FindAll("[role=option]")[0].Click();
        Assert.Equal("false", cut.FindAll("[role=option]")[0].GetAttribute("aria-selected"));
    }

    [Fact]
    public void Multiple_reports_the_selected_values_via_binding()
    {
        IReadOnlyList<string>? reported = null;
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.SelectionMode, SelectionMode.Multiple)
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.ValuesChanged, EventCallback.Factory.Create<IReadOnlyList<string>>(this, v => reported = v))
            .AddChildContent(Body(Fragments(Item("Next.js"), Item("Astro")))));

        cut.FindAll("[role=option]")[1].Click();

        Assert.Equal(new[] { "Astro" }, reported);
    }

    [Fact]
    public void Multiple_renders_a_chip_per_value_and_removing_one_deselects_it()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.SelectionMode, SelectionMode.Multiple)
            .Add(x => x.DefaultValues, new[] { "Next.js", "Astro" })
            .AddChildContent(ChipsBody(Item("Next.js"))));

        var chips = cut.FindAll("[data-bz-combobox-chip]");
        Assert.Equal(2, chips.Count);
        Assert.Equal("Next.js", chips[0].GetAttribute("data-value"));

        // Remove the first chip - the selection (and so the chip list) drops it.
        chips[0].QuerySelector("[data-bz-combobox-chip-remove]")!.Click();

        chips = cut.FindAll("[data-bz-combobox-chip]");
        Assert.Single(chips);
        Assert.Equal("Astro", chips[0].GetAttribute("data-value"));
    }

    [Fact]
    public void Multiple_backspace_on_an_empty_query_removes_the_last_value()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.SelectionMode, SelectionMode.Multiple)
            .Add(x => x.DefaultValues, new[] { "Next.js", "Astro" })
            .AddChildContent(ChipsBody(Item("Next.js"))));

        cut.Find("[data-bz-combobox-input]").KeyDown(new KeyboardEventArgs { Key = "Backspace" });

        var chips = cut.FindAll("[data-bz-combobox-chip]");
        Assert.Single(chips);
        Assert.Equal("Next.js", chips[0].GetAttribute("data-value")); // the last value (Astro) was removed
    }

    // ---- value display + clear ----

    [Fact]
    public void Clear_appears_only_with_a_selection_and_clears_it()
    {
        // Uncontrolled so the clear actually empties the internal selection and then unmounts itself.
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultValue, "Astro")
            .AddChildContent(b =>
            {
                b.OpenRegion(0);
                Input()(b);
                b.CloseRegion();
                b.OpenComponent<BaseComboboxClear>(1);
                b.AddComponentParameter(2, nameof(BaseComboboxClear.ChildContent), (RenderFragment)(x => x.AddContent(0, "x")));
                b.CloseComponent();
            }));

        cut.Find("[data-bz-combobox-clear]").Click();

        // With nothing selected, the clear button is gone and the query is emptied.
        Assert.Empty(cut.FindAll("[data-bz-combobox-clear]"));
        Assert.Equal("", cut.Find("[data-bz-combobox-input]").GetAttribute("value"));
    }

    [Fact]
    public void Value_shows_a_placeholder_when_nothing_is_selected()
    {
        var cut = Render<BaseCombobox>(p => p.AddChildContent(b =>
        {
            b.OpenComponent<BaseComboboxValue>(0);
            b.AddComponentParameter(1, nameof(BaseComboboxValue.PlaceholderContent), (RenderFragment)(x => x.AddContent(0, "Pick one")));
            b.CloseComponent();
        }));

        Assert.Contains("Pick one", cut.Markup);
    }

    [Fact]
    public void Value_shows_the_current_selection()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.DefaultValue, "Astro")
            .AddChildContent(b =>
            {
                b.OpenComponent<BaseComboboxValue>(0);
                b.AddComponentParameter(1, nameof(BaseComboboxValue.PlaceholderContent), (RenderFragment)(x => x.AddContent(0, "Pick one")));
                b.CloseComponent();
            }));

        Assert.Contains("Astro", cut.Markup);
        Assert.DoesNotContain("Pick one", cut.Markup);
    }

    [Fact]
    public void Trigger_toggles_the_open_state()
    {
        var cut = Render<BaseCombobox>(p => p.AddChildContent(b =>
        {
            b.OpenComponent<BaseComboboxTrigger>(0);
            b.AddComponentParameter(1, nameof(BaseComboboxTrigger.Anchor), true);
            b.AddComponentParameter(2, nameof(BaseComboboxTrigger.ChildContent), (RenderFragment)(x => x.AddContent(0, "open")));
            b.CloseComponent();
        }));

        var trigger = cut.Find("[data-bz-combobox-anchor]");
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));

        trigger.Click();
        Assert.Equal("true", cut.Find("[data-bz-combobox-anchor]").GetAttribute("aria-expanded"));
    }

    // chips container holding a chip-per-value (via the Value render-prop) + the input, then the content.
    private static RenderFragment ChipsBody(RenderFragment items) => b =>
    {
        b.OpenComponent<BaseComboboxChips>(0);
        b.AddComponentParameter(1, nameof(BaseComboboxChips.ChildContent), (RenderFragment)(c =>
        {
            c.OpenComponent<BaseComboboxValue>(0);
            c.AddComponentParameter(1, nameof(BaseComboboxValue.Selection),
                (RenderFragment<IReadOnlyList<string>>)(values => v =>
                {
                    var i = 0;
                    foreach (var val in values)
                    {
                        v.OpenRegion(i++);
                        v.OpenComponent<BaseComboboxChip>(0);
                        v.AddComponentParameter(1, nameof(BaseComboboxChip.Value), val);
                        v.AddComponentParameter(2, nameof(BaseComboboxChip.ChildContent), (RenderFragment)(cc =>
                        {
                            cc.AddContent(0, val);
                            cc.OpenComponent<BaseComboboxChipRemove>(1);
                            cc.AddComponentParameter(2, nameof(BaseComboboxChipRemove.ChildContent), (RenderFragment)(r => r.AddContent(0, "x")));
                            cc.CloseComponent();
                        }));
                        v.CloseComponent();
                        v.CloseRegion();
                    }
                }));
            c.CloseComponent();

            c.OpenRegion(2);
            Input()(c);
            c.CloseRegion();
        }));
        b.CloseComponent();

        b.OpenRegion(3);
        Content(List(items))(b);
        b.CloseRegion();
    };
}
