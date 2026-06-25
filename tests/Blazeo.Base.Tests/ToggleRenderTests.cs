using Bunit;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>Render + interaction tests for the headless toggle: ARIA, data-state, and controllability.</summary>
public class ToggleRenderTests : TestContext
{
    [Fact]
    public void Renders_an_off_button_by_default()
    {
        var cut = RenderComponent<BaseToggle>();

        cut.MarkupMatches("<button type=\"button\" aria-pressed=\"false\" data-state=\"off\"></button>");
    }

    [Fact]
    public void DefaultPressed_renders_on()
    {
        var cut = RenderComponent<BaseToggle>(p => p.Add(x => x.DefaultPressed, true));

        var button = cut.Find("button");
        Assert.Equal("on", button.GetAttribute("data-state"));
        Assert.Equal("true", button.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Click_toggles_uncontrolled_state()
    {
        var cut = RenderComponent<BaseToggle>();

        cut.Find("button").Click();

        var button = cut.Find("button");
        Assert.Equal("on", button.GetAttribute("data-state"));
        Assert.Equal("true", button.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Disabled_emits_attributes_and_blocks_toggling()
    {
        var cut = RenderComponent<BaseToggle>(p => p.Add(x => x.Disabled, true));

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
        var cut = RenderComponent<BaseToggle>(p => p
            .Add(x => x.Pressed, true)
            .Add(x => x.PressedChanged, (bool v) => changed = v));

        cut.Find("button").Click();

        // Controlled: the button stays "on" until the parent flows a new value back in…
        Assert.Equal("on", cut.Find("button").GetAttribute("data-state"));
        // …but the toggled value was announced.
        Assert.False(changed);
    }
}
