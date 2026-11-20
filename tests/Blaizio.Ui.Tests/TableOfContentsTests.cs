using System.Globalization;
using AngleSharp.Dom;
using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// BzTableOfContents: the five looks over the same headings, floating and sticky placement, the
/// customization points, and what is handed to the script that finds and follows the headings.
/// </summary>
public class TableOfContentsTests : BunitContext
{
    private const string ModulePath = "./_content/blaizio.base/dist/toc.js";

    private static readonly TocHeading[] Headings =
    [
        new("intro", "Introduction", 2),
        new("install", "Installation", 2),
        new("from-the-cli", "From the CLI", 3),
        new("flags", "Flags", 4),
    ];

    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _spy;

    public TableOfContentsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
        _module = JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
        _module.Setup<TocHeading[]?>("scan", _ => true).SetResult(Headings);
        _spy = _module.SetupModule(invocation => invocation.Identifier == "createSpy");
        _spy.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<BzTableOfContents> Toc(Action<ComponentParameterCollectionBuilder<BzTableOfContents>>? more = null) =>
        Render<BzTableOfContents>(p => more?.Invoke(p));

    private static IElement Link(IRenderedComponent<BzTableOfContents> cut, string id) => cut.Find($"a[data-toc-id={id}]");

    // ---- the list -----------------------------------------------------------------------------

    [Fact]
    public void The_default_look_is_a_titled_list_with_one_link_a_heading()
    {
        var cut = Toc();

        var nav = cut.Find("nav[data-slot=toc]");
        Assert.Equal("list", nav.GetAttribute("data-variant"));
        Assert.Equal("inline", nav.GetAttribute("data-position"));
        Assert.False(nav.HasAttribute("hidden"));
        Assert.False(nav.HasAttribute("style"));
        Assert.Equal("On This Page", cut.Find("[data-slot=toc-title]").TextContent);

        var links = cut.FindAll("a[data-slot=toc-link]");
        Assert.Equal(["Introduction", "Installation", "From the CLI", "Flags"], links.Select(l => l.TextContent));
        Assert.Equal(["#intro", "#install", "#from-the-cli", "#flags"], links.Select(l => l.GetAttribute("href")));
        // What the scrollspy finds its links by.
        Assert.Equal(["intro", "install", "from-the-cli", "flags"], links.Select(l => l.GetAttribute("data-toc-id")));
        // No rail, no marks: this look is the links alone.
        Assert.Empty(cut.FindAll("[data-slot=toc-track], [data-slot=toc-indicator], [data-slot=toc-mark]"));
    }

    [Fact]
    public void Deeper_headings_are_indented_further()
    {
        var cut = Toc();

        Assert.DoesNotContain(Link(cut, "intro").ClassList, c => c.StartsWith("ps-"));
        Assert.Contains("ps-4", Link(cut, "from-the-cli").ClassList);
        Assert.Contains("ps-8", Link(cut, "flags").ClassList);
    }

    [Fact]
    public void A_page_with_no_headings_hides_the_panel()
    {
        _module.Setup<TocHeading[]?>("scan", _ => true).SetResult([]);

        var cut = Toc();

        var nav = cut.Find("nav[data-slot=toc]");
        Assert.True(nav.HasAttribute("hidden"));
        Assert.Equal(0, nav.ChildElementCount);
        Assert.Empty(_module.Invocations["createSpy"]);
    }

    [Fact]
    public void The_title_can_be_removed_replaced_or_restyled()
    {
        Assert.Empty(Toc(p => p.Add(x => x.Title, null)).FindAll("[data-slot=toc-title]"));

        var replaced = Toc(p => p.Add(x => x.TitleContent, "<h2 id=\"own\">Contents</h2>"));
        Assert.Equal("Contents", replaced.Find("#own").TextContent);
        Assert.Empty(replaced.FindAll("[data-slot=toc-title]"));

        var styled = Toc(p => p.Add(x => x.Title, "Contents").Add(x => x.TitleClass, "mb-4 uppercase")).Find("[data-slot=toc-title]");
        Assert.Equal("Contents", styled.TextContent);
        Assert.Contains("uppercase", styled.ClassList);
        Assert.Contains("mb-4", styled.ClassList);
        Assert.DoesNotContain("mb-2", styled.ClassList);
    }

    // ---- rail and progress --------------------------------------------------------------------

