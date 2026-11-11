using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The image root's load status across renderers. Interactive, it starts at Loading and waits
/// for the load to be reported. On a page that is rendered statically and stays that way nothing
/// can report it, so the image counts as loaded from the start: held at Loading, the styled
/// layer kept it transparent for good.
/// </summary>
public class ImageRenderTests : BunitContext
{
    public ImageRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Img => builder =>
    {
        builder.OpenComponent<BaseImageImg>(0);
        builder.AddAttribute(1, nameof(BaseImageImg.Src), "/hero.jpg");
        builder.AddAttribute(2, nameof(BaseImageImg.Alt), "Hero");
        builder.CloseComponent();
    };

    private static string? State(IRenderedComponent<BaseImage> cut, string slot) =>
        cut.Find($"[data-slot={slot}]").GetAttribute("data-state");

    [Fact]
    public void Without_renderer_info_the_image_waits_for_its_load()
    {
        // A test host supplies no renderer info: treated as interactive, as the avatar does.
        var cut = Render<BaseImage>(p => p.AddChildContent(Img));

        Assert.Equal("loading", State(cut, "image"));
        Assert.Equal("loading", State(cut, "image-img"));
    }

    [Fact]
    public void On_a_statically_rendered_page_the_image_counts_as_loaded()
    {
        SetRendererInfo(new RendererInfo("Static", false));

        var cut = Render<BaseImage>(p => p.AddChildContent(Img));

        Assert.Equal("loaded", State(cut, "image"));
        Assert.Equal("loaded", State(cut, "image-img"));
    }

    [Fact]
    public void While_prerendering_an_interactive_component_the_image_still_waits()
    {
        // Not interactive yet, but it will be: the load is reported once it is.
        SetRendererInfo(new RendererInfo("Static", false));

        var cut = Render<BaseImage>(p => p
            .SetAssignedRenderMode(RenderMode.InteractiveServer)
            .AddChildContent(Img));

        Assert.Equal("loading", State(cut, "image"));
    }
}
