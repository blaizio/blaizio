using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render tests for the headless carousel. The scrolling, selection and drag live in ts/carousel.ts and
/// are verified in-browser; here we cover the C# contract - the region/slide hooks, orientation, and the
/// state the engine pushes back through OnScrollState (selected index, edge flags, count, and the
/// selected-index callback). JSInterop is Loose so the viewport's module import is a no-op.
/// </summary>
public class CarouselRenderTests : TestContext
{
    public CarouselRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<BaseCarousel> RenderCarousel(
        Orientation orientation, int slides, EventCallback<int>? onSelected = null)
    {
        RenderFragment content = builder =>
        {
            builder.OpenComponent<BaseCarouselContent>(0);
            builder.AddAttribute(1, nameof(BaseCarouselContent.ChildContent), (RenderFragment)(cb =>
            {
                var seq = 0;
                for (var i = 0; i < slides; i++)
                {
                    cb.OpenComponent<BaseCarouselItem>(seq++);
                    cb.CloseComponent();
                }
            }));
            builder.CloseComponent();
        };

        return RenderComponent<BaseCarousel>(ps =>
        {
            ps.Add(x => x.Orientation, orientation).AddChildContent(content);
            if (onSelected is { } cb) ps.Add(x => x.OnSelectedIndexChanged, cb);
        });
    }

    [Fact]
    public void Renders_region_hooks()
    {
        var cut = RenderCarousel(Orientation.Horizontal, 3);
        var region = cut.Find("[data-slot=carousel]");
        Assert.Equal("region", region.GetAttribute("role"));
        Assert.Equal("carousel", region.GetAttribute("aria-roledescription"));
        Assert.Equal("horizontal", region.GetAttribute("data-orientation"));
    }

    [Fact]
    public void Vertical_sets_orientation_hook()
    {
        var cut = RenderCarousel(Orientation.Vertical, 3);
        Assert.Equal("vertical", cut.Find("[data-slot=carousel]").GetAttribute("data-orientation"));
        Assert.False(cut.Instance.IsHorizontal);
    }

    [Fact]
    public void Slides_announce_as_slides()
    {
        var cut = RenderCarousel(Orientation.Horizontal, 3);
        var slides = cut.FindAll("[data-slot=carousel-item]");
        Assert.Equal(3, slides.Count);
        Assert.All(slides, s =>
        {
            Assert.Equal("group", s.GetAttribute("role"));
            Assert.Equal("slide", s.GetAttribute("aria-roledescription"));
        });
    }

    [Fact]
    public async Task OnScrollState_updates_state()
    {
        var cut = RenderCarousel(Orientation.Horizontal, 5);
        await cut.InvokeAsync(() => cut.Instance.OnScrollState(2, canPrev: true, canNext: true, count: 5));

        Assert.Equal(2, cut.Instance.SelectedIndex);
        Assert.Equal(5, cut.Instance.SlideCount);
        Assert.True(cut.Instance.CanScrollPrev);
        Assert.True(cut.Instance.CanScrollNext);
    }

    [Fact]
    public async Task OnScrollState_raises_callback_only_when_index_changes()
    {
        var raised = new List<int>();
        var cb = EventCallback.Factory.Create<int>(this, i => raised.Add(i));
        var cut = RenderCarousel(Orientation.Horizontal, 5, cb);

        await cut.InvokeAsync(() => cut.Instance.OnScrollState(1, true, true, 5));
        await cut.InvokeAsync(() => cut.Instance.OnScrollState(1, false, true, 5)); // index unchanged
        await cut.InvokeAsync(() => cut.Instance.OnScrollState(2, true, true, 5));

        Assert.Equal(new[] { 1, 2 }, raised);
    }
}
