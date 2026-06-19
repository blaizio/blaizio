using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render + open/close tests for the headless hover card. Positioning (ts/positioning.ts), the
/// fade/zoom presence (ts/presence.ts) and Escape / outside-pointer-down dismissal
/// (ts/dismissableLayer.ts) are JS, verified in-browser; these cover the C# contract: the trigger's
/// anchor hook, the content appearing when open with its id, the dismiss request closing through the
/// presence handshake, and controlled binding. The hover/focus open+close DELAY paths are timing
/// based and exercised in-browser rather than here. JSInterop is Loose so module imports are no-ops.
/// The content carries data-side (the trigger does not), so it doubles as the "is the card shown" hook.
/// </summary>
public class HoverCardRenderTests : TestContext
{
    public HoverCardRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Body() => builder =>
    {
        builder.OpenComponent<BzHoverCardTrigger>(0);
        builder.AddComponentParameter(1, nameof(BzHoverCardTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Trigger")));
        builder.CloseComponent();
        builder.OpenComponent<BzHoverCardContent>(2);
        builder.AddComponentParameter(3, nameof(BzHoverCardContent.ChildContent),
            (RenderFragment)(c => c.AddContent(0, "Card body")));
        builder.CloseComponent();
    };

    [Fact]
    public void Closed_by_default_with_anchor_hook_and_no_content()
    {
        var cut = RenderComponent<BzHoverCard>(p => p.AddChildContent(Body()));

        var trigger = cut.Find("[data-bz-hover-card-anchor]");
        Assert.Equal("closed", trigger.GetAttribute("data-state"));
        Assert.DoesNotContain("Card body", cut.Markup);
        Assert.Empty(cut.FindAll("[data-side]"));
    }

    [Fact]
    public void Open_renders_the_content_with_an_id_and_open_state()
    {
        var cut = RenderComponent<BzHoverCard>(p => p.Add(x => x.Open, true).AddChildContent(Body()));

        var content = cut.Find("[data-side]");
        Assert.Equal("open", content.GetAttribute("data-state"));
        Assert.False(string.IsNullOrEmpty(content.GetAttribute("id")));
        Assert.Contains("Card body", cut.Markup);
        Assert.Equal("open", cut.Find("[data-bz-hover-card-anchor]").GetAttribute("data-state"));
    }

    [Fact]
    public void Dismiss_request_animates_closed_then_unmounts_via_handshake()
    {
        // Uncontrolled (DefaultOpen) so the dismiss actually drives the internal state to closed.
        var cut = RenderComponent<BzHoverCard>(p => p.Add(x => x.DefaultOpen, true).AddChildContent(Body()));
        Assert.Single(cut.FindAll("[data-side]"));

        var contentComponent = cut.FindComponent<BzHoverCardContent>();
        cut.InvokeAsync(() => contentComponent.Instance.OnDismissRequested());

        // Presence keeps it mounted with data-state="closed" until JS reports animationend.
        Assert.Equal("closed", contentComponent.Find("[data-side]").GetAttribute("data-state"));
        Assert.Contains("Card body", cut.Markup);

        cut.InvokeAsync(() => contentComponent.Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[data-side]"));
    }

    [Fact]
    public void Controlled_hover_card_renders_when_open_and_closes_via_handshake()
    {
        var cut = RenderComponent<BzHoverCard>(p => p.Add(x => x.Open, true).AddChildContent(Body()));
        Assert.Single(cut.FindAll("[data-side]"));

        cut.SetParametersAndRender(p => p.Add(x => x.Open, false));
        Assert.Equal("closed", cut.Find("[data-side]").GetAttribute("data-state"));

        cut.InvokeAsync(() => cut.FindComponent<BzHoverCardContent>().Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[data-side]"));
    }
}
