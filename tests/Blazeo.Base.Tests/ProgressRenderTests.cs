using Bunit;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>Render tests for the headless progress bar: ARIA value semantics, data-state, indicator.</summary>
public class ProgressRenderTests : TestContext
{
    [Fact]
    public void Loading_value_sets_role_and_aria_value_semantics()
    {
        var cut = RenderComponent<BlazeProgress>(p => p.Add(x => x.Value, 60));

        var bar = cut.Find("div");
        Assert.Equal("progressbar", bar.GetAttribute("role"));
        Assert.Equal("0", bar.GetAttribute("aria-valuemin"));
        Assert.Equal("100", bar.GetAttribute("aria-valuemax"));
        Assert.Equal("60", bar.GetAttribute("aria-valuenow"));
        Assert.Equal("60%", bar.GetAttribute("aria-valuetext"));
        Assert.Equal("loading", bar.GetAttribute("data-state"));
        Assert.Equal("60", bar.GetAttribute("data-value"));
        Assert.Equal("100", bar.GetAttribute("data-max"));
    }

    [Fact]
    public void Value_equal_to_max_is_complete()
    {
        var cut = RenderComponent<BlazeProgress>(p => p.Add(x => x.Value, 100));

        Assert.Equal("complete", cut.Find("div").GetAttribute("data-state"));
    }

    [Fact]
    public void Null_value_is_indeterminate_and_omits_value_attributes()
    {
        var cut = RenderComponent<BlazeProgress>();

        var bar = cut.Find("div");
        Assert.Equal("indeterminate", bar.GetAttribute("data-state"));
        Assert.False(bar.HasAttribute("aria-valuenow"));
        Assert.False(bar.HasAttribute("data-value"));
    }

    [Fact]
    public void Custom_max_scales_the_percentage_label()
    {
        var cut = RenderComponent<BlazeProgress>(p => p
            .Add(x => x.Value, 5)
            .Add(x => x.Max, 10));

        var bar = cut.Find("div");
        Assert.Equal("10", bar.GetAttribute("aria-valuemax"));
        Assert.Equal("5", bar.GetAttribute("aria-valuenow"));
        Assert.Equal("50%", bar.GetAttribute("aria-valuetext"));
        Assert.Equal("loading", bar.GetAttribute("data-state"));
    }

    [Fact]
    public void Value_is_clamped_to_the_range()
    {
        var over = RenderComponent<BlazeProgress>(p => p.Add(x => x.Value, 150));
        Assert.Equal("100", over.Find("div").GetAttribute("aria-valuenow"));
        Assert.Equal("complete", over.Find("div").GetAttribute("data-state"));

        var under = RenderComponent<BlazeProgress>(p => p.Add(x => x.Value, -20));
        Assert.Equal("0", under.Find("div").GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void Indicator_mirrors_value_and_state_via_context()
    {
        var cut = RenderComponent<BlazeProgress>(p => p
            .Add(x => x.Value, 40)
            .AddChildContent<BlazeProgressIndicator>());

        // Outer is the progressbar; the indicator is the nested div.
        var indicator = cut.FindAll("div")[1];
        Assert.Equal("loading", indicator.GetAttribute("data-state"));
        Assert.Equal("40", indicator.GetAttribute("data-value"));
        Assert.Equal("100", indicator.GetAttribute("data-max"));
    }
}