    [Theory]
    [InlineData(TocVariant.Rail, "rail", false)]
    [InlineData(TocVariant.Progress, "progress", true)]
    public void A_rail_adds_a_track_and_a_sliding_indicator_and_progress_a_fill_as_well(TocVariant variant, string name, bool fill)
    {
        var cut = Toc(p => p.Add(x => x.Variant, variant));

        Assert.Equal(name, cut.Find("nav").GetAttribute("data-variant"));
        Assert.Single(cut.FindAll("[data-slot=toc-track]"));
        Assert.Single(cut.FindAll("[data-slot=toc-indicator]"));
        Assert.Equal(fill ? 1 : 0, cut.FindAll("[data-slot=toc-progress]").Count);
        // Decoration only: none of it is read out or takes part in the link flow.
        Assert.All(cut.FindAll("[data-slot=toc-track], [data-slot=toc-indicator], [data-slot=toc-progress]"), part =>
        {
            Assert.Equal("true", part.GetAttribute("aria-hidden"));
            Assert.Contains("absolute", part.ClassList);
            Assert.Contains("pointer-events-none", part.ClassList);
        });
        // The links leave room for the rail.
        Assert.Contains("ps-4", Link(cut, "intro").ClassList);
        Assert.Contains("ps-7", Link(cut, "from-the-cli").ClassList);
        Assert.Contains("ps-10", Link(cut, "flags").ClassList);
        Assert.Contains("relative", cut.Find("nav").ClassList);
    }

    [Fact]
    public void The_rail_moves_in_with_the_padding_of_a_floating_panel()
    {
        Assert.Contains("start-0", Toc(p => p.Add(x => x.Variant, TocVariant.Rail)).Find("[data-slot=toc-track]").ClassList);

        var floating = Toc(p => p.Add(x => x.Variant, TocVariant.Rail).Add(x => x.Position, TocPosition.EndTop));
        Assert.Contains("start-3", floating.Find("[data-slot=toc-track]").ClassList);
        Assert.Contains("start-3", floating.Find("[data-slot=toc-indicator]").ClassList);
    }

    // ---- marks --------------------------------------------------------------------------------

    [Fact]
    public void Marks_draw_a_dash_a_heading_and_keep_every_label_in_the_page()
    {
        var cut = Toc(p => p.Add(x => x.Variant, TocVariant.Marks));

        Assert.Equal("marks", cut.Find("nav").GetAttribute("data-variant"));
        var links = cut.FindAll("a[data-slot=toc-link]");
        Assert.All(links, link => Assert.Equal("true", link.QuerySelector("[data-slot=toc-mark]")!.GetAttribute("aria-hidden")));
        // The labels are clipped while the panel is closed, never removed: a screen reader and
        // the Tab key still reach every section.
        Assert.Equal(
            ["Introduction", "Installation", "From the CLI", "Flags"],
            links.Select(link => link.QuerySelector("[data-slot=toc-label]")!.TextContent));
        Assert.Equal(["#intro", "#install", "#from-the-cli", "#flags"], links.Select(l => l.GetAttribute("href")));
        // The title has no room in a minimap, and stays for assistive tech.
        Assert.Contains("sr-only", cut.Find("[data-slot=toc-title]").ClassList);
    }

    [Fact]
    public void A_mark_is_as_long_as_its_heading_is_important()
    {
        var cut = Toc(p => p.Add(x => x.Variant, TocVariant.Marks));

        Assert.Contains("[--bz-toc-mark-w:var(--bz-toc-mark-base,1.5rem)]", Link(cut, "intro").ClassList);
        Assert.Contains("[--bz-toc-mark-w:calc(var(--bz-toc-mark-base,1.5rem)*0.667)]", Link(cut, "from-the-cli").ClassList);
        Assert.Contains("[--bz-toc-mark-w:calc(var(--bz-toc-mark-base,1.5rem)*0.5)]", Link(cut, "flags").ClassList);
    }

    // ---- peek ---------------------------------------------------------------------------------

    [Fact]
    public void Peek_gives_each_dash_a_card_of_its_own()
    {
        var cut = Toc(p => p.Add(x => x.Variant, TocVariant.Peek));

        Assert.Equal("peek", cut.Find("nav").GetAttribute("data-variant"));
        var link = Link(cut, "install");
        Assert.NotNull(link.QuerySelector("[data-slot=toc-mark]"));
        Assert.Equal("Installation", link.QuerySelector("[data-slot=toc-peek]")!.TextContent.Trim());
        // Each row is a hover group of its own, so only its own card opens.
        Assert.Contains("group/toc-item", link.ClassList);
        Assert.Contains("sr-only", cut.Find("[data-slot=toc-title]").ClassList);
        Assert.DoesNotContain("bz-toc-wave", cut.Find("nav").ClassList);
    }

