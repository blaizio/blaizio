using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// axe-core over key docs pages, light and dark. Serious/critical violations fail; moderate and
/// minor ones are reported through the assertion message when the gate trips, so the fix lands
/// with context. (The docs pages double as the component fixtures - a violation here is almost
/// always a component defect, not a docs defect.)
/// </summary>
[Collection("docs-e2e")]
public sealed class AxeTests(DocsServerFixture fx)
{
    public static readonly TheoryData<string, string> Cases = new()
    {
        { "", "light" },
        { "docs/components/accordion", "light" },
        { "docs/components/tabs", "light" },
        { "docs/components/select", "light" },
        { "docs/components/switch", "dark" },
        { "docs/components/carousel", "dark" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Page_has_no_serious_axe_violations(string route, string theme)
    {
        if (!E2E.Enabled) return;

        await using var context = await fx.NewContextAsync(theme: theme);
        var page = await DocsServerFixture.OpenAsync(context, route);

        // The carousel viewport is EXCLUDED by design, not oversight: it is a scroll-snap
        // container deliberately kept out of the tab order (tabindex -1) because the APG carousel
        // pattern routes keyboard access through the prev/next/dot controls and the root's arrow
        // handler - axe's scrollable-region-focusable can't see that contract and false-positives.
        var scope = new AxeRunContext
        {
            Exclude = [new AxeSelector("[data-slot=carousel-content]")],
        };
        AxeResult results = await page.RunAxe(scope);

        var blocking = results.Violations
            .Where(v => v.Impact is "serious" or "critical")
            .Select(v => $"{v.Impact}: {v.Id} - {v.Help} ({v.Nodes.Length} nodes, e.g. {v.Nodes.FirstOrDefault()?.Target})")
            .ToList();

        Assert.True(blocking.Count == 0,
            $"axe violations on /{route} ({theme}):\n{string.Join('\n', blocking)}");
    }
}
