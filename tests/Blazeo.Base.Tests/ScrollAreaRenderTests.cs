using Bunit;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render tests for the headless <see cref="BaseScrollArea"/>. The overlay-scrollbar behaviour lives
/// in ts/scrollArea.ts and is verified in-browser; here we cover the C# contract - the root hooks,
/// the Type -> data-type mapping, and that the supplied structure is rendered through. JSInterop is
/// Loose so the module import is a no-op.
/// </summary>
public class ScrollAreaRenderTests : TestContext
{
    public ScrollAreaRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Renders_root_hook_and_passes_through_content()
    {
        var cut = RenderComponent<BaseScrollArea>(p => p
            .AddChildContent("<div data-slot=\"scroll-area-viewport\">body</div>"));

        var root = cut.Find("[data-slot=scroll-area]");
        Assert.Equal("auto", root.GetAttribute("data-type")); // Auto is the default
        Assert.Contains("body", cut.Markup);
        Assert.NotNull(cut.Find("[data-slot=scroll-area-viewport]"));
    }

    [Theory]
    [InlineData(ScrollAreaType.Auto, "auto")]
    [InlineData(ScrollAreaType.Always, "always")]
    [InlineData(ScrollAreaType.Hover, "hover")]
    [InlineData(ScrollAreaType.Scroll, "scroll")]
    public void Type_maps_to_data_type(ScrollAreaType type, string expected)
    {
        var cut = RenderComponent<BaseScrollArea>(p => p.Add(x => x.Type, type));
        Assert.Equal(expected, cut.Find("[data-slot=scroll-area]").GetAttribute("data-type"));
    }

    [Fact]
    public void Forwards_class_and_extra_attributes()
    {
        var cut = RenderComponent<BaseScrollArea>(p => p
            .Add(x => x.Class, "h-72 w-48")
            .AddUnmatched("data-testid", "sa"));

        var root = cut.Find("[data-slot=scroll-area]");
        Assert.Contains("h-72", root.GetAttribute("class"));
        Assert.Equal("sa", root.GetAttribute("data-testid"));
    }
}
