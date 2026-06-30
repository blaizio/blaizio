using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + value-math tests for the headless slider. The pointer drag and keyboard nav are JS
/// (verified in-browser); these cover the C# contract: the domain/orientation data-attributes, the
/// thumbs' roles + ARIA + position fractions, the filled range, and the OnInput math the JS module
/// calls back into (snap to step, clamp to the domain, keep thumbs apart, controlled vs uncontrolled).
/// JSInterop is Loose so the slider module import is a no-op.
/// </summary>
public class SliderRenderTests : TestContext
{
    public SliderRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // BaseSliderTrack(+Range) followed by one BaseSliderThumb per index - the shape the styled layer builds.
    private static RenderFragment Parts(int thumbCount) => builder =>
    {
        var seq = 0;
        builder.OpenComponent<BaseSliderTrack>(seq++);
        builder.AddComponentParameter(seq++, nameof(BaseSliderTrack.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<BaseSliderRange>(0);
            inner.CloseComponent();
        }));
        builder.CloseComponent();

        for (var i = 0; i < thumbCount; i++)
        {
            builder.OpenComponent<BaseSliderThumb>(seq++);
            builder.AddComponentParameter(seq++, nameof(BaseSliderThumb.Index), i);
            builder.CloseComponent();
        }
    };

    [Fact]
    public void Root_exposes_the_domain_and_orientation()
    {
        var cut = RenderComponent<BaseSlider>(p => p
            .Add(x => x.Min, 0)
            .Add(x => x.Max, 50)
            .Add(x => x.Step, 5)
            .AddChildContent(Parts(1)));

        var root = cut.Find("[data-bz-slider]");
        Assert.Equal("horizontal", root.GetAttribute("data-orientation"));
        Assert.Equal("0", root.GetAttribute("data-min"));
        Assert.Equal("50", root.GetAttribute("data-max"));
        Assert.Equal("5", root.GetAttribute("data-step"));
    }

    [Fact]
    public void Default_value_renders_one_thumb_with_aria_and_a_position_fraction()
    {
        var cut = RenderComponent<BaseSlider>(p => p
            .Add(x => x.DefaultValue, [50])
            .AddChildContent(Parts(1)));

        var thumbs = cut.FindAll("[role=slider]");
        var thumb = Assert.Single(thumbs);
        Assert.Equal("0", thumb.GetAttribute("aria-valuemin"));
        Assert.Equal("100", thumb.GetAttribute("aria-valuemax"));
        Assert.Equal("50", thumb.GetAttribute("aria-valuenow"));
        Assert.Equal("horizontal", thumb.GetAttribute("aria-orientation"));
        Assert.Equal("0", thumb.GetAttribute("data-bz-slider-index"));
        Assert.Equal("0", thumb.GetAttribute("tabindex"));
        Assert.Contains("--bz-slider-thumb-fraction:0.5", thumb.GetAttribute("style"));
    }

    [Fact]
    public void A_range_renders_a_thumb_per_value_with_the_fill_spanning_them()
    {
        var cut = RenderComponent<BaseSlider>(p => p
            .Add(x => x.DefaultValue, [25, 75])
            .AddChildContent(Parts(2)));

        var thumbs = cut.FindAll("[role=slider]");
        Assert.Equal(2, thumbs.Count);
        Assert.Equal("25", thumbs[0].GetAttribute("aria-valuenow"));
        Assert.Equal("75", thumbs[1].GetAttribute("aria-valuenow"));

        var range = cut.Find("[data-bz-slider-range]").GetAttribute("style");
        Assert.Contains("--bz-slider-range-start:0.25", range);
        Assert.Contains("--bz-slider-range-end:0.75", range);
    }

    [Fact]
    public void A_single_thumb_fills_from_the_track_origin()
    {
        var cut = RenderComponent<BaseSlider>(p => p
            .Add(x => x.DefaultValue, [40])
            .AddChildContent(Parts(1)));

        var range = cut.Find("[data-bz-slider-range]").GetAttribute("style");
        Assert.Contains("--bz-slider-range-start:0;", range);
        Assert.Contains("--bz-slider-range-end:0.4", range);
    }

    [Fact]
    public void Inverted_flips_the_position_fraction()
    {
        var cut = RenderComponent<BaseSlider>(p => p
            .Add(x => x.Inverted, true)
            .Add(x => x.DefaultValue, [25])
            .AddChildContent(Parts(1)));

        Assert.Contains("--bz-slider-thumb-fraction:0.75", cut.Find("[role=slider]").GetAttribute("style"));
    }

    [Fact]
    public void Vertical_marks_the_orientation_on_the_parts()
    {
        var cut = RenderComponent<BaseSlider>(p => p
            .Add(x => x.Orientation, Orientation.Vertical)
            .Add(x => x.DefaultValue, [50])
            .AddChildContent(Parts(1)));

        Assert.Equal("vertical", cut.Find("[data-bz-slider]").GetAttribute("data-orientation"));
        Assert.Equal("vertical", cut.Find("[data-bz-slider-track]").GetAttribute("data-orientation"));
        Assert.Equal("vertical", cut.Find("[role=slider]").GetAttribute("aria-orientation"));
    }

    [Fact]
    public void Disabled_marks_the_root_and_drops_the_thumb_from_the_tab_order()
    {
        var cut = RenderComponent<BaseSlider>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.DefaultValue, [50])
            .AddChildContent(Parts(1)));

        Assert.True(cut.Find("[data-bz-slider]").HasAttribute("data-disabled"));

        var thumb = cut.Find("[role=slider]");
        Assert.True(thumb.HasAttribute("data-disabled"));
        Assert.Equal("true", thumb.GetAttribute("aria-disabled"));
        Assert.False(thumb.HasAttribute("tabindex"));
    }

    [Fact]
    public async Task OnInput_snaps_to_the_step_and_clamps_to_the_domain()
    {
        var cut = RenderComponent<BaseSlider>(p => p
            .Add(x => x.Step, 10)
            .Add(x => x.DefaultValue, [40])
            .AddChildContent(Parts(1)));

        await cut.InvokeAsync(() => cut.Instance.OnInput(0, 73)); // 73 -> nearest step (70)
        Assert.Equal("70", cut.Find("[role=slider]").GetAttribute("aria-valuenow"));

        await cut.InvokeAsync(() => cut.Instance.OnInput(0, 999)); // above Max -> 100
        Assert.Equal("100", cut.Find("[role=slider]").GetAttribute("aria-valuenow"));

        await cut.InvokeAsync(() => cut.Instance.OnInput(0, -20)); // below Min -> 0
        Assert.Equal("0", cut.Find("[role=slider]").GetAttribute("aria-valuenow"));
    }

    [Fact]
    public async Task OnInput_keeps_adjacent_thumbs_min_steps_apart()
    {
        var cut = RenderComponent<BaseSlider>(p => p
            .Add(x => x.Step, 1)
            .Add(x => x.MinStepsBetweenThumbs, 10)
            .Add(x => x.DefaultValue, [20, 50])
            .AddChildContent(Parts(2)));

        // Dragging the lower thumb up to 45 can't cross within 10 of the upper (50) -> clamps to 40.
        await cut.InvokeAsync(() => cut.Instance.OnInput(0, 45));
        Assert.Equal("40", cut.FindAll("[role=slider]")[0].GetAttribute("aria-valuenow"));
        Assert.Equal("50", cut.FindAll("[role=slider]")[1].GetAttribute("aria-valuenow"));
    }

    [Fact]
    public async Task Controlled_slider_does_not_self_update_but_announces_the_change()
    {
        IReadOnlyList<double>? changed = null;
        var cut = RenderComponent<BaseSlider>(p => p
            .Add(x => x.Value, [40])
            .Add(x => x.ValueChanged, v => changed = v)
            .AddChildContent(Parts(1)));

        await cut.InvokeAsync(() => cut.Instance.OnInput(0, 70));

        // Controlled: pinned to 40 until the parent flows a new Value back…
        Assert.Equal("40", cut.Find("[role=slider]").GetAttribute("aria-valuenow"));
        // …but the intended value was announced.
        Assert.NotNull(changed);
        Assert.Equal(70, Assert.Single(changed!));
    }
}
