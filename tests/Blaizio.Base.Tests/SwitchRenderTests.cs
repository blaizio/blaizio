using Bunit;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>Render + interaction tests for the headless switch and its context-driven thumb.</summary>
public class SwitchRenderTests : BunitContext
{
    [Fact]
    public void Renders_an_unchecked_switch_button_by_default()
    {
        var cut = Render<BaseSwitch>();

        cut.MarkupMatches("<button type=\"button\" role=\"switch\" aria-checked=\"false\" data-state=\"unchecked\"></button>");
    }

    [Fact]
    public void DefaultChecked_renders_checked()
    {
        var cut = Render<BaseSwitch>(p => p.Add(x => x.DefaultChecked, true));

        var button = cut.Find("button");
        Assert.Equal("checked", button.GetAttribute("data-state"));
        Assert.Equal("true", button.GetAttribute("aria-checked"));
    }

    [Fact]
    public void Click_toggles_uncontrolled_state()
    {
        var cut = Render<BaseSwitch>();

        cut.Find("button").Click();

        Assert.Equal("checked", cut.Find("button").GetAttribute("data-state"));
    }

    [Fact]
    public void Disabled_emits_attributes_and_blocks_toggling()
    {
        var cut = Render<BaseSwitch>(p => p.Add(x => x.Disabled, true));

        var button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
        Assert.True(button.HasAttribute("data-disabled"));

        cut.Find("button").Click();
        Assert.Equal("unchecked", cut.Find("button").GetAttribute("data-state"));
    }

    [Fact]
    public void Controlled_does_not_self_update_but_raises_change()
    {
        bool? changed = null;
        var cut = Render<BaseSwitch>(p => p
            .Add(x => x.Checked, false)
            .Add(x => x.CheckedChanged, (bool v) => changed = v));

        cut.Find("button").Click();

        Assert.Equal("unchecked", cut.Find("button").GetAttribute("data-state"));
        Assert.True(changed);   // the toggled value was announced
    }

    [Fact]
    public void Thumb_mirrors_the_switch_state_via_context_and_follows_clicks()
    {
        var cut = Render<BaseSwitch>(p => p.AddChildContent<BaseSwitchThumb>());

        // Starts unchecked → thumb mirrors it.
        Assert.Equal("unchecked", cut.Find("span").GetAttribute("data-state"));

        cut.Find("button").Click();

        // After toggling, the cascaded context re-renders the thumb to "checked".
        Assert.Equal("checked", cut.Find("span").GetAttribute("data-state"));
    }

    [Fact]
    public void Disabled_switch_cascades_data_disabled_to_thumb()
    {
        var cut = Render<BaseSwitch>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<BaseSwitchThumb>());

        Assert.True(cut.Find("span").HasAttribute("data-disabled"));
    }
}
