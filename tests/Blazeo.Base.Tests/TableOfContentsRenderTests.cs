using Bunit;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render tests for the headless table of contents. Heading discovery and the scrollspy are JS
/// (ts/toc.ts, verified in-browser); these cover the C# contract: the nav stays hidden and empty
/// until the module reports headings (Loose JSInterop returns none).
/// </summary>
public class TableOfContentsRenderTests : TestContext
{
    public TableOfContentsRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Renders_hidden_empty_nav_until_headings_arrive()
    {
        var cut = RenderComponent<BzTableOfContents>();

        var nav = cut.Find("nav");
        Assert.NotNull(nav.GetAttribute("hidden"));
        Assert.Empty(cut.FindAll("a"));
    }

    [Fact]
    public void Forwards_class_and_attributes_to_the_nav()
    {
        var cut = RenderComponent<BzTableOfContents>(p => p
            .Add(x => x.Class, "text-sm")
            .AddUnmatched("data-slot", "toc"));

        var nav = cut.Find("nav");
        Assert.Equal("text-sm", nav.GetAttribute("class"));
        Assert.Equal("toc", nav.GetAttribute("data-slot"));
    }
}
