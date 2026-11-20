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

/// <summary>
/// The image family's load lifecycle on an interactive page: the img's own attributes, the
/// placeholder while it loads, the fallback when it fails, a retry, a source that changes, the
/// probe that catches a load the events missed, and the responsive candidates.
/// </summary>
public class ImageLifecycleTests : BunitContext
{
    public ImageLifecycleTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static void AddImg(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, string? src)
    {
        builder.OpenComponent<BaseImageImg>(0);
        builder.AddAttribute(1, nameof(BaseImageImg.Src), src);
        builder.AddAttribute(2, nameof(BaseImageImg.Alt), "Hero");
        builder.CloseComponent();
    }

    private static void AddSlots(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.OpenComponent<BaseImagePlaceholder>(10);
        builder.AddAttribute(11, nameof(BaseImagePlaceholder.ChildContent), (RenderFragment)(b => b.AddContent(0, "loading")));
        builder.CloseComponent();
        builder.OpenComponent<BaseImageFallback>(20);
        builder.AddAttribute(21, nameof(BaseImageFallback.ChildContent), (RenderFragment)(b => b.AddContent(0, "failed")));
        builder.CloseComponent();
    }

    // The whole family: img, placeholder and fallback, reporting every status change.
    private IRenderedComponent<BaseImage> RenderImage(List<ImageStatus>? statuses = null, string? src = "/hero.jpg") =>
        Render<BaseImage>(p =>
        {
            if (statuses is not null) p.Add(x => x.StatusChanged, statuses.Add);
            p.AddChildContent(builder =>
            {
                AddImg(builder, src);
                AddSlots(builder);
            });
        });

    private static string? RootState(IRenderedComponent<BaseImage> cut) =>
        cut.Find("[data-slot=image]").GetAttribute("data-state");

    [Fact]
    public void While_loading_the_placeholder_shows_and_the_fallback_does_not()
    {
        var cut = RenderImage();

        Assert.Equal("loading", RootState(cut));
        Assert.Equal("loading", cut.Find("[data-slot=image-placeholder]").TextContent);
        Assert.Empty(cut.FindAll("[data-slot=image-fallback]"));
        // In the document while it loads: it is revealed by state, not by being added later.
        Assert.Equal("/hero.jpg", cut.Find("img").GetAttribute("src"));
    }

    [Fact]
    public void A_load_marks_the_family_loaded_drops_the_placeholder_and_is_reported_once()
    {
        var statuses = new List<ImageStatus>();
        var cut = RenderImage(statuses);

        cut.Find("img").TriggerEvent("onload", EventArgs.Empty);
        cut.Find("img").TriggerEvent("onload", EventArgs.Empty); // a second event for the same state

        Assert.Equal("loaded", RootState(cut));
        Assert.Equal("loaded", cut.Find("img").GetAttribute("data-state"));
        Assert.Empty(cut.FindAll("[data-slot=image-placeholder]"));
        Assert.Empty(cut.FindAll("[data-slot=image-fallback]"));
        Assert.Equal([ImageStatus.Loaded], statuses);
    }

    [Fact]
    public void A_failed_load_shows_the_fallback_in_place_of_the_placeholder()
    {
        var statuses = new List<ImageStatus>();
        var cut = RenderImage(statuses);

        cut.Find("img").TriggerEvent("onerror", EventArgs.Empty);

        Assert.Equal("error", RootState(cut));
        Assert.Equal("failed", cut.Find("[data-slot=image-fallback]").TextContent);
        Assert.Empty(cut.FindAll("[data-slot=image-placeholder]"));
        Assert.Equal([ImageStatus.Error], statuses);
    }

