using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + interaction tests for the headless dropdown menu. Positioning (ts/positioning.ts), the
/// presence animation (ts/presence.ts), focus management (ts/focusScope.ts), keyboard + pointer
/// navigation (ts/menu.ts), and outside-pointer-down dismissal (ts/dismissableLayer.ts) are JS,
/// verified in-browser; these cover the C# contract: the trigger's anchor hook + aria wiring,
/// click-to-toggle, role="menu", item selection closing through the presence handshake (and
/// PreventDefault keeping it open), checkbox / radio state, disabled items, controlled binding, and
/// a submenu opening. JSInterop is Loose so module imports are no-ops.
/// </summary>
public class DropdownMenuRenderTests : TestContext
{
    public DropdownMenuRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static void AddTrigger(RenderTreeBuilder b, int seq)
    {
        b.OpenComponent<BaseDropdownMenuTrigger>(seq);
        b.AddComponentParameter(seq + 1, nameof(BaseDropdownMenuTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Menu")));
        b.CloseComponent();
    }

    private static RenderFragment Body(RenderFragment items) => b =>
    {
        AddTrigger(b, 0);
        b.OpenComponent<BaseDropdownMenuContent>(2);
        b.AddComponentParameter(3, nameof(BaseDropdownMenuContent.ChildContent), items);
        b.CloseComponent();
    };

    private static RenderFragment Item(string text, EventCallback<MenuSelectEventArgs> onSelect = default, bool disabled = false) =>
        b =>
        {
            b.OpenComponent<BaseDropdownMenuItem>(0);
            b.AddComponentParameter(1, nameof(BaseDropdownMenuItem.ChildContent), (RenderFragment)(x => x.AddContent(0, text)));
            if (onSelect.HasDelegate) b.AddComponentParameter(2, nameof(BaseDropdownMenuItem.OnSelect), onSelect);
            if (disabled) b.AddComponentParameter(3, nameof(BaseDropdownMenuItem.Disabled), true);
            b.CloseComponent();
        };

    [Fact]
    public void Closed_by_default_with_anchor_hook_and_menu_aria()
    {
        var cut = RenderComponent<BaseDropdownMenu>(p => p.AddChildContent(Body(Item("Profile"))));

        var trigger = cut.Find("button");
        Assert.Equal("closed", trigger.GetAttribute("data-state"));
        Assert.True(trigger.HasAttribute("data-bz-dropdown-menu-anchor"));
        Assert.Equal("menu", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public void Click_opens_then_click_animates_closed_before_unmounting()
    {
        var cut = RenderComponent<BaseDropdownMenu>(p => p.AddChildContent(Body(Item("Profile"))));
        Assert.DoesNotContain("Profile", cut.Markup);

        cut.Find("button").Click();
        var content = cut.Find("[role=menu]");
        Assert.Equal("open", content.GetAttribute("data-state"));
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Contains("Profile", cut.Markup);

        // Clicking the trigger again begins closing; presence keeps it mounted with data-state="closed"
        // until JS reports animationend -> OnCloseFinished, which unmounts it.
        cut.Find("button").Click();
        var contentComponent = cut.FindComponent<BaseDropdownMenuContent>();
        Assert.Equal("closed", contentComponent.Find("[role=menu]").GetAttribute("data-state"));

        cut.InvokeAsync(() => contentComponent.Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public void Open_wires_the_trigger_to_the_content()
    {
        var cut = RenderComponent<BaseDropdownMenu>(p => p.Add(x => x.Open, true).AddChildContent(Body(Item("Profile"))));

        var trigger = cut.Find("button");
        var content = cut.Find("[role=menu]");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        var controls = trigger.GetAttribute("aria-controls");
        Assert.False(string.IsNullOrEmpty(controls));
        Assert.Equal(controls, content.GetAttribute("id"));
        // The content is labelled by the trigger.
        Assert.Equal(trigger.GetAttribute("id"), content.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Escape_on_content_closes_via_handshake()
    {
        var cut = RenderComponent<BaseDropdownMenu>(p => p.AddChildContent(Body(Item("Profile"))));
        cut.Find("button").Click();
        Assert.Single(cut.FindAll("[role=menu]"));

        cut.Find("[role=menu]").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        var contentComponent = cut.FindComponent<BaseDropdownMenuContent>();
        Assert.Equal("closed", contentComponent.Find("[role=menu]").GetAttribute("data-state"));

        cut.InvokeAsync(() => contentComponent.Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public void Selecting_an_item_invokes_OnSelect_and_closes()
    {
        var selected = false;
        var onSelect = EventCallback.Factory.Create<MenuSelectEventArgs>(this, _ => selected = true);
        // Uncontrolled, so RequestClose drives the internal state and the surface actually closes.
        var cut = RenderComponent<BaseDropdownMenu>(p => p.AddChildContent(Body(Item("Profile", onSelect))));
        cut.Find("button").Click();

        cut.Find("[role=menuitem]").Click();

        Assert.True(selected);
        Assert.Equal("closed", cut.Find("[role=menu]").GetAttribute("data-state"));
    }

    [Fact]
    public void Item_OnSelect_preventDefault_keeps_the_menu_open()
    {
        var onSelect = EventCallback.Factory.Create<MenuSelectEventArgs>(this, e => e.PreventDefault());
        var cut = RenderComponent<BaseDropdownMenu>(p => p.Add(x => x.Open, true).AddChildContent(Body(Item("Profile", onSelect))));

        cut.Find("[role=menuitem]").Click();

        Assert.Equal("open", cut.Find("[role=menu]").GetAttribute("data-state"));
    }

    [Fact]
    public void Disabled_item_marks_state_and_ignores_clicks()
    {
        var selected = false;
        var onSelect = EventCallback.Factory.Create<MenuSelectEventArgs>(this, _ => selected = true);
        var cut = RenderComponent<BaseDropdownMenu>(p => p.Add(x => x.Open, true).AddChildContent(Body(Item("Profile", onSelect, disabled: true))));

        var item = cut.Find("[role=menuitem]");
        Assert.Equal("true", item.GetAttribute("aria-disabled"));
        Assert.True(item.HasAttribute("data-disabled"));

        item.Click();
        Assert.False(selected);
        Assert.Equal("open", cut.Find("[role=menu]").GetAttribute("data-state"));
    }

    [Fact]
    public void Checkbox_item_toggles_state_and_reports()
    {
        var value = false;
        // Uncontrolled, so the click toggles the item's own state and it re-renders checked.
        RenderFragment items = b =>
        {
            b.OpenComponent<BaseDropdownMenuCheckboxItem>(0);
            b.AddComponentParameter(1, nameof(BaseDropdownMenuCheckboxItem.CheckedChanged),
                EventCallback.Factory.Create<bool>(this, v => value = v));
            b.AddComponentParameter(2, nameof(BaseDropdownMenuCheckboxItem.OnSelect),
                EventCallback.Factory.Create<MenuSelectEventArgs>(this, e => e.PreventDefault()));
            b.AddComponentParameter(3, nameof(BaseDropdownMenuCheckboxItem.ChildContent), (RenderFragment)(x => x.AddContent(0, "Status Bar")));
            b.CloseComponent();
        };
        var cut = RenderComponent<BaseDropdownMenu>(p => p.Add(x => x.Open, true).AddChildContent(Body(items)));

        var box = cut.Find("[role=menuitemcheckbox]");
        Assert.Equal("false", box.GetAttribute("aria-checked"));

        box.Click();
        Assert.True(value);
        Assert.Equal("true", cut.Find("[role=menuitemcheckbox]").GetAttribute("aria-checked"));
        // PreventDefault kept it open.
        Assert.Equal("open", cut.Find("[role=menu]").GetAttribute("data-state"));
    }

    [Fact]
    public void Radio_item_selection_sets_the_group_value()
    {
        var value = "top";
        RenderFragment items = b =>
        {
            b.OpenComponent<BaseDropdownMenuRadioGroup>(0);
            b.AddComponentParameter(1, nameof(BaseDropdownMenuRadioGroup.Value), value);
            b.AddComponentParameter(2, nameof(BaseDropdownMenuRadioGroup.ValueChanged),
                EventCallback.Factory.Create<string>(this, v => value = v));
            b.AddComponentParameter(3, nameof(BaseDropdownMenuRadioGroup.ChildContent), (RenderFragment)(g =>
            {
                g.OpenComponent<BaseDropdownMenuRadioItem>(0);
                g.AddComponentParameter(1, nameof(BaseDropdownMenuRadioItem.Value), "bottom");
                g.AddComponentParameter(2, nameof(BaseDropdownMenuRadioItem.ChildContent), (RenderFragment)(x => x.AddContent(0, "Bottom")));
                g.CloseComponent();
            }));
            b.CloseComponent();
        };
        var cut = RenderComponent<BaseDropdownMenu>(p => p.Add(x => x.Open, true).AddChildContent(Body(items)));

        var radio = cut.Find("[role=menuitemradio]");
        Assert.Equal("false", radio.GetAttribute("aria-checked"));

        radio.Click();
        Assert.Equal("bottom", value);
    }

    [Fact]
    public void Controlled_menu_renders_when_open_and_closes_via_handshake()
    {
        var cut = RenderComponent<BaseDropdownMenu>(p => p.Add(x => x.Open, true).AddChildContent(Body(Item("Profile"))));
        Assert.Single(cut.FindAll("[role=menu]"));

        cut.SetParametersAndRender(p => p.Add(x => x.Open, false));
        Assert.Equal("closed", cut.Find("[role=menu]").GetAttribute("data-state"));

        cut.InvokeAsync(() => cut.FindComponent<BaseDropdownMenuContent>().Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public void Sub_trigger_opens_the_submenu()
    {
        RenderFragment items = b =>
        {
            b.OpenComponent<BaseDropdownMenuSub>(0);
            b.AddComponentParameter(1, nameof(BaseDropdownMenuSub.ChildContent), (RenderFragment)(s =>
            {
                s.OpenComponent<BaseDropdownMenuSubTrigger>(0);
                s.AddComponentParameter(1, nameof(BaseDropdownMenuSubTrigger.ChildContent), (RenderFragment)(x => x.AddContent(0, "Invite users")));
                s.CloseComponent();
                s.OpenComponent<BaseDropdownMenuSubContent>(2);
                s.AddComponentParameter(3, nameof(BaseDropdownMenuSubContent.ChildContent), Item("Email"));
                s.CloseComponent();
            }));
            b.CloseComponent();
        };
        var cut = RenderComponent<BaseDropdownMenu>(p => p.Add(x => x.Open, true).AddChildContent(Body(items)));

        // Root menu only; submenu closed.
        Assert.Single(cut.FindAll("[role=menu]"));
        var subTrigger = cut.Find("[data-bz-dropdown-menu-sub-anchor]");
        Assert.Equal("false", subTrigger.GetAttribute("aria-expanded"));

        subTrigger.Click();

        // Submenu surface now mounted alongside the root.
        Assert.Equal(2, cut.FindAll("[role=menu]").Count);
        Assert.Equal("true", cut.Find("[data-bz-dropdown-menu-sub-anchor]").GetAttribute("aria-expanded"));
    }
}
