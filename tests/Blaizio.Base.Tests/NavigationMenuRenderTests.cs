using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render tests for the headless navigation menu. The hover delays + viewport/indicator measurement
/// live in ts/navigationMenu.ts and are verified in-browser; here we cover the C# state machine - the
/// open value, toggling, and that a content relays into the shared viewport (or drops inline when the
/// viewport is off). JSInterop is Loose so the module import is a no-op.
/// </summary>
public class NavigationMenuRenderTests : BunitContext
{
    public NavigationMenuRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // An item "a" with a trigger and a content panel.
    private static RenderFragment Item(string value) => b =>
    {
        b.OpenComponent<BaseNavigationMenuItem>(0);
        b.AddAttribute(1, nameof(BaseNavigationMenuItem.Value), value);
        b.AddAttribute(2, nameof(BaseNavigationMenuItem.ChildContent), (RenderFragment)(ib =>
        {
            ib.OpenComponent<BaseNavigationMenuTrigger>(0);
            ib.AddAttribute(1, nameof(BaseNavigationMenuTrigger.ChildContent), (RenderFragment)(tb => tb.AddContent(0, "Trigger")));
            ib.CloseComponent();
            ib.OpenComponent<BaseNavigationMenuContent>(2);
            ib.AddAttribute(3, nameof(BaseNavigationMenuContent.ChildContent), (RenderFragment)(cb => cb.AddMarkupContent(0, "<span>Panel</span>")));
            ib.CloseComponent();
        }));
        b.CloseComponent();
    };

    // Item "a" with a trigger and a content panel that holds a link ("InPanel"), plus a submenu-less
    // top-level link item ("TopLink") - the shape that exercises hover-to-close.
    private static RenderFragment TriggerItemWithPanelLink(string value) => b =>
    {
        b.OpenComponent<BaseNavigationMenuItem>(0);
        b.AddAttribute(1, nameof(BaseNavigationMenuItem.Value), value);
        b.AddAttribute(2, nameof(BaseNavigationMenuItem.ChildContent), (RenderFragment)(ib =>
        {
            ib.OpenComponent<BaseNavigationMenuTrigger>(0);
            ib.AddAttribute(1, nameof(BaseNavigationMenuTrigger.ChildContent), (RenderFragment)(tb => tb.AddContent(0, "Trigger")));
            ib.CloseComponent();
            ib.OpenComponent<BaseNavigationMenuContent>(2);
            ib.AddAttribute(3, nameof(BaseNavigationMenuContent.ChildContent), (RenderFragment)(cb =>
            {
                cb.OpenComponent<BaseNavigationMenuLink>(0);
                cb.AddAttribute(1, nameof(BaseNavigationMenuLink.ChildContent), (RenderFragment)(lb => lb.AddMarkupContent(0, "<span>InPanel</span>")));
                cb.CloseComponent();
            }));
            ib.CloseComponent();
        }));
        b.CloseComponent();
    };

    private static RenderFragment LinkItem(string value, string label) => b =>
    {
        b.OpenComponent<BaseNavigationMenuItem>(0);
        b.AddAttribute(1, nameof(BaseNavigationMenuItem.Value), value);
        b.AddAttribute(2, nameof(BaseNavigationMenuItem.ChildContent), (RenderFragment)(ib =>
        {
            ib.OpenComponent<BaseNavigationMenuLink>(0);
            ib.AddAttribute(1, nameof(BaseNavigationMenuLink.ChildContent), (RenderFragment)(lb => lb.AddContent(0, label)));
            ib.CloseComponent();
        }));
        b.CloseComponent();
    };

    private IRenderedComponent<BaseNavigationMenu> RenderWithLinks() =>
        Render<BaseNavigationMenu>(ps => ps
            .Add(x => x.OpenDelay, 0)
            .Add(x => x.CloseDelay, 0)
            .AddChildContent(b =>
            {
                b.AddContent(0, TriggerItemWithPanelLink("a"));
                b.AddContent(1, LinkItem("b", "TopLink"));
            }));

    private IRenderedComponent<BaseNavigationMenu> Render(bool viewport = true) =>
        Render<BaseNavigationMenu>(ps => ps.Add(x => x.Viewport, viewport).AddChildContent(Item("a")));