    [Fact]
    public void A_peek_card_can_show_anything_about_its_heading()
    {
        var cut = Toc(p => p
            .Add(x => x.Variant, TocVariant.Peek)
            .Add(x => x.PeekContent, heading => $"<b>{heading.Level}</b> {heading.Text} ({heading.Id})"));

        Assert.Equal("3 From the CLI (from-the-cli)", Link(cut, "from-the-cli").QuerySelector("[data-slot=toc-peek]")!.TextContent.Trim());
    }

    [Theory]
    [InlineData(TocPosition.Inline, "start-full")]
    [InlineData(TocPosition.StartCenter, "start-full")]
    [InlineData(TocPosition.EndTop, "end-full")]
    [InlineData(TocPosition.EndCenter, "end-full")]
    [InlineData(TocPosition.EndBottom, "end-full")]
    public void A_peek_card_opens_toward_the_page(TocPosition position, string side)
    {
        var cut = Toc(p => p.Add(x => x.Variant, TocVariant.Peek).Add(x => x.Position, position));

        Assert.Contains(side, Link(cut, "intro").QuerySelector("[data-slot=toc-peek]")!.ClassList);
    }

    [Fact]
    public void Wave_packs_the_column_tighter_and_starts_from_shorter_marks()
    {
        var cut = Toc(p => p.Add(x => x.Variant, TocVariant.Peek).Add(x => x.Wave, true));

        Assert.Contains("bz-toc-wave", cut.Find("nav").ClassList);
        Assert.Contains("[--bz-toc-mark-w:var(--bz-toc-mark-base,1rem)]", Link(cut, "intro").ClassList);
        Assert.Contains("[--bz-toc-mark-w:calc(var(--bz-toc-mark-base,1rem)*0.75)]", Link(cut, "from-the-cli").ClassList);
        Assert.Contains("py-0.5", Link(cut, "intro").ClassList);

        var still = Toc(p => p.Add(x => x.Variant, TocVariant.Peek));
        Assert.Contains("[--bz-toc-mark-w:var(--bz-toc-mark-base,1.25rem)]", Link(still, "intro").ClassList);
        Assert.Contains("[--bz-toc-mark-w:calc(var(--bz-toc-mark-base,1.25rem)*0.7)]", Link(still, "from-the-cli").ClassList);
    }

    [Fact]
    public void Wave_is_only_a_peek_matter()
    {
        Assert.DoesNotContain("bz-toc-wave", Toc(p => p.Add(x => x.Wave, true)).Find("nav").ClassList);
        Assert.DoesNotContain("bz-toc-wave", Toc(p => p.Add(x => x.Variant, TocVariant.Marks).Add(x => x.Wave, true)).Find("nav").ClassList);
    }

    [Fact]
    public void Mark_width_and_wave_height_are_published_as_custom_properties_with_a_dot()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal("--bz-toc-mark-base:1.5rem",
                Toc(p => p.Add(x => x.MarkWidth, 1.5)).Find("nav").GetAttribute("style"));
            Assert.Equal("--bz-toc-wave-amp:0.75rem",
                Toc(p => p.Add(x => x.WaveAmplitude, 0.75)).Find("nav").GetAttribute("style"));
            Assert.Equal("--bz-toc-mark-base:2rem;--bz-toc-wave-amp:0.25rem",
                Toc(p => p.Add(x => x.MarkWidth, 2).Add(x => x.WaveAmplitude, 0.25)).Find("nav").GetAttribute("style"));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    // ---- placement ----------------------------------------------------------------------------

    [Theory]
    [InlineData(TocPosition.StartTop, "start-top", "start-6", "top-6")]
    [InlineData(TocPosition.StartCenter, "start-center", "start-6", "my-auto")]
    [InlineData(TocPosition.StartBottom, "start-bottom", "start-6", "bottom-6")]
    [InlineData(TocPosition.EndTop, "end-top", "end-6", "top-6")]
    [InlineData(TocPosition.EndCenter, "end-center", "end-6", "my-auto")]
    [InlineData(TocPosition.EndBottom, "end-bottom", "end-6", "bottom-6")]
    public void A_floating_panel_is_fixed_to_its_edge_with_a_size_and_scrolling_of_its_own(
        TocPosition position, string name, string side, string along)
    {
        var nav = Toc(p => p.Add(x => x.Position, position)).Find("nav");

        Assert.Equal(name, nav.GetAttribute("data-position"));
        Assert.Contains("fixed", nav.ClassList);
        Assert.Contains(side, nav.ClassList);
        Assert.Contains(along, nav.ClassList);
        Assert.Contains("w-56", nav.ClassList);
        Assert.Contains("overflow-y-auto", nav.ClassList);
        Assert.Contains("scrollbar-thin", nav.ClassList);
        // No surface unless one is asked for.
        Assert.DoesNotContain("bg-popover", nav.ClassList);
    }