    [Fact]
    public void Retry_after_a_failure_goes_back_to_loading_with_the_same_source()
    {
        var statuses = new List<ImageStatus>();
        var cut = RenderImage(statuses);
        cut.Find("img").TriggerEvent("onerror", EventArgs.Empty);

        cut.InvokeAsync(() => cut.Instance.Retry());

        Assert.Equal("loading", RootState(cut));
        Assert.Empty(cut.FindAll("[data-slot=image-fallback]"));
        Assert.NotEmpty(cut.FindAll("[data-slot=image-placeholder]"));
        Assert.Equal("/hero.jpg", cut.Find("img").GetAttribute("src"));
        Assert.Equal([ImageStatus.Error, ImageStatus.Loading], statuses);

        // The second attempt can succeed like a first one.
        cut.Find("img").TriggerEvent("onload", EventArgs.Empty);
        Assert.Equal("loaded", RootState(cut));
        Assert.Equal([ImageStatus.Error, ImageStatus.Loading, ImageStatus.Loaded], statuses);
    }

    [Fact]
    public void Retry_does_nothing_unless_the_load_failed()
    {
        var statuses = new List<ImageStatus>();
        var cut = RenderImage(statuses);

        cut.InvokeAsync(() => cut.Instance.Retry()); // still loading
        cut.Find("img").TriggerEvent("onload", EventArgs.Empty);
        cut.InvokeAsync(() => cut.Instance.Retry()); // loaded

        Assert.Equal("loaded", RootState(cut));
        Assert.Equal([ImageStatus.Loaded], statuses);
    }

    [Fact]
    public void With_an_empty_source_there_is_no_img_and_the_image_is_idle_behind_its_placeholder()
    {
        var statuses = new List<ImageStatus>();
        var cut = RenderImage(statuses, src: "");

        Assert.Empty(cut.FindAll("img"));
        Assert.Equal("idle", RootState(cut));
        Assert.NotEmpty(cut.FindAll("[data-slot=image-placeholder]"));
        Assert.Empty(cut.FindAll("[data-slot=image-fallback]"));
        Assert.Equal([ImageStatus.Idle], statuses);
    }

    [Fact]
    public void With_no_source_at_all_the_image_is_idle_too()
    {
        var statuses = new List<ImageStatus>();
        var cut = RenderImage(statuses, src: null);

        Assert.Empty(cut.FindAll("img"));
        Assert.Equal("idle", RootState(cut));
        Assert.Equal([ImageStatus.Idle], statuses);
    }

    [Fact]
    public void A_new_source_starts_loading_again_and_a_removed_one_goes_idle()
    {
        var statuses = new List<ImageStatus>();
        string? src = "/one.jpg";
        var cut = Render<BaseImage>(p => p
            .Add(x => x.StatusChanged, statuses.Add)
            .AddChildContent(builder =>
            {
                AddImg(builder, src);
                AddSlots(builder);
            }));
        cut.Find("img").TriggerEvent("onload", EventArgs.Empty);
        Assert.Equal("loaded", RootState(cut));

        src = "/two.jpg";
        cut.Render();
        Assert.Equal("/two.jpg", cut.Find("img").GetAttribute("src"));
        Assert.Equal("loading", RootState(cut));
        Assert.NotEmpty(cut.FindAll("[data-slot=image-placeholder]"));

        // The same source again is not a new load.
        cut.Find("img").TriggerEvent("onload", EventArgs.Empty);
        cut.Render();
        Assert.Equal("loaded", RootState(cut));

        src = "";
        cut.Render();
        Assert.Empty(cut.FindAll("img"));
        Assert.Equal("idle", RootState(cut));
        Assert.Equal([ImageStatus.Loaded, ImageStatus.Loading, ImageStatus.Loaded, ImageStatus.Idle], statuses);
    }

    [Theory]
    [InlineData("loaded", "loaded")]
    [InlineData("error", "error")]
    [InlineData("pending", "loading")]
    public void An_image_that_settled_before_the_events_were_wired_is_caught_by_the_probe(string probed, string expected)
    {
        // A cached image can finish before Blazor attaches its handlers: the event never comes.
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/image.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<string>("status", _ => true).SetResult(probed);

        var cut = RenderImage();

        cut.WaitForAssertion(() => Assert.Equal(expected, RootState(cut)));
        Assert.Equal(expected == "error", cut.FindAll("[data-slot=image-fallback]").Count == 1);
    }