    [Fact]
    public void Renders_root_and_trigger_closed()
    {
        var cut = Render();
        var root = cut.Find("[data-slot=navigation-menu]");
        Assert.Equal("true", root.GetAttribute("data-viewport"));
        Assert.Equal("closed", cut.Find("[data-slot=navigation-menu-trigger]").GetAttribute("data-state"));
        // the viewport is only mounted while something is open (so no empty box lingers while closed)
        Assert.Empty(cut.FindAll("[data-slot=navigation-menu-viewport]"));
    }

    [Fact]
    public async Task Toggle_opens_and_relays_content_into_viewport()
    {
        var cut = Render();
        await cut.InvokeAsync(() => cut.Instance.ToggleAsync("a"));

        Assert.True(cut.Instance.IsOpen("a"));
        Assert.Equal("open", cut.Find("[data-slot=navigation-menu-trigger]").GetAttribute("data-state"));
        Assert.Contains("Panel", cut.Find("[data-slot=navigation-menu-viewport]").InnerHtml);
    }

    [Fact]
    public async Task Toggle_again_closes()
    {
        var cut = Render();
        await cut.InvokeAsync(() => cut.Instance.ToggleAsync("a"));
        await cut.InvokeAsync(() => cut.Instance.ToggleAsync("a"));

        Assert.False(cut.Instance.IsOpen("a"));
        Assert.False(cut.Instance.IsAnyOpen);
        Assert.Equal("closed", cut.Find("[data-slot=navigation-menu-trigger]").GetAttribute("data-state"));
    }

    [Fact]
    public async Task Hover_open_then_click_keeps_open()
    {
        // A trigger opened on hover must not be slammed shut by the click that immediately follows it.
        var cut = Render<BaseNavigationMenu>(ps => ps.Add(x => x.OpenDelay, 0).AddChildContent(Item("a")));
        await cut.InvokeAsync(() => cut.Instance.OpenAsync("a")); // hover-open
        Assert.True(cut.Instance.IsOpen("a"));
        await cut.InvokeAsync(() => cut.Instance.ToggleAsync("a")); // the follow-up click is swallowed
        Assert.True(cut.Instance.IsOpen("a"));
        await cut.InvokeAsync(() => cut.Instance.ToggleAsync("a")); // a real second click then closes it
        Assert.False(cut.Instance.IsAnyOpen);
    }

    [Fact]
    public async Task CloseNow_closes()
    {
        var cut = Render();
        await cut.InvokeAsync(() => cut.Instance.ToggleAsync("a"));
        await cut.InvokeAsync(() => cut.Instance.CloseNowAsync());
        Assert.False(cut.Instance.IsAnyOpen);
    }

    [Fact]
    public async Task Hovering_a_top_level_link_closes_the_open_panel()
    {
        // The reported bug: a submenu-less top-level link left the previously-opened panel open.
        var cut = RenderWithLinks();
        await cut.InvokeAsync(() => cut.Instance.OpenAsync("a"));
        Assert.True(cut.Instance.IsOpen("a"));

        var topLink = cut.FindAll("[data-slot=navigation-menu-link]").Single(e => e.TextContent.Contains("TopLink"));
        await topLink.TriggerEventAsync("onpointerenter", new PointerEventArgs());

        Assert.False(cut.Instance.IsAnyOpen);
    }

    [Fact]
    public async Task Hovering_a_link_inside_the_panel_keeps_it_open()
    {
        // The other half: a link INSIDE the open panel must not close it (would make the panel's own
        // rows un-hoverable). This also proves the content cascades itself down to its links.
        var cut = RenderWithLinks();
        await cut.InvokeAsync(() => cut.Instance.OpenAsync("a"));
        Assert.True(cut.Instance.IsOpen("a"));

        var panelLink = cut.FindAll("[data-slot=navigation-menu-link]").Single(e => e.TextContent.Contains("InPanel"));
        await panelLink.TriggerEventAsync("onpointerenter", new PointerEventArgs());

        Assert.True(cut.Instance.IsOpen("a"));
    }

    [Fact]
    public async Task Viewport_off_renders_content_inline_when_open()
    {
        var cut = Render(viewport: false);
        // no shared viewport in inline mode
        Assert.Empty(cut.FindAll("[data-slot=navigation-menu-viewport]"));
        Assert.Empty(cut.FindAll("[data-slot=navigation-menu-content]")); // not rendered while closed

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync("a"));

        var content = cut.Find("[data-slot=navigation-menu-content]");
        Assert.Contains("Panel", content.InnerHtml);
        Assert.NotNull(content.Closest("[data-slot=navigation-menu-item]")); // dropped inside its item
    }
}
