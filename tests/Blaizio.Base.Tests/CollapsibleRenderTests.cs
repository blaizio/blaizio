using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + toggle tests for the headless collapsible: trigger ARIA, content presence, and the
/// controlled/uncontrolled contract.
/// </summary>
public class CollapsibleRenderTests : BunitContext
{
    public CollapsibleRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Body() => builder =>
    {
        builder.OpenComponent<BaseCollapsibleTrigger>(0);
        builder.AddComponentParameter(1, nameof(BaseCollapsibleTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Toggle")));
        builder.CloseComponent();
        builder.OpenComponent<BaseCollapsibleContent>(2);
        builder.AddComponentParameter(3, nameof(BaseCollapsibleContent.ChildContent),
            (RenderFragment)(c => c.AddContent(0, "Hidden rows")));
        builder.CloseComponent();
    };

    [Fact]
    public void Closed_by_default_with_wired_trigger_and_no_content()
    {
        var cut = Render<BaseCollapsible>(p => p.AddChildContent(Body()));

        var trigger = cut.Find("button");
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("closed", trigger.GetAttribute("data-state"));
        Assert.NotNull(trigger.GetAttribute("aria-controls"));
        Assert.DoesNotContain("Hidden rows", cut.Markup);
    }

    [Fact]
    public void Clicking_the_trigger_opens_then_animates_closed_before_unmounting()
    {
        var cut = Render<BaseCollapsible>(p => p.AddChildContent(Body()));

        cut.Find("button").Click();
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Contains("Hidden rows", cut.Markup);
        Assert.Equal(cut.Find("button").GetAttribute("aria-controls"),
                     cut.Find("[data-state=open][id]").GetAttribute("id"));

        // Closing keeps the panel mounted with data-state="closed" so its exit animation can play;
        // ts/collapse.js reports animationend -> OnCloseFinished, which then unmounts it.
        cut.Find("button").Click();
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        var content = cut.FindComponent<BaseCollapsibleContent>();
        Assert.Equal("closed", content.Find("[data-state]").GetAttribute("data-state"));
        Assert.Contains("Hidden rows", cut.Markup);

        cut.InvokeAsync(() => content.Instance.OnCloseFinished());
        Assert.DoesNotContain("Hidden rows", cut.Markup);
    }

    [Fact]
    public void Controlled_collapsible_does_not_self_update_but_raises_change()
    {
        bool? changed = null;
        var cut = Render<BaseCollapsible>(p => p
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
        var cut = Render<BaseCollapsible>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent(Body()));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
        cut.Find("button").Click();
        Assert.DoesNotContain("Hidden rows", cut.Markup);
    }

    private static RenderFragment KeptBody() => builder =>
    {
        builder.OpenComponent<BaseCollapsibleTrigger>(0);
        builder.AddComponentParameter(1, nameof(BaseCollapsibleTrigger.ChildContent),
            (RenderFragment)(t => t.AddContent(0, "Toggle")));
        builder.CloseComponent();
        builder.OpenComponent<BaseCollapsibleContent>(2);
        builder.AddComponentParameter(3, nameof(BaseCollapsibleContent.KeepRendered), true);
        builder.AddComponentParameter(4, nameof(BaseCollapsibleContent.ChildContent),
            (RenderFragment)(c => c.AddContent(0, "Kept rows")));
        builder.CloseComponent();
    };

    [Fact]
    public void Kept_rendered_content_starts_in_the_page_hidden_until_found()
    {
        var cut = Render<BaseCollapsible>(p => p.AddChildContent(KeptBody()));

        var content = cut.Find("[data-state][id]");
        Assert.Equal("until-found", content.GetAttribute("hidden"));
        Assert.Equal("closed", content.GetAttribute("data-state"));
        Assert.Contains("Kept rows", content.TextContent);
        Assert.Equal(cut.Find("button").GetAttribute("aria-controls"), content.GetAttribute("id"));
    }

    [Fact]
    public void Kept_rendered_content_is_hidden_after_its_close_animation_instead_of_removed()
    {
        var cut = Render<BaseCollapsible>(p => p.AddChildContent(KeptBody()));

        cut.Find("button").Click();
        Assert.Equal("open", cut.Find("[data-state][id]").GetAttribute("data-state"));
        Assert.False(cut.Find("[data-state][id]").HasAttribute("hidden"));

        // Closing plays the exit animation first, exactly like a removable panel.
        cut.Find("button").Click();
        var content = cut.FindComponent<BaseCollapsibleContent>();
        Assert.Equal("closed", content.Find("[id]").GetAttribute("data-state"));
        Assert.False(content.Find("[id]").HasAttribute("hidden"));

        cut.InvokeAsync(() => content.Instance.OnCloseFinished());
        Assert.Equal("until-found", content.Find("[id]").GetAttribute("hidden"));
        Assert.Contains("Kept rows", cut.Markup);
    }

    [Fact]
    public void A_find_in_page_match_opens_the_collapsible()
    {
        bool? changed = null;
        var cut = Render<BaseCollapsible>(p => p
            .Add(x => x.OpenChanged, (bool open) => changed = open)
            .AddChildContent(KeptBody()));

        var content = cut.FindComponent<BaseCollapsibleContent>();
        cut.InvokeAsync(() => content.Instance.OnBeforeMatch());

        Assert.True(changed);
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.False(content.Find("[id]").HasAttribute("hidden"));
    }
}
