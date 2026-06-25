using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render + open/close tests for the headless tooltip. Positioning (ts/positioning.ts over
/// @floating-ui) and the fade/zoom exit-animation presence (ts/presence.ts) are JS, verified
/// in-browser; these cover the C# contract: the trigger's anchor hook + aria-describedby wiring,
/// role="tooltip", opening on focus, closing via the presence handshake, disabled, and controlled
/// binding. JSInterop is Loose so the module imports are no-ops. The hover-delay path is timing
/// based and exercised in-browser rather than here.
/// </summary>
public class TooltipRenderTests : TestContext
{
    public TooltipRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Body() => builder =>
    {
        builder.OpenComponent<BaseTooltipTrigger>(0);
        builder.AddComponentParameter(1, nameof(BaseTooltipTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Trigger")));
        builder.CloseComponent();
        builder.OpenComponent<BaseTooltipContent>(2);
        builder.AddComponentParameter(3, nameof(BaseTooltipContent.ChildContent),
            (RenderFragment)(c => c.AddContent(0, "Tip body")));
        builder.CloseComponent();
    };

    [Fact]
    public void Closed_by_default_with_anchor_hook_and_no_content()
    {
        var cut = RenderComponent<BaseTooltip>(p => p.AddChildContent(Body()));

        var trigger = cut.Find("button");
        Assert.Equal("closed", trigger.GetAttribute("data-state"));
        Assert.True(trigger.HasAttribute("data-bz-tooltip-anchor"));
        Assert.Null(trigger.GetAttribute("aria-describedby"));
        Assert.Empty(cut.FindAll("[role=tooltip]"));
    }

    [Fact]
    public void Focus_opens_then_blur_animates_closed_before_unmounting()
    {
        var cut = RenderComponent<BaseTooltip>(p => p.AddChildContent(Body()));
        Assert.DoesNotContain("Tip body", cut.Markup);

        // Keyboard focus opens immediately (no hover delay).
        cut.Find("button").FocusIn(new FocusEventArgs());
        var content = cut.Find("[role=tooltip]");
        Assert.Equal("open", content.GetAttribute("data-state"));
        Assert.Contains("Tip body", cut.Markup);

        // Blur begins closing; presence keeps it mounted with data-state="closed" until JS reports
        // animationend -> OnCloseFinished, which unmounts it.
        cut.Find("button").FocusOut(new FocusEventArgs());
        var contentComponent = cut.FindComponent<BaseTooltipContent>();
        Assert.Equal("closed", contentComponent.Find("[role=tooltip]").GetAttribute("data-state"));
        Assert.Contains("Tip body", cut.Markup);

        cut.InvokeAsync(() => contentComponent.Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=tooltip]"));
    }

    [Fact]
    public void Open_describes_the_trigger_with_the_content_id()
    {
        var cut = RenderComponent<BaseTooltip>(p => p.Add(x => x.Open, true).AddChildContent(Body()));

        var trigger = cut.Find("button");
        var content = cut.Find("[role=tooltip]");
        var describedby = trigger.GetAttribute("aria-describedby");
        Assert.False(string.IsNullOrEmpty(describedby));
        Assert.Equal(describedby, content.GetAttribute("id"));
    }

    [Fact]
    public void Disabled_does_not_open_on_focus()
    {
        var cut = RenderComponent<BaseTooltip>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent(Body()));

        cut.Find("button").FocusIn(new FocusEventArgs());

        Assert.Empty(cut.FindAll("[role=tooltip]"));
    }

    [Fact]
    public void Controlled_tooltip_renders_when_open_and_closes_via_handshake()
    {
        var cut = RenderComponent<BaseTooltip>(p => p.Add(x => x.Open, true).AddChildContent(Body()));
        Assert.Single(cut.FindAll("[role=tooltip]"));

        cut.SetParametersAndRender(p => p.Add(x => x.Open, false));
        Assert.Equal("closed", cut.Find("[role=tooltip]").GetAttribute("data-state"));

        cut.InvokeAsync(() => cut.FindComponent<BaseTooltipContent>().Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=tooltip]"));
    }

    [Fact]
    public void Arrow_renders_inside_the_open_content()
    {
        var cut = RenderComponent<BaseTooltip>(p => p.Add(x => x.Open, true).AddChildContent(builder =>
        {
            builder.OpenComponent<BaseTooltipContent>(0);
            builder.AddComponentParameter(1, nameof(BaseTooltipContent.ChildContent), (RenderFragment)(c =>
            {
                c.AddContent(0, "Tip body");
                c.OpenComponent<BaseTooltipArrow>(1);
                c.CloseComponent();
            }));
            builder.CloseComponent();
        }));

        Assert.Single(cut.FindAll("[data-bz-arrow]"));
    }

    [Fact]
    public void Provider_skips_the_delay_while_a_tooltip_is_open()
    {
        var ctx = new TooltipProviderContext(delayDuration: 700, skipDelayDuration: 300);

        Assert.False(ctx.ShouldSkipDelay);   // nothing open yet -> the next hover waits the full delay
        ctx.NotifyOpen();
        Assert.True(ctx.ShouldSkipDelay);    // one open -> an adjacent trigger opens immediately
        ctx.NotifyClose();
        Assert.True(ctx.ShouldSkipDelay);    // still inside the skip window right after the last closes

        ctx.Dispose();
    }
}
