using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render tests for the headless 2D color surface. The pointer/keyboard geometry lives in
/// ts/colorArea.ts and is verified in-browser; here we cover the C# contract - the thumb's 2D-slider
/// ARIA (name, per-axis valuetext, value publication) and the disabled state. JSInterop is Loose so
/// the module import is a no-op.
/// </summary>
public class ColorAreaRenderTests : BunitContext
{
    public ColorAreaRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<BaseColorArea> RenderArea(
        ColorAreaPoint? value = null, bool disabled = false, string? xLabel = null, string? yLabel = null)
    {
        RenderFragment thumb = builder =>
        {
            builder.OpenComponent<BaseColorAreaThumb>(0);
            if (xLabel is not null) builder.AddComponentParameter(1, nameof(BaseColorAreaThumb.XAxisLabel), xLabel);
            if (yLabel is not null) builder.AddComponentParameter(2, nameof(BaseColorAreaThumb.YAxisLabel), yLabel);
            builder.CloseComponent();
        };

        return Render<BaseColorArea>(ps =>
        {
            ps.Add(x => x.DefaultValue, value ?? default).AddChildContent(thumb);
            if (disabled) ps.Add(x => x.Disabled, true);
        });
    }

    [Fact]
    public void Thumb_is_a_named_slider_with_both_axes_in_the_valuetext()
    {
        var cut = RenderArea(new ColorAreaPoint(0.25, 0.5));

        var thumb = cut.Find("[data-bz-color-area-thumb]");
        Assert.Equal("slider", thumb.GetAttribute("role"));
        Assert.Equal("Color", thumb.GetAttribute("aria-label"));
        Assert.Equal("50", thumb.GetAttribute("aria-valuenow"));
        Assert.Equal("Saturation 25%, Brightness 50%", thumb.GetAttribute("aria-valuetext"));
        Assert.Equal("0", thumb.GetAttribute("tabindex"));
    }

    [Fact]
    public void Axis_labels_are_customizable()
    {
        var cut = RenderArea(new ColorAreaPoint(0.1, 0.9), xLabel: "X", yLabel: "Y");

        Assert.Equal("X 10%, Y 90%", cut.Find("[data-bz-color-area-thumb]").GetAttribute("aria-valuetext"));
    }

    [Fact]
    public void Disabled_surface_removes_the_tab_stop()
    {
        var cut = RenderArea(disabled: true);

        var thumb = cut.Find("[data-bz-color-area-thumb]");
        Assert.False(thumb.HasAttribute("tabindex"));
        Assert.Equal("true", thumb.GetAttribute("aria-disabled"));
    }
}
