using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The avatar image picks its load handling from the renderer (Blazor events when interactive, the
/// element's own handler on a static page). A test host supplies no renderer info at all; reading
/// it must not take the component down.
/// </summary>
public class AvatarRenderTests : BunitContext
{
    private IRenderedComponent<BaseAvatar> RenderAvatar() =>
        Render<BaseAvatar>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<BaseAvatarImage>(0);
            builder.AddAttribute(1, nameof(BaseAvatarImage.Src), "/me.png");
            builder.AddAttribute(2, nameof(BaseAvatarImage.Alt), "Me");
            builder.CloseComponent();
            builder.OpenComponent<BaseAvatarFallback>(3);
            builder.AddAttribute(4, nameof(BaseAvatarFallback.ChildContent), (RenderFragment)(b => b.AddContent(0, "ME")));
            builder.CloseComponent();
        }));

    [Fact]
    public void Renders_without_renderer_info_and_shows_the_fallback_until_the_image_loads()
    {
        var cut = RenderAvatar();

        Assert.Contains("display:none", cut.Find("[data-slot=avatar-image]").GetAttribute("style"));
        Assert.Equal("ME", cut.Find("[data-slot=avatar-fallback]").TextContent);
    }

    [Fact]
    public void A_loaded_image_replaces_the_fallback()
    {
        var cut = RenderAvatar();

        cut.Find("[data-slot=avatar-image]").TriggerEvent("onload", EventArgs.Empty);

        Assert.DoesNotContain("display:none", cut.Find("[data-slot=avatar-image]").GetAttribute("style") ?? "");
        Assert.Empty(cut.FindAll("[data-slot=avatar-fallback]"));
    }
}
