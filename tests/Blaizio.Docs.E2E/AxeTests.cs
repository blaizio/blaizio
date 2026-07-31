using System.Text.Json;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// axe-core over one docs page per component family, light and dark mixed. Serious/critical
/// violations FAIL; every violation of every impact (moderate and minor included) is written to
/// AxeResults/*.json regardless of outcome, so lower-impact findings are recorded evidence, not
/// silently discarded - CI uploads the directory as a build artifact. (The docs pages double as
/// the component fixtures - a violation here is almost always a component defect, not a docs
/// defect.)
/// </summary>
[Collection("docs-e2e")]
public sealed class AxeTests(DocsServerFixture fx)
{
    // One representative page per component family. A new family should add its page here.
    public static readonly TheoryData<string, string> Cases = new()
    {
        // Landing + disclosure
        { "", "light" },
        { "docs/components/accordion", "light" },
        { "docs/components/tabs", "light" },
        { "docs/components/carousel", "dark" },
        // Overlays
        { "docs/components/dialog", "light" },
        { "docs/components/drawer", "dark" },
        { "docs/components/toast", "light" },
        // Menus
        { "docs/components/dropdown-menu", "light" },
        { "docs/components/menubar", "dark" },
        { "docs/components/command", "dark" },
        // Selection
        { "docs/components/select", "light" },
        { "docs/components/combobox", "light" },
        { "docs/components/checkbox", "light" },
        { "docs/components/radio-group", "dark" },
        { "docs/components/switch", "dark" },
        { "docs/components/slider", "light" },
        // Text + composite input
        { "docs/components/input-text", "light" },
        { "docs/components/input-date", "dark" },
        { "docs/components/calendar", "light" },
        { "docs/components/color-picker", "dark" },
        // Data + structure
        { "docs/components/table", "light" },
        { "docs/components/tree", "dark" },
        { "docs/components/sidebar", "light" },
        { "docs/components/navigation-menu", "dark" },
        { "docs/components/chart", "dark" },
    };

    [E2ETheory]
    [MemberData(nameof(Cases))]
    public async Task Page_has_no_serious_axe_violations(string route, string theme)
    {
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

        var artifactPath = await WriteArtifactAsync(route, theme, results);

        var blocking = results.Violations
            .Where(v => v.Impact is "serious" or "critical")
            .Select(Describe)
            .ToList();
        var recorded = results.Violations
            .Where(v => v.Impact is not ("serious" or "critical"))
            .Select(Describe)
            .ToList();

        Assert.True(blocking.Count == 0,
            $"axe violations on /{route} ({theme}):\n{string.Join('\n', blocking)}"
            + (recorded.Count > 0 ? $"\nAlso recorded (not blocking):\n{string.Join('\n', recorded)}" : "")
            + $"\nFull results: {artifactPath}");
    }

    private static string Describe(AxeResultItem v) =>
        $"{v.Impact}: {v.Id} - {v.Help} ({v.Nodes.Length} nodes, e.g. {v.Nodes.FirstOrDefault()?.Target})";

    /// <summary>Every violation of every impact for this page, one JSON file per case.</summary>
    private static async Task<string> WriteArtifactAsync(string route, string theme, AxeResult results)
    {
        var dir = Path.Combine(E2E.RepoRoot, "tests", "Blaizio.Docs.E2E", "AxeResults");
        Directory.CreateDirectory(dir);
        var name = route.Length == 0 ? "home" : route.Replace('/', '_');
        var path = Path.Combine(dir, $"{name}-{theme}.json");

        var document = new
        {
            route = $"/{route}",
            theme,
            violations = results.Violations.Select(v => new
            {
                impact = v.Impact,
                id = v.Id,
                help = v.Help,
                helpUrl = v.HelpUrl,
                nodes = v.Nodes.Length,
                sampleTarget = v.Nodes.FirstOrDefault()?.Target?.ToString(),
            }),
        };
        await File.WriteAllTextAsync(path,
            JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }
}
