using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render + interaction tests for the headless context menu. Positioning (ts/positioning.ts), the
/// presence animation (ts/presence.ts), keyboard + pointer navigation (ts/menu.ts), and
/// outside-pointer-down dismissal (ts/dismissableLayer.ts) are JS, verified in-browser; these cover
/// the C# contract: a right-click opens the menu and parks the 0x0 anchor marker at the pointer, the
/// reused BzDropdownMenu* items work inside it (selection closes through the presence handshake),
/// Escape closes, and the content carries no aria-labelledby (there is no trigger to name it).
/// JSInterop is Loose so module imports are no-ops.
/// </summary>
public class ContextMenuRenderTests : TestContext
{
    public ContextMenuRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Body(RenderFragment items) => b =>
    {
        b.OpenComponent<BzContextMenuTrigger>(0);
        b.AddComponentParameter(1, nameof(BzContextMenuTrigger.ChildContent), (RenderFragment)(t => t.AddContent(0, "Area")));
        b.CloseComponent();
        b.OpenComponent<BzContextMenuContent>(2);
        b.AddComponentParameter(3, nameof(BzContextMenuContent.ChildContent), items);
        b.CloseComponent();
    };

    private static RenderFragment Item(string text, EventCallback<MenuSelectEventArgs> onSelect = default) => b =>
    {
        b.OpenComponent<BzDropdownMenuItem>(0);
        b.AddComponentParameter(1, nameof(BzDropdownMenuItem.ChildContent), (RenderFragment)(x => x.AddContent(0, text)));
        if (onSelect.HasDelegate) b.AddComponentParameter(2, nameof(BzDropdownMenuItem.OnSelect), onSelect);
        b.CloseComponent();
    };

    [Fact]
    public void Closed_by_default_renders_no_menu_but_keeps_trigger_and_marker()
    {
        var cut = RenderComponent<BzContextMenu>(p => p.AddChildContent(Body(Item("Back"))));

        Assert.Empty(cut.FindAll("[role=menu]"));
        Assert.True(cut.Find("[data-bz-context-menu-trigger]").HasAttribute("data-bz-context-menu-trigger"));
        // The anchor marker is always present (so the close animation never loses its anchor).
        Assert.Contains("position:fixed", cut.Find("[data-bz-context-menu-anchor]").GetAttribute("style"));
    }

    [Fact]
    public void Right_click_opens_the_menu_and_parks_the_marker_at_the_point()
    {
        var cut = RenderComponent<BzContextMenu>(p => p.AddChildContent(Body(Item("Back"))));

        cut.Find("[data-bz-context-menu-trigger]").TriggerEvent("oncontextmenu", new MouseEventArgs { ClientX = 120, ClientY = 240 });

        var menu = cut.Find("[role=menu]");
        Assert.Equal("open", menu.GetAttribute("data-state"));
        Assert.Contains("Back", cut.Markup);
        var style = cut.Find("[data-bz-context-menu-anchor]").GetAttribute("style");
        Assert.Contains("left:120px", style);
        Assert.Contains("top:240px", style);
    }

    [Fact]
    public void Selecting_an_item_invokes_OnSelect_and_closes()
    {
        var selected = false;
        var onSelect = EventCallback.Factory.Create<MenuSelectEventArgs>(this, _ => selected = true);
        var cut = RenderComponent<BzContextMenu>(p => p.AddChildContent(Body(Item("Back", onSelect))));
        cut.Find("[data-bz-context-menu-trigger]").TriggerEvent("oncontextmenu", new MouseEventArgs { ClientX = 10, ClientY = 10 });

        cut.Find("[role=menuitem]").Click();

        Assert.True(selected);
        Assert.Equal("closed", cut.Find("[role=menu]").GetAttribute("data-state"));
    }

    [Fact]
    public void Escape_on_content_closes_via_handshake()
    {
        var cut = RenderComponent<BzContextMenu>(p => p.AddChildContent(Body(Item("Back"))));
        cut.Find("[data-bz-context-menu-trigger]").TriggerEvent("oncontextmenu", new MouseEventArgs { ClientX = 10, ClientY = 10 });
        Assert.Single(cut.FindAll("[role=menu]"));

        cut.Find("[role=menu]").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        var content = cut.FindComponent<BzContextMenuContent>();
        Assert.Equal("closed", content.Find("[role=menu]").GetAttribute("data-state"));

        cut.InvokeAsync(() => content.Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public void Content_is_a_vertical_menu_without_aria_labelledby()
    {
        var cut = RenderComponent<BzContextMenu>(p => p.Add(x => x.Open, true).AddChildContent(Body(Item("Back"))));

        var menu = cut.Find("[role=menu]");
        Assert.Equal("vertical", menu.GetAttribute("aria-orientation"));
        // No trigger names a context menu, so aria-labelledby is omitted.
        Assert.False(menu.HasAttribute("aria-labelledby"));
    }
}
