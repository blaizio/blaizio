using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The carousel's side of its scroll engine: what the engine is started with, the methods that
/// drive it, and how a bound slide index and the viewport are kept in step (the carousel scrolls to
/// a new index, and takes the engine's reports as the selection except while it is on its way
/// there). The engine itself is ts/carousel.ts, a recording stand-in here.
/// </summary>
public class CarouselEngineTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _engine;

    public CarouselEngineTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./_content/blaizio.base/dist/carousel.js");
        _module.Mode = JSRuntimeMode.Loose;
        _engine = _module.SetupModule(invocation => invocation.Identifier == "createCarousel");
        _engine.Mode = JSRuntimeMode.Loose;
    }

    private static readonly RenderFragment FiveSlides = builder =>
    {
        builder.OpenComponent<BaseCarouselContent>(0);
        builder.AddComponentParameter(1, nameof(BaseCarouselContent.ChildContent), (RenderFragment)(content =>
        {
            for (var i = 0; i < 5; i++)
            {
                content.OpenComponent<BaseCarouselItem>(0);
                content.CloseComponent();
            }
        }));
        builder.CloseComponent();
    };

    // A page that binds the slide index both ways, as @bind-SelectedIndex does. Follow off is a
    // parent that listens and keeps its own value.
    private sealed class Deck : ComponentBase
    {
        [Parameter] public int Start { get; set; }
        [Parameter] public bool Follow { get; set; } = true;

        public int Index { get; set; }
        public List<int> Changes { get; } = [];
        public BaseCarousel Carousel { get; private set; } = default!;

        protected override void OnInitialized() => Index = Start;

        public void Show(int index)
        {
            Index = index;
            StateHasChanged();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<BaseCarousel>(0);
            builder.AddComponentParameter(1, nameof(BaseCarousel.AriaLabel), "Photos");
            builder.AddComponentParameter(2, nameof(BaseCarousel.SelectedIndex), (int?)Index);
            builder.AddComponentParameter(3, nameof(BaseCarousel.SelectedIndexChanged),
                EventCallback.Factory.Create<int>(this, index =>
                {
                    Changes.Add(index);
                    if (Follow) Index = index;
                }));
            builder.AddComponentParameter(4, nameof(BaseCarousel.ChildContent), FiveSlides);
            builder.AddComponentReferenceCapture(5, carousel => Carousel = (BaseCarousel)carousel);
            builder.CloseComponent();
        }
    }

    private IEnumerable<int> ScrollsAskedFor =>
        _engine.Invocations.Where(i => i.Identifier == "scrollTo").Select(i => (int)i.Arguments[0]!);

    // What the engine reports as the viewport moves: the slide in view and how many there are.
    private static Task Report(IRenderedComponent<Deck> cut, int index, bool settled = false) =>
        cut.InvokeAsync(() => cut.Instance.Carousel.OnScrollState(index, index > 0, index < 4, 5, settled));

    [Fact]
    public void The_engine_starts_once_on_the_viewport_with_the_carousels_settings()
    {
        var cut = Render<BaseCarousel>(p => p
            .Add(x => x.AriaLabel, "Photos")
            .Add(x => x.Orientation, Orientation.Vertical)
            .Add(x => x.Loop, true)
            .Add(x => x.AutoplayInterval, 4000)
            .AddChildContent(FiveSlides));
        cut.Render();

        // createCarousel(viewport, reference, options, root)
        var create = Assert.Single(_module.Invocations, i => i.Identifier == "createCarousel");
        Assert.IsType<ElementReference>(create.Arguments[0]);
        Assert.Same(cut.Instance, Assert.IsType<Microsoft.JSInterop.DotNetObjectReference<BaseCarousel>>(create.Arguments[1]).Value);
        Assert.Equal("""{"horizontal":false,"loop":true,"autoplayMs":4000}""", JsonSerializer.Serialize(create.Arguments[2]));
        Assert.IsType<ElementReference>(create.Arguments[3]);
    }

    [Fact]
    public void By_default_the_engine_scrolls_sideways_without_wrapping_or_rotating()
    {
        Render<BaseCarousel>(p => p.Add(x => x.AriaLabel, "Photos").AddChildContent(FiveSlides));

        var create = Assert.Single(_module.Invocations, i => i.Identifier == "createCarousel");
        Assert.Equal("""{"horizontal":true,"loop":false,"autoplayMs":0}""", JsonSerializer.Serialize(create.Arguments[2]));
    }

    [Fact]
    public async Task Each_method_drives_the_engine()
    {
        var cut = Render<BaseCarousel>(p => p.Add(x => x.AriaLabel, "Photos").AddChildContent(FiveSlides));

        await cut.InvokeAsync(async () =>
        {
            await cut.Instance.ScrollPrev();
            await cut.Instance.ScrollNext();
            await cut.Instance.ScrollTo(3);
            await cut.Instance.PauseAsync();
            await cut.Instance.ResumeAsync();
        });

        Assert.Equal(["scrollPrev", "scrollNext", "scrollTo", "pause", "play"], _engine.Invocations.Select(i => i.Identifier));
        Assert.Equal([3], ScrollsAskedFor);
    }

    [Fact]
    public async Task Without_a_viewport_there_is_no_engine_and_the_methods_do_nothing()
    {
        // No content part: nothing to scroll yet.
        var cut = Render<BaseCarousel>(p => p.Add(x => x.AriaLabel, "Photos"));

        await cut.InvokeAsync(async () =>
        {
            await cut.Instance.ScrollNext();
            await cut.Instance.ScrollTo(2);
            await cut.Instance.PauseAsync();
        });

        Assert.DoesNotContain(_module.Invocations, i => i.Identifier == "createCarousel");
        Assert.Empty(_engine.Invocations);
    }

    [Fact]
    public async Task A_bound_index_scrolls_the_viewport_there_and_the_way_there_is_not_a_selection()
    {
        var cut = Render<Deck>(p => p.Add(x => x.Start, 3));
        // Before the engine has measured the slides there is nowhere to scroll to.
        Assert.Empty(ScrollsAskedFor);

        await Report(cut, 0); // the engine's first report: at the first slide, five slides
        Assert.Equal([3], ScrollsAskedFor);

        await Report(cut, 1); // passing slides on the way
        await Report(cut, 2);
        await Report(cut, 3); // arrived
        Assert.Empty(cut.Instance.Changes);
        Assert.Equal([3], ScrollsAskedFor);
        Assert.Equal(3, cut.Instance.Carousel.CurrentIndex);
    }

    [Fact]
    public async Task After_arriving_a_scroll_by_the_reader_becomes_the_selection()
    {
        var cut = Render<Deck>(p => p.Add(x => x.Start, 1));
        await Report(cut, 0);
        await Report(cut, 1);

        await Report(cut, 2); // swiped on

        Assert.Equal([2], cut.Instance.Changes);
        Assert.Equal(2, cut.Instance.Index);
        // The page follows, so index and viewport agree: nothing is scrolled back.
        Assert.Equal([1], ScrollsAskedFor);
    }

    [Fact]
    public async Task A_new_bound_index_scrolls_the_viewport_to_it()
    {
        var cut = Render<Deck>();
        await Report(cut, 0);
        Assert.Empty(ScrollsAskedFor);

        await cut.InvokeAsync(() => cut.Instance.Show(4));

        Assert.Equal([4], ScrollsAskedFor);
        // Rendered again while still on the way: the same scroll is not asked for twice.
        cut.Render();
        Assert.Equal([4], ScrollsAskedFor);
    }

    [Fact]
    public async Task A_scroll_that_was_interrupted_takes_where_the_viewport_came_to_rest()
    {
        var cut = Render<Deck>(p => p.Add(x => x.Start, 3));
        await Report(cut, 0);
        Assert.Equal([3], ScrollsAskedFor);

        // The reader grabbed the slides on the way: the viewport settles on the second one.
        await Report(cut, 1, settled: true);

        Assert.Equal([1], cut.Instance.Changes);
        Assert.Equal([3], ScrollsAskedFor); // not dragged back to where it was heading

        // Later scrolls are selections again. They used to be swallowed for good.
        await Report(cut, 2);
        Assert.Equal([1, 2], cut.Instance.Changes);
    }

    [Fact]
    public async Task A_page_that_keeps_its_own_index_gets_the_viewport_scrolled_back()
    {
        var cut = Render<Deck>(p => p.Add(x => x.Follow, false));
        await Report(cut, 0);

        await Report(cut, 2); // swiped to the third slide; the page is asked and does not follow

        Assert.Equal([2], cut.Instance.Changes);
        Assert.Equal(0, cut.Instance.Carousel.CurrentIndex);
        Assert.Equal([0], ScrollsAskedFor);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(-1)]
    public async Task An_index_outside_the_slides_is_not_scrolled_to(int index)
    {
        var cut = Render<Deck>(p => p.Add(x => x.Start, index));

        await Report(cut, 0);

        Assert.Empty(ScrollsAskedFor);
    }

    [Fact]
    public async Task Leaving_the_page_releases_the_engine()
    {
        Render<BaseCarousel>(p => p.Add(x => x.AriaLabel, "Photos").AddChildContent(FiveSlides));

        await DisposeComponentsAsync();

        Assert.Single(_engine.Invocations, i => i.Identifier == "dispose");
    }
}
