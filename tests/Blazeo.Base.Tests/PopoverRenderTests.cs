using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render + open/close tests for the headless popover. Positioning (ts/positioning.ts), the fade/zoom
/// presence (ts/presence.ts), focus management (ts/focusScope.ts) and outside-pointer-down dismissal
/// (ts/dismissableLayer.ts) are JS, verified in-browser; these cover the C# contract: the trigger's
/// anchor hook + aria wiring, click-to-toggle, role="dialog", Escape via onkeydown, closing through
/// the presence handshake, and controlled binding. JSInterop is Loose so module imports are no-ops.
/// Outside-pointer-down dismissal is browser-only and not reachable here.
/// </summary>
public class PopoverRenderTests : TestContext
{
    public PopoverRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Body() => builder =>
    {
        builder.OpenComponent<BzPopoverTrigger>(0);
        builder.AddComponentParameter(1, nameof(BzPopoverTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Trigger")));
        builder.CloseComponent();
        builder.OpenComponent<BzPopoverContent>(2);
        builder.AddComponentParameter(3, nameof(BzPopoverContent.ChildContent),
            (RenderFragment)(c => c.AddContent(0, "Panel body")));
        builder.CloseComponent();
    };

    [Fact]
    public void Closed_by_default_with_anchor_hook_and_no_content()
    {
        var cut = RenderComponent<BzPopover>(p => p.AddChildContent(Body()));

        var trigger = cut.Find("button");
        Assert.Equal("closed", trigger.GetAttribute("data-state"));
        Assert.True(trigger.HasAttribute("data-bz-popover-anchor"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Click_opens_then_click_animates_closed_before_unmounting()
    {
        var cut = RenderComponent<BzPopover>(p => p.AddChildContent(Body()));
        Assert.DoesNotContain("Panel body", cut.Markup);

        // Click toggles open.
        cut.Find("button").Click();
        var content = cut.Find("[role=dialog]");
        Assert.Equal("open", content.GetAttribute("data-state"));
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Contains("Panel body", cut.Markup);

        // Clicking again begins closing; presence keeps it mounted with data-state="closed" until JS
        // reports animationend -> OnCloseFinished, which unmounts it.
        cut.Find("button").Click();
        var contentComponent = cut.FindComponent<BzPopoverContent>();
        Assert.Equal("closed", contentComponent.Find("[role=dialog]").GetAttribute("data-state"));
        Assert.Contains("Panel body", cut.Markup);

        cut.InvokeAsync(() => contentComponent.Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Open_controls_the_trigger_with_the_content_id()
    {
        var cut = RenderComponent<BzPopover>(p => p.Add(x => x.Open, true).AddChildContent(Body()));

        var trigger = cut.Find("button");
        var content = cut.Find("[role=dialog]");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        var controls = trigger.GetAttribute("aria-controls");
        Assert.False(string.IsNullOrEmpty(controls));
        Assert.Equal(controls, content.GetAttribute("id"));
    }

    [Fact]
    public void Escape_on_content_closes_via_handshake()
    {
        var cut = RenderComponent<BzPopover>(p => p.AddChildContent(Body()));
        cut.Find("button").Click(); // open (uncontrolled, so SetOpen drives the internal state)
        Assert.Single(cut.FindAll("[role=dialog]"));

        cut.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        var contentComponent = cut.FindComponent<BzPopoverContent>();
        Assert.Equal("closed", contentComponent.Find("[role=dialog]").GetAttribute("data-state"));

        cut.InvokeAsync(() => contentComponent.Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Controlled_popover_renders_when_open_and_closes_via_handshake()
    {
        var cut = RenderComponent<BzPopover>(p => p.Add(x => x.Open, true).AddChildContent(Body()));
        Assert.Single(cut.FindAll("[role=dialog]"));

        cut.SetParametersAndRender(p => p.Add(x => x.Open, false));
        Assert.Equal("closed", cut.Find("[role=dialog]").GetAttribute("data-state"));

        cut.InvokeAsync(() => cut.FindComponent<BzPopoverContent>().Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }
}
