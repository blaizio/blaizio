using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render tests for the headless toolbar. The roving-focus keyboard nav is JS (verified
/// in-browser); these cover the C# contract: role="toolbar" over roving markers, the two disabled
/// shapes (focusable-when-disabled vs native), bar-wide and group-scoped disabling, the link's
/// inert form, and the toggle-group handover - a BaseToggleGroup inside a toolbar must NOT start a
/// second roving-focus composite. JSInterop is Loose so module imports are no-ops.
/// </summary>
public class ToolbarRenderTests : BunitContext
{
    public ToolbarRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Controls(bool disabledButton = false, bool focusable = false) => builder =>
    {
        var seq = 0;
        builder.OpenComponent<BaseToolbarButton>(seq++);
        builder.AddComponentParameter(seq++, nameof(BaseToolbarButton.Disabled), disabledButton);
        builder.AddComponentParameter(seq++, nameof(BaseToolbarButton.FocusableWhenDisabled), focusable);
        builder.CloseComponent();
        builder.OpenComponent<BaseToolbarLink>(seq++);
        builder.AddComponentParameter(seq++, nameof(BaseToolbarLink.Href), "/docs");
        builder.CloseComponent();
    };

    [Fact]
    public void Renders_role_toolbar_with_roving_items_and_no_tabindex()
    {
        var cut = Render<BaseToolbar>(p => p.AddChildContent(Controls()));

        var bar = cut.Find("[role=toolbar]");
        // aria-orientation defaults to horizontal on role="toolbar" - only the override is stated.
        Assert.False(bar.HasAttribute("aria-orientation"));
        Assert.Equal("horizontal", bar.GetAttribute("data-orientation"));

        var items = cut.FindAll("[data-bz-roving-item]");
        Assert.Equal(2, items.Count);
        foreach (var item in items)
            Assert.False(item.HasAttribute("tabindex")); // owned by the roving-focus script

        Assert.Equal("button", items[0].GetAttribute("type"));
        Assert.Equal("/docs", items[1].GetAttribute("href"));
    }

    [Fact]
    public void Vertical_toolbar_states_its_orientation()
    {
        var cut = Render<BaseToolbar>(p => p
            .Add(x => x.Orientation, Orientation.Vertical)
            .AddChildContent(Controls()));

        var bar = cut.Find("[role=toolbar]");
        Assert.Equal("vertical", bar.GetAttribute("aria-orientation"));
        Assert.Equal("vertical", bar.GetAttribute("data-orientation"));
    }

    [Fact]
    public void Disabled_button_renders_the_native_attribute_by_default()
    {
        var cut = Render<BaseToolbar>(p => p.AddChildContent(Controls(disabledButton: true)));

        var button = cut.Find("button[data-bz-roving-item]");
        Assert.True(button.HasAttribute("disabled"));
        Assert.False(button.HasAttribute("aria-disabled"));
        Assert.False(button.HasAttribute("data-focusable"));
    }

    [Fact]
    public void FocusableWhenDisabled_keeps_the_button_reachable_and_swallows_the_click()
    {
        var clicked = false;
        var cut = Render<BaseToolbar>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<BaseToolbarButton>(0);
            builder.AddComponentParameter(1, nameof(BaseToolbarButton.Disabled), true);
            builder.AddComponentParameter(2, nameof(BaseToolbarButton.FocusableWhenDisabled), true);
            builder.AddComponentParameter(3, nameof(BaseToolbarButton.OnClick),
                EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => clicked = true));
            builder.CloseComponent();
        }));

        var button = cut.Find("[data-bz-roving-item]");
        Assert.False(button.HasAttribute("disabled")); // focusable route: no native attribute
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.True(button.HasAttribute("data-disabled"));
        Assert.True(button.HasAttribute("data-focusable"));

        button.Click();
        Assert.False(clicked);
    }

    [Fact]
    public void Disabling_the_bar_reaches_buttons_and_makes_links_inert()
    {
        var cut = Render<BaseToolbar>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent(Controls()));

        Assert.True(cut.Find("[role=toolbar]").HasAttribute("data-disabled"));

        var button = cut.Find("button[data-bz-roving-item]");
        Assert.True(button.HasAttribute("disabled"));

        // The link withholds its href - an href-less <a> is unfocusable and inert.
        var link = cut.Find("a[data-bz-roving-item]");
        Assert.False(link.HasAttribute("href"));
        Assert.Equal("true", link.GetAttribute("aria-disabled"));
    }

    [Fact]
    public void Disabling_a_group_scopes_to_its_controls()
    {
        var cut = Render<BaseToolbar>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<BaseToolbarButton>(0);
            builder.CloseComponent();
            builder.OpenComponent<BaseToolbarGroup>(1);
            builder.AddComponentParameter(2, nameof(BaseToolbarGroup.Disabled), true);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<BaseToolbarButton>(0);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }));

        var group = cut.Find("[role=group]");
        Assert.True(group.HasAttribute("data-disabled"));

        var buttons = cut.FindAll("button[data-bz-roving-item]");
        Assert.False(buttons[0].HasAttribute("disabled")); // outside the group: live
        Assert.True(buttons[1].HasAttribute("disabled")); // inside: disabled
    }

    [Fact]
    public void Toggle_group_inside_a_toolbar_yields_navigation_to_the_bar()
    {
        var cut = Render<BaseToolbar>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<BaseToggleGroup>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<BaseToggleGroupItem>(0);
                inner.AddComponentParameter(1, nameof(BaseToggleGroupItem.Value), "a");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }));

        // Exactly ONE roving-focus composite: the toolbar's. The nested group renders a plain
        // role="group" (still carrying data-orientation for the styled axis variants).
        Assert.Equal(1, JSInterop.Invocations.Count(i => i.Identifier == "createRovingFocus"));
        var group = cut.Find("[role=group]");
        Assert.Equal("horizontal", group.GetAttribute("data-orientation"));

        // Its item still toggles through the group's own click contract.
        cut.Find("[data-bz-roving-item][data-state]").Click();
        Assert.Equal("on", cut.Find("[data-bz-roving-item][data-state]").GetAttribute("data-state"));
    }

    [Fact]
    public void Disabling_the_bar_reaches_a_nested_toggle_group()
    {
        var cut = Render<BaseToolbar>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<BaseToggleGroup>(0);
                builder.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<BaseToggleGroupItem>(0);
                    inner.AddComponentParameter(1, nameof(BaseToggleGroupItem.Value), "a");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }));

        var item = cut.Find("[data-bz-roving-item][data-state]");
        Assert.True(item.HasAttribute("disabled"));

        item.Click();
        Assert.Equal("off", cut.Find("[data-bz-roving-item][data-state]").GetAttribute("data-state"));
    }

    [Fact]
    public void Focusable_disabled_input_goes_readonly_instead_of_disabled()
    {
        var cut = Render<BaseToolbar>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<BaseToolbarInput>(0);
            builder.AddComponentParameter(1, nameof(BaseToolbarInput.Disabled), true);
            builder.AddComponentParameter(2, nameof(BaseToolbarInput.FocusableWhenDisabled), true);
            builder.CloseComponent();
        }));

        var input = cut.Find("input[data-bz-roving-item]");
        Assert.False(input.HasAttribute("disabled"));
        Assert.True(input.HasAttribute("readonly"));
        Assert.Equal("true", input.GetAttribute("aria-disabled"));
        Assert.True(input.HasAttribute("data-focusable"));
    }

    [Fact]
    public void Disabled_input_renders_the_native_attribute_by_default()
    {
        var cut = Render<BaseToolbar>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<BaseToolbarInput>(0);
            builder.AddComponentParameter(1, nameof(BaseToolbarInput.Disabled), true);
            builder.CloseComponent();
        }));

        var input = cut.Find("input[data-bz-roving-item]");
        Assert.True(input.HasAttribute("disabled"));
        Assert.False(input.HasAttribute("readonly"));
        Assert.False(input.HasAttribute("aria-disabled"));
    }
}
