using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render + expansion tests for the headless accordion. The close animation/unmount handshake is
/// JS (ts/collapse.ts, verified in-browser); these cover the C# contract: heading/button/region
/// roles, the trigger↔panel ARIA wiring, single/multiple/collapsible rules, and open-panel
/// presence. JSInterop is Loose so the collapse module import is a no-op.
/// </summary>
public class AccordionRenderTests : TestContext
{
    public AccordionRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Items(params string[] values) => builder =>
    {
        var seq = 0;
        foreach (var value in values)
        {
            builder.OpenComponent<BlazeAccordionItem>(seq++);
            builder.AddComponentParameter(seq++, nameof(BlazeAccordionItem.Value), value);
            builder.AddComponentParameter(seq++, nameof(BlazeAccordionItem.ChildContent), (RenderFragment)(item =>
            {
                item.OpenComponent<BlazeAccordionTrigger>(0);
                item.AddComponentParameter(1, nameof(BlazeAccordionTrigger.ChildContent),
                    (RenderFragment)(t => t.AddContent(0, value)));
                item.CloseComponent();
                item.OpenComponent<BlazeAccordionContent>(2);
                item.AddComponentParameter(3, nameof(BlazeAccordionContent.ChildContent),
                    (RenderFragment)(c => c.AddContent(0, $"{value}-panel")));
                item.CloseComponent();
            }));
            builder.CloseComponent();
        }
    };

    [Fact]
    public void Renders_heading_wrapped_triggers_wired_to_their_open_region()
    {
        var cut = RenderComponent<BlazeAccordion>(p => p
            .Add(x => x.DefaultValue, "a")
            .AddChildContent(Items("a", "b")));

        var triggers = cut.FindAll("button");
        Assert.Equal(2, triggers.Count);
        Assert.Equal("H3", triggers[0].ParentElement!.TagName);
        Assert.Equal("true", triggers[0].GetAttribute("aria-expanded"));
        Assert.Equal("open", triggers[0].GetAttribute("data-state"));
        Assert.Equal("false", triggers[1].GetAttribute("aria-expanded"));

        var regions = cut.FindAll("[role=region]");
        Assert.Single(regions);
        Assert.Equal("a-panel", regions[0].TextContent);
        Assert.Equal(triggers[0].GetAttribute("aria-controls"), regions[0].GetAttribute("id"));
        Assert.Equal(triggers[0].GetAttribute("id"), regions[0].GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Single_mode_moves_the_open_item_and_ignores_reclosing_without_collapsible()
    {
        var cut = RenderComponent<BlazeAccordion>(p => p
            .Add(x => x.DefaultValue, "a")
            .AddChildContent(Items("a", "b")));

        cut.FindAll("button")[1].Click();
        Assert.Equal("open", cut.FindAll("button")[1].GetAttribute("data-state"));
        Assert.Equal("closed", cut.FindAll("button")[0].GetAttribute("data-state"));

        // Without Collapsible, pressing the open trigger again keeps it open.
        cut.FindAll("button")[1].Click();
        Assert.Equal("open", cut.FindAll("button")[1].GetAttribute("data-state"));
    }

    [Fact]
    public void Collapsible_single_mode_closes_the_open_item()
    {
        var cut = RenderComponent<BlazeAccordion>(p => p
            .Add(x => x.DefaultValue, "a")
            .Add(x => x.Collapsible, true)
            .AddChildContent(Items("a", "b")));

        cut.FindAll("button")[0].Click();

        Assert.Equal("closed", cut.FindAll("button")[0].GetAttribute("data-state"));
    }

    [Fact]
    public void Multiple_mode_opens_independently()
    {
        var cut = RenderComponent<BlazeAccordion>(p => p
            .Add(x => x.Type, AccordionType.Multiple)
            .AddChildContent(Items("a", "b")));

        cut.FindAll("button")[0].Click();
        cut.FindAll("button")[1].Click();

        Assert.All(cut.FindAll("button"), b => Assert.Equal("open", b.GetAttribute("data-state")));
        Assert.Equal(2, cut.FindAll("[role=region]").Count);
    }

    [Fact]
    public void Disabled_item_does_not_toggle()
    {
        var cut = RenderComponent<BlazeAccordion>(p => p
            .AddChildContent((RenderFragment)(builder =>
            {
                builder.OpenComponent<BlazeAccordionItem>(0);
                builder.AddComponentParameter(1, nameof(BlazeAccordionItem.Value), "locked");
                builder.AddComponentParameter(2, nameof(BlazeAccordionItem.Disabled), true);
                builder.AddComponentParameter(3, nameof(BlazeAccordionItem.ChildContent), (RenderFragment)(item =>
                {
                    item.OpenComponent<BlazeAccordionTrigger>(0);
                    item.CloseComponent();
                }));
                builder.CloseComponent();
            })));

        var trigger = cut.Find("button");
        Assert.True(trigger.HasAttribute("disabled"));
        trigger.Click();
        Assert.Equal("closed", cut.Find("button").GetAttribute("data-state"));
    }
}
