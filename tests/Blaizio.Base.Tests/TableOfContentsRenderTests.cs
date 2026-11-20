using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render tests for the headless table of contents. Heading discovery and the scrollspy are JS
/// (ts/toc.ts, verified in-browser); these cover the C# contract around them: the nav stays hidden
/// and empty until the module reports headings, one link per reported heading, what the scan and
/// the spy are started with, and the rescan when navigation lands on another page.
/// </summary>
public class TableOfContentsRenderTests : BunitContext
{
    private const string ModulePath = "./_content/blaizio.base/dist/toc.js";

    private static readonly TocHeading[] Headings =
    [
        new("intro", "Introduction", 2),
        new("install", "Installation", 2),
        new("from-the-cli", "From the CLI", 3),
    ];

    public TableOfContentsRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // The module with a scan that reports the given headings, and a recording spy.
    private (BunitJSModuleInterop Module, BunitJSModuleInterop Spy, JSRuntimeInvocationHandler<TocHeading[]?> Scan) SetupToc(
        TocHeading[]? headings)
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;
        var scan = module.Setup<TocHeading[]?>("scan", _ => true);
        scan.SetResult(headings);
        var spy = module.SetupModule(invocation => invocation.Identifier == "createSpy");
        spy.Mode = JSRuntimeMode.Loose;
        return (module, spy, scan);
    }

    [Fact]
    public void Renders_hidden_empty_nav_until_headings_arrive()
    {
        var cut = Render<BaseTableOfContents>();

        var nav = cut.Find("nav");
        Assert.NotNull(nav.GetAttribute("hidden"));
        Assert.Empty(cut.FindAll("a"));
    }

    [Fact]
    public void Forwards_class_and_attributes_to_the_nav()
    {
        var cut = Render<BaseTableOfContents>(p => p
            .Add(x => x.Class, "text-sm")
            .AddUnmatched("data-slot", "toc"));

        var nav = cut.Find("nav");
        Assert.Equal("text-sm", nav.GetAttribute("class"));
        Assert.Equal("toc", nav.GetAttribute("data-slot"));
    }

    [Fact]
    public void Reported_headings_become_links_in_page_order_and_the_nav_shows()
    {
        SetupToc(Headings);

        var cut = Render<BaseTableOfContents>();

        Assert.False(cut.Find("nav").HasAttribute("hidden"));
        var links = cut.FindAll("nav a");
        Assert.Equal(["Introduction", "Installation", "From the CLI"], links.Select(a => a.TextContent));
        // The pair the spy relies on: the fragment to scroll to and the id to mark active.
        Assert.Equal(["#intro", "#install", "#from-the-cli"], links.Select(a => a.GetAttribute("href")));
        Assert.Equal(["intro", "install", "from-the-cli"], links.Select(a => a.GetAttribute("data-toc-id")));
    }

    [Fact]
    public void The_header_renders_ahead_of_the_links_and_only_when_there_are_headings()
    {
        RenderFragment header = b => b.AddMarkupContent(0, "<p data-test-header>On this page</p>");
        var (_, _, scan) = SetupToc(Headings);

        var cut = Render<BaseTableOfContents>(p => p.Add(x => x.HeaderContent, header));
        Assert.Equal("p", cut.Find("nav").Children[0].LocalName);
        Assert.Equal("On this page", cut.Find("[data-test-header]").TextContent);

        // The next page has no headings: a title over nothing would be left behind.
        scan.SetResult([]);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/empty");
        cut.WaitForAssertion(() => Assert.True(cut.Find("nav").HasAttribute("hidden")));
        Assert.Empty(cut.FindAll("[data-test-header]"));
    }

    [Fact]
    public void An_item_template_renders_each_link_from_its_heading()
    {
        SetupToc(Headings);

        var cut = Render<BaseTableOfContents>(p => p.Add(x => x.ItemContent, heading => builder =>
        {
            builder.OpenElement(0, "a");
            builder.AddAttribute(1, "href", "#" + heading.Id);
            builder.AddAttribute(2, "data-toc-id", heading.Id);
            builder.AddAttribute(3, "data-level", heading.Level);
            builder.AddContent(4, heading.Text.ToUpperInvariant());
            builder.CloseElement();
        }));

        var links = cut.FindAll("nav a");
        Assert.Equal(["INTRODUCTION", "INSTALLATION", "FROM THE CLI"], links.Select(a => a.TextContent));
        Assert.Equal(["2", "2", "3"], links.Select(a => a.GetAttribute("data-level")));
    }

    [Fact]
    public void The_scan_looks_in_main_for_second_and_third_level_headings_by_default()
    {
        var (module, _, _) = SetupToc(Headings);

        Render<BaseTableOfContents>();

        var scan = Assert.Single(module.Invocations, i => i.Identifier == "scan");
        Assert.Equal(["main", "h2, h3"], scan.Arguments);
    }

    [Fact]
    public void The_spy_attaches_to_the_nav_once_the_links_exist_with_every_setting()
    {
        var (module, _, _) = SetupToc(Headings);

        Render<BaseTableOfContents>(p => p
            .Add(x => x.ContentSelector, "article")
            .Add(x => x.HeadingSelector, "h2")
            .Add(x => x.TopOffset, 64)
            .Add(x => x.RootSelector, "#scroller")
            .Add(x => x.UpdateHash, false));

        Assert.Equal(["article", "h2"], module.Invocations.Single(i => i.Identifier == "scan").Arguments);
        // createSpy(nav, contentSelector, headingSelector, topOffset, rootSelector, updateHash)
        var create = Assert.Single(module.Invocations, i => i.Identifier == "createSpy");
        Assert.IsType<ElementReference>(create.Arguments[0]);
        Assert.Equal(["article", "h2", 64, "#scroller", false], create.Arguments.Skip(1));
    }

    [Fact]
    public void By_default_the_spy_tracks_the_window_and_writes_the_section_to_the_address()
    {
        var (module, _, _) = SetupToc(Headings);

        Render<BaseTableOfContents>();

        var create = Assert.Single(module.Invocations, i => i.Identifier == "createSpy");
        Assert.Equal(["main", "h2, h3", 0, null, true], create.Arguments.Skip(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_page_with_no_headings_gets_no_spy(bool scanAnswersNothing)
    {
        var (module, _, _) = SetupToc(scanAnswersNothing ? null : []);

        var cut = Render<BaseTableOfContents>();

        Assert.Single(module.Invocations, i => i.Identifier == "scan");
        Assert.DoesNotContain(module.Invocations, i => i.Identifier == "createSpy");
        Assert.True(cut.Find("nav").HasAttribute("hidden"));
    }

    [Fact]
    public void Navigating_to_another_page_rescans_and_replaces_the_spy()
    {
        var (module, spy, scan) = SetupToc(Headings);
        var cut = Render<BaseTableOfContents>();
        Assert.Equal(3, cut.FindAll("nav a").Count);

        scan.SetResult([new TocHeading("usage", "Usage", 2)]);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/docs/other");

        cut.WaitForAssertion(() => Assert.Equal(["Usage"], cut.FindAll("nav a").Select(a => a.TextContent)));
        Assert.Equal(2, module.Invocations.Count(i => i.Identifier == "scan"));
        // The old spy watched headings that are gone: it is released before the new one attaches.
        cut.WaitForAssertion(() => Assert.Equal(2, module.Invocations.Count(i => i.Identifier == "createSpy")));
        Assert.Single(spy.Invocations, i => i.Identifier == "dispose");
    }

    [Fact]
    public void Navigating_to_a_page_without_headings_hides_the_nav_and_releases_the_spy()
    {
        var (module, spy, scan) = SetupToc(Headings);
        var cut = Render<BaseTableOfContents>();

        scan.SetResult([]);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/docs/plain");

        cut.WaitForAssertion(() => Assert.True(cut.Find("nav").HasAttribute("hidden")));
        Assert.Empty(cut.FindAll("nav a"));
        cut.WaitForAssertion(() => Assert.Single(spy.Invocations, i => i.Identifier == "dispose"));
        Assert.Single(module.Invocations, i => i.Identifier == "createSpy");
    }

    [Fact]
    public async Task Leaving_the_page_releases_the_spy_and_stops_following_navigation()
    {
        var (module, spy, _) = SetupToc(Headings);
        Render<BaseTableOfContents>();

        await DisposeComponentsAsync();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/elsewhere");

        Assert.Single(spy.Invocations, i => i.Identifier == "dispose");
        Assert.Single(module.Invocations, i => i.Identifier == "scan");
    }
}
