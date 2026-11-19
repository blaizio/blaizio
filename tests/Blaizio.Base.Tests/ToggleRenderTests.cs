using Bunit;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>Render + interaction tests for the headless toggle: ARIA, data-state, and controllability.</summary>
public class ToggleRenderTests : BunitContext
{
    [Fact]
    public void Renders_an_off_button_by_default()
    {
        var cut = Render<BaseToggle>();

        cut.MarkupMatches("<button type=\"button\" aria-pressed=\"false\" data-state=\"off\"></button>");
    }

    [Fact]
    public void DefaultPressed_renders_on()
    {
        var cut = Render<BaseToggle>(p => p.Add(x => x.DefaultPressed, true));

        var button = cut.Find("button");
        Assert.Equal("on", button.GetAttribute("data-state"));
        Assert.Equal("true", button.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Click_toggles_uncontrolled_state()
    {
        var cut = Render<BaseToggle>();

        cut.Find("button").Click();

        var button = cut.Find("button");
        Assert.Equal("on", button.GetAttribute("data-state"));
        Assert.Equal("true", button.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Disabled_emits_attributes_and_blocks_toggling()
    {
        var cut = Render<BaseToggle>(p => p.Add(x => x.Disabled, true));

        var button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
        Assert.True(button.HasAttribute("data-disabled"));

        cut.Find("button").Click();
        Assert.Equal("off", cut.Find("button").GetAttribute("data-state"));
    }

    [Fact]
    public void Controlled_does_not_self_update_but_raises_change()
    {
        bool? changed = null;
        var cut = Render<BaseToggle>(p => p
            .Add(x => x.Pressed, true)
            .Add(x => x.PressedChanged, (bool v) => changed = v));

        cut.Find("button").Click();

        // Controlled: the button stays "on" until the parent flows a new value back in…
        Assert.Equal("on", cut.Find("button").GetAttribute("data-state"));
        // …but the toggled value was announced.
        Assert.False(changed);
    }

    // The toggle's behaviour on an element of the consumer's own, here a span.
    private static readonly Microsoft.AspNetCore.Components.RenderFragment<BzRenderProps> AsSpan = props => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddMultipleAttributes(1, props.Attributes);
        builder.AddContent(2, "Bold");
        builder.CloseElement();
    };

    [Fact]
    public void On_another_element_the_toggle_keeps_its_state_and_click_but_not_the_button_only_attributes()
    {
        var cut = Render<BaseToggle>(p => p.Add(x => x.RenderAs, AsSpan));

        var element = cut.Find("span");
        Assert.Equal("false", element.GetAttribute("aria-pressed"));
        Assert.False(element.HasAttribute("type"));
        Assert.False(element.HasAttribute("aria-disabled"));

        element.Click();

        Assert.Equal("true", cut.Find("span").GetAttribute("aria-pressed"));
        Assert.Equal("on", cut.Find("span").GetAttribute("data-state"));
    }

    [Fact]
    public void On_another_element_disabled_is_told_to_assistive_tools_and_still_blocks_the_click()
    {
        var cut = Render<BaseToggle>(p => p
            .Add(x => x.RenderAs, AsSpan)
            .Add(x => x.Disabled, true));

        var element = cut.Find("span");
        // A span takes no native disabled: nothing would stop its click or announce the state.
        Assert.False(element.HasAttribute("disabled"));
        Assert.Equal("true", element.GetAttribute("aria-disabled"));
        Assert.True(element.HasAttribute("data-disabled"));

        element.Click();

        Assert.Equal("off", cut.Find("span").GetAttribute("data-state"));
    }
}
