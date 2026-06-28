using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render tests for the headless navigation menu. The hover delays + viewport/indicator measurement
/// live in ts/navigationMenu.ts and are verified in-browser; here we cover the C# state machine - the
/// open value, toggling, and that a content relays into the shared viewport (or drops inline when the
/// viewport is off). JSInterop is Loose so the module import is a no-op.
/// </summary>
public class NavigationMenuRenderTests : TestContext
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

    private IRenderedComponent<BaseNavigationMenu> Render(bool viewport = true) =>
        RenderComponent<BaseNavigationMenu>(ps => ps.Add(x => x.Viewport, viewport).AddChildContent(Item("a")));

    [Fact]
    public void Renders_root_and_trigger_closed()
    {
        var cut = Render();
        var root = cut.Find("[data-slot=navigation-menu]");
        Assert.Equal("true", root.GetAttribute("data-viewport"));
        Assert.Equal("closed", cut.Find("[data-slot=navigation-menu-trigger]").GetAttribute("data-state"));
        // viewport exists but holds no content yet
        Assert.DoesNotContain("Panel", cut.Find("[data-slot=navigation-menu-viewport]").InnerHtml);
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
    public async Task CloseNow_closes()
    {
        var cut = Render();
        await cut.InvokeAsync(() => cut.Instance.ToggleAsync("a"));
        await cut.InvokeAsync(() => cut.Instance.CloseNowAsync());
        Assert.False(cut.Instance.IsAnyOpen);
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
