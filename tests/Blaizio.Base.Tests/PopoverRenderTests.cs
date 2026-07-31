using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + open/close tests for the headless popover. Positioning (ts/positioning.ts), the fade/zoom
/// presence (ts/presence.ts), focus management (ts/focusScope.ts) and outside-pointer-down dismissal
/// (ts/dismissableLayer.ts) are JS, verified in-browser; these cover the C# contract: the trigger's
/// anchor hook + aria wiring, click-to-toggle, role="dialog", Escape via onkeydown, closing through
/// the presence handshake, and controlled binding. JSInterop is Loose so module imports are no-ops.
/// Outside-pointer-down dismissal is browser-only and not reachable here.
/// </summary>
public class PopoverRenderTests : BunitContext
{
    public PopoverRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Body() => builder =>
    {
        builder.OpenComponent<BasePopoverTrigger>(0);
        builder.AddComponentParameter(1, nameof(BasePopoverTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Trigger")));
        builder.CloseComponent();
        builder.OpenComponent<BasePopoverContent>(2);
        builder.AddComponentParameter(3, nameof(BasePopoverContent.ChildContent),
            (RenderFragment)(c => c.AddContent(0, "Panel body")));
        builder.CloseComponent();
    };

    [Fact]
    public void Closed_by_default_with_anchor_hook_and_no_content()
    {
        var cut = Render<BasePopover>(p => p.AddChildContent(Body()));

        var trigger = cut.Find("button");
        Assert.Equal("closed", trigger.GetAttribute("data-state"));
        Assert.True(trigger.HasAttribute("data-bz-popover-anchor"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Click_opens_then_click_animates_closed_before_unmounting()
    {
        var cut = Render<BasePopover>(p => p.AddChildContent(Body()));
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
        var contentComponent = cut.FindComponent<BasePopoverContent>();
        Assert.Equal("closed", contentComponent.Find("[role=dialog]").GetAttribute("data-state"));
        Assert.Contains("Panel body", cut.Markup);

        cut.InvokeAsync(() => contentComponent.Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Open_controls_the_trigger_with_the_content_id()
    {
        var cut = Render<BasePopover>(p => p.Add(x => x.Open, true).AddChildContent(Body()));

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
        var cut = Render<BasePopover>(p => p.AddChildContent(Body()));
        cut.Find("button").Click(); // open (uncontrolled, so SetOpen drives the internal state)
        Assert.Single(cut.FindAll("[role=dialog]"));

        cut.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        var contentComponent = cut.FindComponent<BasePopoverContent>();
        Assert.Equal("closed", contentComponent.Find("[role=dialog]").GetAttribute("data-state"));

        cut.InvokeAsync(() => contentComponent.Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Controlled_popover_renders_when_open_and_closes_via_handshake()
    {
        var cut = Render<BasePopover>(p => p.Add(x => x.Open, true).AddChildContent(Body()));
        Assert.Single(cut.FindAll("[role=dialog]"));

        cut.Render(p => p.Add(x => x.Open, false));
        Assert.Equal("closed", cut.Find("[role=dialog]").GetAttribute("data-state"));

        cut.InvokeAsync(() => cut.FindComponent<BasePopoverContent>().Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    // ---- accessible naming ----

    private static RenderFragment NamedBody(bool title, bool description) => builder =>
    {
        builder.OpenComponent<BasePopoverTrigger>(0);
        builder.AddComponentParameter(1, nameof(BasePopoverTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Trigger")));
        builder.CloseComponent();
        builder.OpenComponent<BasePopoverContent>(2);
        builder.AddComponentParameter(3, nameof(BasePopoverContent.ChildContent), (RenderFragment)(c =>
        {
            if (title)
            {
                c.OpenComponent<BasePopoverTitle>(0);
                c.AddComponentParameter(1, nameof(BasePopoverTitle.ChildContent), (RenderFragment)(x => x.AddContent(0, "Dimensions")));
                c.CloseComponent();
            }
            if (description)
            {
                c.OpenComponent<BasePopoverDescription>(2);
                c.AddComponentParameter(3, nameof(BasePopoverDescription.ChildContent), (RenderFragment)(x => x.AddContent(0, "Set the dimensions.")));
                c.CloseComponent();
            }
            c.AddContent(4, "Panel body");
        }));
        builder.CloseComponent();
    };

    [Fact]
    public void Title_and_description_name_the_dialog()
    {
        var cut = Render<BasePopover>(p => p.Add(x => x.Open, true).AddChildContent(NamedBody(true, true)));

        var content = cut.Find("[role=dialog]");
        var title = cut.Find($"#{content.GetAttribute("aria-labelledby")}");
        var description = cut.Find($"#{content.GetAttribute("aria-describedby")}");
        Assert.Equal("Dimensions", title.TextContent);
        Assert.Equal("h2", title.TagName, ignoreCase: true);
        Assert.Equal("Set the dimensions.", description.TextContent);
        Assert.Equal("p", description.TagName, ignoreCase: true);
    }

    [Fact]
    public void Without_title_or_description_no_reference_dangles()
    {
        var cut = Render<BasePopover>(p => p.Add(x => x.Open, true).AddChildContent(NamedBody(false, false)));

        var content = cut.Find("[role=dialog]");
        Assert.False(content.HasAttribute("aria-labelledby"));
        Assert.False(content.HasAttribute("aria-describedby"));
    }

    [Fact]
    public void Consumer_aria_labelledby_wins_over_the_generated_wiring()
    {
        RenderFragment body = builder =>
        {
            builder.OpenComponent<BasePopoverContent>(0);
            builder.AddComponentParameter(1, nameof(BasePopoverContent.Attributes),
                (IReadOnlyDictionary<string, object>)new Dictionary<string, object> { ["aria-labelledby"] = "custom-name" });
            builder.AddComponentParameter(2, nameof(BasePopoverContent.ChildContent), (RenderFragment)(c =>
            {
                c.OpenComponent<BasePopoverTitle>(0);
                c.AddComponentParameter(1, nameof(BasePopoverTitle.ChildContent), (RenderFragment)(x => x.AddContent(0, "Dimensions")));
                c.CloseComponent();
            }));
            builder.CloseComponent();
        };
        var cut = Render<BasePopover>(p => p.Add(x => x.Open, true).AddChildContent(body));

        Assert.Equal("custom-name", cut.Find("[role=dialog]").GetAttribute("aria-labelledby"));
    }
}