    [Fact]
    public void A_probe_that_fails_leaves_the_image_waiting_for_its_events()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/image.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<string>("status", _ => true).SetException(new Microsoft.JSInterop.JSException("no element"));

        var cut = RenderImage();

        Assert.Equal("loading", RootState(cut));
        cut.Find("img").TriggerEvent("onload", EventArgs.Empty);
        Assert.Equal("loaded", RootState(cut));
    }

    [Fact]
    public void The_probe_runs_again_for_a_new_source_but_not_on_other_renders()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/image.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<string>("status", _ => true).SetResult("pending");
        string src = "/one.jpg";
        var cut = Render<BaseImage>(p => p.AddChildContent(builder => AddImg(builder, src)));
        Assert.Single(module.Invocations, i => i.Identifier == "status");

        cut.Render();
        Assert.Single(module.Invocations, i => i.Identifier == "status");

        src = "/two.jpg";
        cut.Render();
        Assert.Equal(2, module.Invocations.Count(i => i.Identifier == "status"));
    }

    [Fact]
    public void The_img_carries_alt_candidates_and_the_native_hints_that_were_asked_for()
    {
        var cut = Render<BaseImage>(p => p.AddChildContent<BaseImageImg>(img => img
            .Add(x => x.Src, "/hero.jpg")
            .Add(x => x.Alt, "A harbour at dusk")
            .Add(x => x.SrcSet, "/hero.jpg 1x, /hero@2x.jpg 2x")
            .Add(x => x.Sizes, "(min-width: 768px) 50vw, 100vw")
            .Add(x => x.Loading, ImageLoading.Lazy)
            .Add(x => x.FetchPriority, ImageFetchPriority.Low)
            .Add(x => x.Class, "rounded")
            .Add(x => x.Style, "object-fit:cover")
            .AddUnmatched("width", "640")));

        var img = cut.Find("img[data-slot=image-img]");
        Assert.Equal("A harbour at dusk", img.GetAttribute("alt"));
        Assert.Equal("/hero.jpg 1x, /hero@2x.jpg 2x", img.GetAttribute("srcset"));
        Assert.Equal("(min-width: 768px) 50vw, 100vw", img.GetAttribute("sizes"));
        Assert.Equal("lazy", img.GetAttribute("loading"));
        Assert.Equal("low", img.GetAttribute("fetchpriority"));
        Assert.Equal("async", img.GetAttribute("decoding"));
        Assert.Equal("rounded", img.GetAttribute("class"));
        Assert.Equal("object-fit:cover", img.GetAttribute("style"));
        Assert.Equal("640", img.GetAttribute("width"));
    }

    [Fact]
    public void Browser_defaults_are_left_alone_unless_asked_otherwise()
    {
        var cut = Render<BaseImage>(p => p.AddChildContent<BaseImageImg>(img => img
            .Add(x => x.Src, "/hero.jpg")
            .Add(x => x.Alt, "")));

        var img = cut.Find("img");
        Assert.False(img.HasAttribute("loading"));
        Assert.False(img.HasAttribute("fetchpriority"));
        Assert.False(img.HasAttribute("srcset"));
        // An empty alt is kept: it is how a decorative image is marked.
        Assert.True(img.HasAttribute("alt"));
        Assert.Equal("", img.GetAttribute("alt"));
    }

    [Fact]
    public void A_high_priority_image_says_so()
    {
        var cut = Render<BaseImage>(p => p.AddChildContent<BaseImageImg>(img => img
            .Add(x => x.Src, "/hero.jpg")
            .Add(x => x.FetchPriority, ImageFetchPriority.High)));

        Assert.Equal("high", cut.Find("img").GetAttribute("fetchpriority"));
    }

    [Fact]
    public void The_root_takes_its_tag_class_and_attributes()
    {
        var cut = Render<BaseImage>(p => p
            .Add(x => x.Element, "figure")
            .Add(x => x.Class, "overflow-hidden")
            .AddUnmatched("id", "cover"));

        var root = cut.Find("figure[data-slot=image]");
        Assert.Equal("overflow-hidden", root.GetAttribute("class"));
        Assert.Equal("cover", root.GetAttribute("id"));
    }

    [Fact]
    public void Candidates_render_as_sources_ahead_of_the_img_inside_a_picture()
    {
        var cut = Render<BaseImage>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<BaseImageSource>(0);
            builder.AddAttribute(1, nameof(BaseImageSource.SrcSet), "/hero.avif");
            builder.AddAttribute(2, nameof(BaseImageSource.Type), "image/avif");
            builder.CloseComponent();
            builder.OpenComponent<BaseImageSource>(3);
            builder.AddAttribute(4, nameof(BaseImageSource.SrcSet), "/hero-wide.jpg");
            builder.AddAttribute(5, nameof(BaseImageSource.Media), "(min-width: 768px)");
            builder.AddAttribute(6, nameof(BaseImageSource.Sizes), "50vw");
            builder.CloseComponent();
            builder.OpenRegion(7);
            AddImg(builder, "/hero.jpg");
            builder.CloseRegion();
        }));

        var picture = cut.Find("picture[data-slot=image-picture]");
        var children = picture.Children.Select(c => c.LocalName).ToArray();
        Assert.Equal(["source", "source", "img"], children);
        var sources = picture.QuerySelectorAll("source");
        Assert.Equal("/hero.avif", sources[0].GetAttribute("srcset"));
        Assert.Equal("image/avif", sources[0].GetAttribute("type"));
        Assert.Equal("/hero-wide.jpg", sources[1].GetAttribute("srcset"));
        Assert.Equal("(min-width: 768px)", sources[1].GetAttribute("media"));
        Assert.Equal("50vw", sources[1].GetAttribute("sizes"));
        Assert.Equal("/hero.jpg", picture.QuerySelector("img")!.GetAttribute("src"));
    }

    [Fact]
    public void A_candidate_that_goes_away_takes_its_source_and_the_last_one_takes_the_picture()
    {
        var wide = true;
        var cut = Render<BaseImage>(p => p.AddChildContent(builder =>
        {
            if (wide)
            {
                builder.OpenComponent<BaseImageSource>(0);
                builder.AddAttribute(1, nameof(BaseImageSource.SrcSet), "/hero-wide.jpg");
                builder.CloseComponent();
            }
            builder.OpenRegion(2);
            AddImg(builder, "/hero.jpg");
            builder.CloseRegion();
        }));
        Assert.Single(cut.FindAll("picture source"));

        wide = false;
        cut.Render();

        Assert.Empty(cut.FindAll("picture"));
        Assert.Equal("/hero.jpg", cut.Find("img").GetAttribute("src"));
    }

    [Fact]
    public void Outside_an_image_a_placeholder_renders_and_a_fallback_does_not()
    {
        var placeholder = Render<BaseImagePlaceholder>(p => p
            .Add(x => x.Class, "animate-pulse")
            .AddChildContent("loading"));
        var fallback = Render<BaseImageFallback>(p => p.AddChildContent("failed"));

        Assert.Equal("animate-pulse", placeholder.Find("[data-slot=image-placeholder]").GetAttribute("class"));
        Assert.Empty(fallback.FindAll("[data-slot=image-fallback]"));
    }

    [Fact]
    public void Parts_removed_from_the_page_stop_listening_to_the_image()
    {
        var showSlots = true;
        var cut = Render<BaseImage>(p => p.AddChildContent(builder =>
        {
            AddImg(builder, "/hero.jpg");
            if (showSlots) AddSlots(builder);
        }));
        Assert.Equal(3, Listeners(cut.Instance)); // the img, the placeholder, the fallback

        showSlots = false;
        cut.Render();

        // A part that stayed subscribed would be kept alive by the image for as long as it lives.
        Assert.Equal(1, Listeners(cut.Instance));
        cut.Find("img").TriggerEvent("onload", EventArgs.Empty);
        Assert.Equal("loaded", RootState(cut));
    }

    // How many parts follow the root's status: the subscribers of its internal change event.
    private static int Listeners(BaseImage root) =>
        ((Delegate?)typeof(BaseImage)
            .GetField("Changed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(root))?.GetInvocationList().Length ?? 0;
}
