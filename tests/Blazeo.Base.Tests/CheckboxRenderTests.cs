using Bunit;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>Render + interaction tests for the headless checkbox: tri-state ARIA, data-state, indicator.</summary>
public class CheckboxRenderTests : TestContext
{
    [Fact]
    public void Renders_an_unchecked_checkbox_button_by_default()
    {
        var cut = RenderComponent<BlazeCheckbox>();

        cut.MarkupMatches("<button type=\"button\" role=\"checkbox\" aria-checked=\"false\" data-state=\"unchecked\"></button>");
    }

    [Fact]
    public void DefaultChecked_renders_checked()
    {
        var cut = RenderComponent<BlazeCheckbox>(p => p.Add(x => x.DefaultChecked, CheckedState.Checked));

        var button = cut.Find("button");
        Assert.Equal("checked", button.GetAttribute("data-state"));
        Assert.Equal("true", button.GetAttribute("aria-checked"));
    }

    [Fact]
    public void Indeterminate_reports_mixed_and_data_state()
    {
        var cut = RenderComponent<BlazeCheckbox>(p => p.Add(x => x.DefaultChecked, CheckedState.Indeterminate));

        var button = cut.Find("button");
        Assert.Equal("indeterminate", button.GetAttribute("data-state"));
        Assert.Equal("mixed", button.GetAttribute("aria-checked"));
    }

    [Fact]
    public void Click_toggles_unchecked_to_checked()
    {
        var cut = RenderComponent<BlazeCheckbox>();

        cut.Find("button").Click();

        Assert.Equal("checked", cut.Find("button").GetAttribute("data-state"));
    }

    [Fact]
    public void Click_from_indeterminate_resolves_to_checked()
    {
        var cut = RenderComponent<BlazeCheckbox>(p => p.Add(x => x.DefaultChecked, CheckedState.Indeterminate));

        cut.Find("button").Click();

        var button = cut.Find("button");
        Assert.Equal("checked", button.GetAttribute("data-state"));
        Assert.Equal("true", button.GetAttribute("aria-checked"));
    }

    [Fact]
    public void Disabled_emits_attributes_and_blocks_toggling()
    {
        var cut = RenderComponent<BlazeCheckbox>(p => p.Add(x => x.Disabled, true));

        var button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
        Assert.True(button.HasAttribute("data-disabled"));

        cut.Find("button").Click();
        Assert.Equal("unchecked", cut.Find("button").GetAttribute("data-state"));
    }

    [Fact]
    public void Controlled_does_not_self_update_but_raises_change()
    {
        CheckedState? changed = null;
        var cut = RenderComponent<BlazeCheckbox>(p => p
            .Add(x => x.Checked, CheckedState.Unchecked)
            .Add(x => x.CheckedChanged, (CheckedState v) => changed = v));

        cut.Find("button").Click();

        Assert.Equal("unchecked", cut.Find("button").GetAttribute("data-state"));
        Assert.Equal(CheckedState.Checked, changed);
    }

    [Fact]
    public void Indicator_is_absent_when_unchecked_and_present_once_checked()
    {
        var cut = RenderComponent<BlazeCheckbox>(p => p.AddChildContent<BlazeCheckboxIndicator>());

        // Unchecked → indicator not rendered (no indicator element when unchecked).
        Assert.Empty(cut.FindAll("span"));

        cut.Find("button").Click();

        // Checked → indicator span appears mirroring the state.
        var indicator = cut.Find("span");
        Assert.Equal("checked", indicator.GetAttribute("data-state"));
    }

    [Fact]
    public void Indicator_renders_when_indeterminate()
    {
        var cut = RenderComponent<BlazeCheckbox>(p => p
            .Add(x => x.DefaultChecked, CheckedState.Indeterminate)
            .AddChildContent<BlazeCheckboxIndicator>());

        Assert.Equal("indeterminate", cut.Find("span").GetAttribute("data-state"));
    }
}