    [Theory]
    [InlineData(TocVariant.Marks)]
    [InlineData(TocVariant.Peek)]
    public void A_floating_minimap_keeps_its_own_width(TocVariant variant)
    {
        var nav = Toc(p => p.Add(x => x.Variant, variant).Add(x => x.Position, TocPosition.EndCenter)).Find("nav");

        Assert.Contains("fixed", nav.ClassList);
        Assert.Contains("w-fit", nav.ClassList);
        Assert.DoesNotContain("w-56", nav.ClassList);
        Assert.DoesNotContain("overflow-y-auto", nav.ClassList);
    }

    [Fact]
    public void Sticky_pins_an_inline_panel_and_means_nothing_for_a_floating_one()
    {
        var inline = Toc(p => p.Add(x => x.Sticky, true)).Find("nav");
        Assert.Contains("sticky", inline.ClassList);
        Assert.Contains("top-20", inline.ClassList);

        Assert.DoesNotContain("sticky", Toc().Find("nav").ClassList);

        var floating = Toc(p => p.Add(x => x.Sticky, true).Add(x => x.Position, TocPosition.EndTop)).Find("nav");
        Assert.DoesNotContain("sticky", floating.ClassList);
        Assert.Contains("fixed", floating.ClassList);

        Assert.True(TocPosition.EndTop.IsFloating());
        Assert.False(TocPosition.Inline.IsFloating());
        Assert.Equal("", TocPosition.Inline.Classes());
    }

    // ---- customization ------------------------------------------------------------------------

    [Fact]
    public void LinkClass_is_merged_over_the_defaults()
    {
        var link = Link(Toc(p => p.Add(x => x.LinkClass, "py-2 data-active:text-primary")), "intro");

        Assert.Contains("py-2", link.ClassList);
        Assert.DoesNotContain("py-1", link.ClassList);
        Assert.Contains("data-active:text-primary", link.ClassList);
        Assert.DoesNotContain("data-active:text-foreground", link.ClassList);
        Assert.Contains("bz-toc-link", link.ClassList);
    }

    [Fact]
    public void ItemContent_replaces_the_links_wholesale()
    {
        var cut = Toc(p => p
            .Add(x => x.Variant, TocVariant.Marks)
            .Add(x => x.ItemContent, heading =>
                $"<a href=\"#{heading.Id}\" data-toc-id=\"{heading.Id}\" class=\"own\">{heading.Level}. {heading.Text}</a>"));

        Assert.Equal(
            ["2. Introduction", "2. Installation", "3. From the CLI", "4. Flags"],
            cut.FindAll("a.own").Select(link => link.TextContent));
        Assert.Empty(cut.FindAll("[data-slot=toc-link], [data-slot=toc-mark]"));
    }

    [Fact]
    public void Class_and_other_attributes_reach_the_nav()
    {
        var nav = Toc(p => p
            .Add(x => x.Class, "hidden xl:block")
            .AddUnmatched("aria-label", "On this page")
            .AddUnmatched("id", "page-toc")).Find("nav");

        Assert.Equal("page-toc", nav.Id);
        Assert.Equal("On this page", nav.GetAttribute("aria-label"));
        Assert.Contains("xl:block", nav.ClassList);
        Assert.Contains("bz-toc", nav.ClassList);
    }

    // ---- the script ---------------------------------------------------------------------------

    [Fact]
    public void Headings_are_looked_for_in_main_and_followed_with_the_address_bar_by_default()
    {
        Toc();

        var scan = _module.Invocations["scan"].Single();
        Assert.Equal(["main", "h2, h3"], scan.Arguments);
        var spy = _module.Invocations["createSpy"].Single();
        // nav, content, headings, top offset, scroll root, update the hash
        Assert.Equal(["main", "h2, h3", 0, null, true], spy.Arguments.Skip(1));
    }

    [Fact]
    public void Where_to_look_and_how_to_follow_can_be_set()
    {
        Toc(p => p
            .Add(x => x.ContentSelector, "article")
            .Add(x => x.HeadingSelector, "h2, h3, h4")
            .Add(x => x.TopOffset, 80)
            .Add(x => x.RootSelector, "#scroller")
            .Add(x => x.UpdateHash, false));

        Assert.Equal(["article", "h2, h3, h4"], _module.Invocations["scan"].Single().Arguments);
        Assert.Equal(["article", "h2, h3, h4", 80, "#scroller", false], _module.Invocations["createSpy"].Single().Arguments.Skip(1));
    }
}
