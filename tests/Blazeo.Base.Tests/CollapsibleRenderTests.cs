using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render + toggle tests for the headless collapsible: trigger ARIA, content presence, and the
/// controlled/uncontrolled contract.
/// </summary>
public class CollapsibleRenderTests : TestContext
{
    public CollapsibleRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Body() => builder =>
    {
        builder.OpenComponent<BlazeCollapsibleTrigger>(0);
        builder.AddComponentParameter(1, nameof(BlazeCollapsibleTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Toggle")));
        builder.CloseComponent();
        builder.OpenComponent<BlazeCollapsibleContent>(2);
        builder.AddComponentParameter(3, nameof(BlazeCollapsibleContent.ChildContent),
            (RenderFragment)(c => c.AddContent(0, "Hidden rows")));
        builder.CloseComponent();
    };

    [Fact]
    public void Closed_by_default_with_wired_trigger_and_no_content()
    {
        var cut = RenderComponent<BlazeCollapsible>(p => p.AddChildContent(Body()));

        var trigger = cut.Find("button");
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("closed", trigger.GetAttribute("data-state"));
        Assert.NotNull(trigger.GetAttribute("aria-controls"));
        Assert.DoesNotContain("Hidden rows", cut.Markup);
    }

    [Fact]
    public void Clicking_the_trigger_opens_and_closes_the_content()
    {
        var cut = RenderComponent<BlazeCollapsible>(p => p.AddChildContent(Body()));

        cut.Find("button").Click();
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Contains("Hidden rows", cut.Markup);
        Assert.Equal(cut.Find("button").GetAttribute("aria-controls"),
                     cut.Find("[data-state=open][id]").GetAttribute("id"));

        cut.Find("button").Click();
        Assert.DoesNotContain("Hidden rows", cut.Markup);
    }

    [Fact]
    public void Controlled_collapsible_does_not_self_update_but_raises_change()
    {
        bool? changed = null;
        var cut = RenderComponent<BlazeCollapsible>(p => p
            .Add(x => x.Open, false)
            .Add(x => x.OpenChanged, (bool open) => changed = open)
            .AddChildContent(Body()));

        cut.Find("button").Click();

        Assert.DoesNotContain("Hidden rows", cut.Markup);
        Assert.True(changed);
    }

    [Fact]
    public void Disabled_blocks_toggling()
    {
        var cut = RenderComponent<BlazeCollapsible>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent(Body()));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
        cut.Find("button").Click();
        Assert.DoesNotContain("Hidden rows", cut.Markup);
    }
}
