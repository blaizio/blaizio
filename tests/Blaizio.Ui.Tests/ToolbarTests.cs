using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// BzToolbar and its parts: the three overflow modes and the script wired for each, the expand
/// toggle, floating positions, and what disabled means for a button, a link, a group and a field.
/// </summary>
public class ToolbarTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _scroller;

    public ToolbarTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
        _module = JSInterop.SetupModule("./_content/blaizio.base/dist/toolbar.js");
        _module.Mode = JSRuntimeMode.Loose;
        // What the factory hands back: the instance the bar pages and disposes.
        _scroller = _module.SetupModule(invocation => invocation.Identifier == "createScrollerFromViewport");
        _scroller.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<BzToolbar> Bar(Action<ComponentParameterCollectionBuilder<BzToolbar>>? more = null) =>
        Render<BzToolbar>(p =>
        {
            p.AddUnmatched("aria-label", "Formatting");
            p.AddChildContent<BzToolbarButton>(b => b.AddUnmatched("id", "bold").AddChildContent("Bold"));
            p.AddChildContent<BzToolbarButton>(b => b.AddUnmatched("id", "italic").AddChildContent("Italic"));
            more?.Invoke(p);
        });

    private string[] WiredModes() =>
        [.. _module.Invocations["createScrollerFromViewport"].Select(call => call.Arguments[1] as string ?? "")];

    // ---- the bar ------------------------------------------------------------------------------

    [Fact]
    public void A_plain_bar_is_a_named_toolbar_whose_controls_wrap()
    {
        var cut = Bar();

        var bar = cut.Find("[data-slot=toolbar]");
        Assert.Equal("toolbar", bar.GetAttribute("role"));
        Assert.Equal("Formatting", bar.GetAttribute("aria-label"));
        Assert.Equal("inline", bar.GetAttribute("data-position"));
        Assert.Equal("wrap", bar.GetAttribute("data-overflow"));
        Assert.Contains("flex-wrap", bar.ClassList);
        Assert.False(bar.HasAttribute("aria-orientation"));
        // The controls are the bar's own children: no viewport, no extra buttons, no script.
        Assert.Empty(cut.FindAll("[data-slot=toolbar-viewport]"));
        Assert.Equal(["Bold", "Italic"], cut.FindAll("button").Select(b => b.TextContent.Trim()));
        Assert.Empty(_module.Invocations["createScrollerFromViewport"]);
    }

    [Fact]
    public void A_vertical_bar_says_so_and_ignores_the_overflow_modes()
    {
        var cut = Bar(p => p.Add(x => x.Orientation, Orientation.Vertical).Add(x => x.Overflow, ToolbarOverflow.Scroll));

        var bar = cut.Find("[data-slot=toolbar]");
        Assert.Equal("vertical", bar.GetAttribute("aria-orientation"));
        Assert.Equal("wrap", bar.GetAttribute("data-overflow"));
        Assert.DoesNotContain("flex-wrap", bar.ClassList);
        Assert.Empty(cut.FindAll("[data-slot=toolbar-viewport]"));
        Assert.Equal(2, cut.FindAll("button").Count);
        Assert.Empty(_module.Invocations["createScrollerFromViewport"]);
    }

    [Theory]
    [InlineData(ToolbarPosition.TopStart, "top-start", "top-4", "start-4")]
    [InlineData(ToolbarPosition.TopCenter, "top-center", "top-4", "mx-auto")]
    [InlineData(ToolbarPosition.TopEnd, "top-end", "top-4", "end-4")]
    [InlineData(ToolbarPosition.BottomStart, "bottom-start", "bottom-4", "start-4")]
    [InlineData(ToolbarPosition.BottomCenter, "bottom-center", "bottom-4", "mx-auto")]
    [InlineData(ToolbarPosition.BottomEnd, "bottom-end", "bottom-4", "end-4")]
    [InlineData(ToolbarPosition.StartCenter, "start-center", "start-4", "my-auto")]
    [InlineData(ToolbarPosition.EndCenter, "end-center", "end-4", "my-auto")]
    public void A_floating_bar_is_fixed_to_its_edge_on_a_raised_surface(ToolbarPosition position, string name, string edge, string along)
    {
        var bar = Bar(p => p.Add(x => x.Position, position)).Find("[data-slot=toolbar]");

        Assert.Equal(name, bar.GetAttribute("data-position"));
        Assert.Contains("fixed", bar.ClassList);
        Assert.Contains(edge, bar.ClassList);
        Assert.Contains(along, bar.ClassList);
        Assert.Contains("bg-popover", bar.ClassList);
        Assert.DoesNotContain("bg-background", bar.ClassList);
    }

    [Fact]
    public void An_inline_bar_stays_in_the_flow()
    {
        var bar = Bar().Find("[data-slot=toolbar]");

        Assert.DoesNotContain("fixed", bar.ClassList);
        Assert.Contains("bg-background", bar.ClassList);
        Assert.True(ToolbarPosition.TopEnd.IsFloating());
        Assert.False(ToolbarPosition.Inline.IsFloating());
        Assert.Equal("", ToolbarPosition.Inline.Classes());
    }

    [Fact]
    public void A_class_can_turn_a_floating_bar_into_one_anchored_to_its_container()
    {
        var bar = Bar(p => p.Add(x => x.Position, ToolbarPosition.BottomCenter).Add(x => x.Class, "absolute")).Find("[data-slot=toolbar]");

        Assert.Contains("absolute", bar.ClassList);
        Assert.DoesNotContain("fixed", bar.ClassList);
    }

    // ---- scroll -------------------------------------------------------------------------------

    [Fact]
    public void A_scrolling_bar_keeps_its_controls_in_one_row_between_two_paging_buttons()
    {
        var cut = Bar(p => p.Add(x => x.Overflow, ToolbarOverflow.Scroll));

        Assert.Equal("scroll", cut.Find("[data-slot=toolbar]").GetAttribute("data-overflow"));
        Assert.Equal(["bold", "italic"], cut.Find("[data-slot=toolbar-viewport]").Children.Select(c => c.Id));
        // Split: one at each end of the row.
        Assert.Equal(
            ["Scroll backward", null, null, "Scroll forward"],
            cut.FindAll("button").Select(b => b.GetAttribute("aria-label")));
        // Both wait for the controls to outgrow the bar, and each dims at its own end.
        Assert.All(cut.FindAll("button[aria-label^=Scroll]"), chevron =>
        {
            Assert.Contains("group-not-data-[overflowing=true]/toolbar:hidden", chevron.ClassList);
            Assert.True(chevron.HasAttribute("data-bz-roving-item"));
        });
        Assert.Contains("group-data-[can-scroll-prev=false]/toolbar:opacity-30", cut.Find("button[aria-label='Scroll backward']").ClassList);
        Assert.Contains("group-data-[can-scroll-next=false]/toolbar:opacity-30", cut.Find("button[aria-label='Scroll forward']").ClassList);
        Assert.Equal(["scroll"], WiredModes());
    }

    [Fact]
    public void The_paging_buttons_can_sit_together_at_the_end()
    {
        var cut = Bar(p => p.Add(x => x.Overflow, ToolbarOverflow.Scroll).Add(x => x.ScrollButtons, ToolbarScrollButtons.End));

        Assert.Equal(
            [null, null, "Scroll backward", "Scroll forward"],
            cut.FindAll("button").Select(b => b.GetAttribute("aria-label")));
    }

    [Theory]
    [InlineData("Scroll forward", "next")]
    [InlineData("Scroll backward", "prev")]
    public void A_paging_button_pages_the_row(string label, string direction)
    {
        var cut = Bar(p => p.Add(x => x.Overflow, ToolbarOverflow.Scroll));

        cut.Find($"button[aria-label='{label}']").Click();

        Assert.Equal(direction, _scroller.Invocations["scroll"].Single().Arguments[0]);
    }

    // ---- expand -------------------------------------------------------------------------------

    private const string Toggle = "button[aria-expanded]";

    [Fact]
    public void An_expanding_bar_clips_to_one_row_behind_a_toggle()
    {
        var cut = Bar(p => p.Add(x => x.Overflow, ToolbarOverflow.Expand));

        Assert.Equal("expand", cut.Find("[data-slot=toolbar]").GetAttribute("data-overflow"));
        var viewport = cut.Find("[data-slot=toolbar-viewport]");
        Assert.Equal(["bold", "italic"], viewport.Children.Select(c => c.Id));
        Assert.Contains("max-h-(--bz-toolbar-row)", viewport.ClassList);
        // The keyboard is never sent to a control it cannot see: focus inside lifts the clip.
        Assert.Contains("has-[:focus-visible]:max-h-max", viewport.ClassList);
        var toggle = cut.Find(Toggle);
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Equal("Show all controls", toggle.GetAttribute("aria-label"));
        Assert.Contains("group-not-data-[overflowing=true]/toolbar:hidden", toggle.ClassList);
        Assert.Equal(["expand"], WiredModes());
    }

    [Fact]
    public void The_toggle_opens_and_closes_a_bar_left_to_itself()
    {
        var states = new List<bool>();
        var cut = Bar(p => p
            .Add(x => x.Overflow, ToolbarOverflow.Expand)
            .Add(x => x.ExpandedChanged, (bool open) => states.Add(open)));

        cut.Find(Toggle).Click();

        Assert.Equal([true], states);
        Assert.Equal("true", cut.Find(Toggle).GetAttribute("aria-expanded"));
        Assert.Equal("Show fewer controls", cut.Find(Toggle).GetAttribute("aria-label"));
        var viewport = cut.Find("[data-slot=toolbar-viewport]");
        Assert.Contains("max-h-max", viewport.ClassList);
        Assert.DoesNotContain("max-h-(--bz-toolbar-row)", viewport.ClassList);
        Assert.Contains("rotate-180", cut.Find($"{Toggle} svg").ClassList);

        cut.Find(Toggle).Click();

        Assert.Equal([true, false], states);
        Assert.Equal("false", cut.Find(Toggle).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void DefaultExpanded_starts_the_bar_open()
    {
        var cut = Bar(p => p.Add(x => x.Overflow, ToolbarOverflow.Expand).Add(x => x.DefaultExpanded, true));

        Assert.Equal("true", cut.Find(Toggle).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void A_bound_bar_asks_and_stays_as_the_page_has_it()
    {
        var states = new List<bool>();
        var cut = Bar(p => p
            .Add(x => x.Overflow, ToolbarOverflow.Expand)
            .Add(x => x.Expanded, false)
            .Add(x => x.ExpandedChanged, (bool open) => states.Add(open)));

        cut.Find(Toggle).Click();
        Assert.Equal([true], states);
        Assert.Equal("false", cut.Find(Toggle).GetAttribute("aria-expanded"));

        cut.Render(p => p.Add(x => x.Expanded, true));
        Assert.Equal("true", cut.Find(Toggle).GetAttribute("aria-expanded"));
    }

    // ---- the script follows the mode ----------------------------------------------------------

    [Fact]
    public void Changing_the_overflow_mode_swaps_the_script_wired_for_it()
    {
        var cut = Bar();
        Assert.Empty(WiredModes());

        cut.Render(p => p.Add(x => x.Overflow, ToolbarOverflow.Scroll));
        Assert.Equal(["scroll"], WiredModes());
        Assert.Empty(_scroller.Invocations["dispose"]);

        cut.Render(p => p.Add(x => x.Overflow, ToolbarOverflow.Expand));
        Assert.Equal(["scroll", "expand"], WiredModes());
        Assert.Single(_scroller.Invocations["dispose"]);

        // Back to a mode that needs none: the last instance goes and nothing replaces it.
        cut.Render(p => p.Add(x => x.Overflow, ToolbarOverflow.Wrap));
        Assert.Equal(["scroll", "expand"], WiredModes());
        Assert.Equal(2, _scroller.Invocations["dispose"].Count);
    }

    [Fact]
    public void A_render_that_keeps_the_mode_does_not_wire_again()
    {
        var cut = Bar(p => p.Add(x => x.Overflow, ToolbarOverflow.Scroll));

        cut.Render(p => p.Add(x => x.Loop, false));
        cut.Render(p => p.Add(x => x.Class, "w-64"));

        Assert.Equal(["scroll"], WiredModes());
        Assert.Empty(_scroller.Invocations["dispose"]);
    }

    [Fact]
    public async Task A_bar_that_goes_away_takes_its_script_instance_along()
    {
        Bar(p => p.Add(x => x.Overflow, ToolbarOverflow.Scroll));

        await DisposeComponentsAsync();

        Assert.Single(_scroller.Invocations["dispose"]);
    }

    // ---- buttons ------------------------------------------------------------------------------

    private IRenderedComponent<BzToolbar> WithButton(
        Action<ComponentParameterCollectionBuilder<BzToolbarButton>> button, bool barDisabled = false) =>
        Render<BzToolbar>(p => p
            .Add(x => x.Disabled, barDisabled)
            .AddChildContent<BzToolbarButton>(b => { b.AddChildContent("Save"); button(b); }));

    [Fact]
    public void A_button_is_an_item_of_the_bar_and_runs_its_action()
    {
        var clicks = 0;
        var cut = WithButton(b => b.Add(x => x.OnClick, (MouseEventArgs _) => clicks++));

        var button = cut.Find("[data-slot=toolbar-button]");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.True(button.HasAttribute("data-bz-roving-item"));
        Assert.False(button.HasAttribute("data-reveal"));
        button.Click();

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void A_disabled_button_is_natively_disabled_and_never_runs()
    {
        var clicks = 0;
        var cut = WithButton(b => b.Add(x => x.Disabled, true).Add(x => x.OnClick, (MouseEventArgs _) => clicks++));

        var button = cut.Find("[data-slot=toolbar-button]");
        Assert.True(button.HasAttribute("disabled"));
        Assert.False(button.HasAttribute("aria-disabled"));
        button.Click();

        Assert.Equal(0, clicks);
    }

    [Fact]
    public void A_button_that_stays_focusable_when_disabled_is_announced_and_still_never_runs()
    {
        var clicks = 0;
        var cut = WithButton(b => b
            .Add(x => x.Disabled, true)
            .Add(x => x.FocusableWhenDisabled, true)
            .Add(x => x.OnClick, (MouseEventArgs _) => clicks++));

        var button = cut.Find("[data-slot=toolbar-button]");
        Assert.False(button.HasAttribute("disabled"));
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.True(button.HasAttribute("data-focusable"));
        // Dimmed and pointer-inert like a disabled one, through aria-disabled.
        Assert.Contains("aria-disabled:opacity-50", button.ClassList);
        Assert.Contains("aria-disabled:pointer-events-none", button.ClassList);
        button.Click();

        Assert.Equal(0, clicks);
    }

    [Fact]
    public void A_disabled_bar_disables_every_button_in_it()
    {
        var clicks = 0;
        var cut = WithButton(b => b.Add(x => x.OnClick, (MouseEventArgs _) => clicks++), barDisabled: true);

        Assert.True(cut.Find("[data-slot=toolbar]").HasAttribute("data-disabled"));
        var button = cut.Find("[data-slot=toolbar-button]");
        Assert.True(button.HasAttribute("disabled"));
        button.Click();

        Assert.Equal(0, clicks);
    }

    [Theory]
    [InlineData(ToolbarReveal.None, null, false)]
    [InlineData(ToolbarReveal.Pin, "pin", true)]
    [InlineData(ToolbarReveal.Expand, "expand", false)]
    public void Reveal_is_published_for_the_script_and_pin_moves_the_control_to_the_first_row(ToolbarReveal reveal, string? name, bool first)
    {
        var cut = Render<BzToolbar>(p => p
            .AddChildContent<BzToolbarButton>(b => b.Add(x => x.Reveal, reveal))
            .AddChildContent<BzToolbarLink>(l => l.Add(x => x.Reveal, reveal))
            .AddChildContent<BzToolbarGroup>(g => g.Add(x => x.Reveal, reveal))
            .AddChildContent<BzToolbarInput>(i => i.Add(x => x.Reveal, reveal)));

        Assert.All(
            cut.FindAll("[data-slot=toolbar-button], [data-slot=toolbar-link], [data-slot=toolbar-group], [data-slot=toolbar-input]"),
            control =>
            {
                Assert.Equal(name, control.GetAttribute("data-reveal"));
                Assert.Equal(first, control.ClassList.Contains("order-first"));
            });
    }

    [Fact]
    public void A_button_takes_the_toggle_sizes_and_variants()
    {
        var plain = WithButton(_ => { }).Find("[data-slot=toolbar-button]").ClassName;
        var outline = WithButton(b => b.Add(x => x.Variant, ToggleVariant.Outline).Add(x => x.Size, ToggleSize.Sm).Add(x => x.Class, "px-4"))
            .Find("[data-slot=toolbar-button]");

        Assert.NotEqual(plain, outline.ClassName);
        Assert.Contains("px-4", outline.ClassList);
        Assert.Contains("bz-toolbar-button", outline.ClassList);
    }

    // ---- links, groups, separators, fields ----------------------------------------------------

    [Fact]
    public void A_link_is_an_item_of_the_bar_that_goes_where_it_points()
    {
        var cut = Render<BzToolbar>(p => p.AddChildContent<BzToolbarLink>(l => l.Add(x => x.Href, "/help").AddChildContent("Help")));

        var link = cut.Find("a[data-slot=toolbar-link]");
        Assert.Equal("/help", link.GetAttribute("href"));
        Assert.True(link.HasAttribute("data-bz-roving-item"));
        Assert.Equal("Help", link.TextContent.Trim());
    }

    [Fact]
    public void A_link_in_a_disabled_bar_loses_its_target()
    {
        var cut = Render<BzToolbar>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<BzToolbarLink>(l => l.Add(x => x.Href, "/help").AddChildContent("Help")));

        var link = cut.Find("[data-slot=toolbar-link]");
        Assert.False(link.HasAttribute("href"));
        Assert.Equal("true", link.GetAttribute("aria-disabled"));
    }

    [Fact]
    public void A_disabled_group_disables_its_own_controls_only()
    {
        var cut = Render<BzToolbar>(p => p
            .AddChildContent<BzToolbarGroup>(g => g
                .Add(x => x.Disabled, true)
                .AddUnmatched("aria-label", "History")
                .AddChildContent<BzToolbarButton>(b => b.AddUnmatched("id", "undo")))
            .AddChildContent<BzToolbarButton>(b => b.AddUnmatched("id", "save")));

        var group = cut.Find("[data-slot=toolbar-group]");
        Assert.Equal("group", group.GetAttribute("role"));
        Assert.Equal("History", group.GetAttribute("aria-label"));
        Assert.True(cut.Find("#undo").HasAttribute("disabled"));
        Assert.False(cut.Find("#save").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(Orientation.Horizontal, null, "vertical")]
    [InlineData(Orientation.Vertical, null, "horizontal")]
    [InlineData(Orientation.Horizontal, Orientation.Horizontal, "horizontal")]
    public void A_separator_runs_across_the_bar_unless_told_otherwise(Orientation bar, Orientation? own, string expected)
    {
        var cut = Render<BzToolbar>(p => p
            .Add(x => x.Orientation, bar)
            .AddChildContent<BzToolbarSeparator>(s => s.Add(x => x.Orientation, own)));

        var separator = cut.Find("[data-slot=toolbar-separator]");
        Assert.Equal(expected, separator.GetAttribute("data-orientation"));
        Assert.Contains("bz-toolbar-separator", separator.ClassList);
    }

    [Fact]
    public void A_field_is_an_item_of_the_bar_and_reports_what_is_typed()
    {
        var values = new List<string?>();
        var cut = Render<BzToolbar>(p => p.AddChildContent<BzToolbarInput>(i => i
            .Add(x => x.Value, "draft")
            .Add(x => x.Placeholder, "Search")
            .Add(x => x.Type, "search")
            .Add(x => x.ValueChanged, (string? value) => values.Add(value))
            .AddUnmatched("aria-label", "Search the document")));

        var field = cut.Find("input[data-slot=toolbar-input]");
        Assert.True(field.HasAttribute("data-bz-roving-item"));
        Assert.Equal("draft", field.GetAttribute("value"));
        Assert.Equal("Search", field.GetAttribute("placeholder"));
        Assert.Equal("search", field.GetAttribute("type"));
        Assert.Equal("Search the document", field.GetAttribute("aria-label"));
        field.Input("drafts");

        Assert.Equal(["drafts"], values);
    }

    [Theory]
    [InlineData(true, false)] // its own flag
    [InlineData(false, true)] // the bar's
    public void A_disabled_field_is_natively_disabled(bool own, bool bar)
    {
        var cut = Render<BzToolbar>(p => p
            .Add(x => x.Disabled, bar)
            .AddChildContent<BzToolbarInput>(i => i.Add(x => x.Disabled, own)));

        var field = cut.Find("input[data-slot=toolbar-input]");
        Assert.True(field.HasAttribute("disabled"));
        Assert.True(field.HasAttribute("data-disabled"));
        Assert.False(field.HasAttribute("readonly"));
    }

    [Fact]
    public void A_field_that_stays_focusable_when_disabled_is_read_only_so_its_text_can_be_copied()
    {
        var cut = Render<BzToolbar>(p => p.AddChildContent<BzToolbarInput>(i => i
            .Add(x => x.Value, "v2.4.1")
            .Add(x => x.Disabled, true)
            .Add(x => x.FocusableWhenDisabled, true)));

        var field = cut.Find("input[data-slot=toolbar-input]");
        Assert.False(field.HasAttribute("disabled"));
        Assert.True(field.HasAttribute("readonly"));
        Assert.Equal("true", field.GetAttribute("aria-disabled"));
        Assert.True(field.HasAttribute("data-focusable"));
        Assert.Equal("v2.4.1", field.GetAttribute("value"));
    }
}
