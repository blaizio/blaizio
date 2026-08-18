using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// The docs chrome itself: the copy control writes the snippet to the clipboard, the mobile rail
/// opens the nav sheet, and the theme-composer page mounts its control surface.
/// </summary>
[Collection("docs-e2e")]
public sealed class InteractionTests(DocsServerFixture fx)
{
    [E2EFact]
    public async Task Copy_control_writes_the_snippet_to_the_clipboard()
    {
        await using var context = await fx.NewContextAsync();
        await context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/accordion");

        var copy = page.GetByLabel("Copy code").First;
        await copy.ScrollIntoViewIfNeededAsync();
        await copy.ClickAsync();

        var clipboard = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        Assert.Contains("BzAccordion", clipboard);
    }

    [E2EFact]
    public async Task Mobile_rail_opens_the_nav_sheet()
    {
        await using var context = await fx.NewContextAsync(
            viewport: new ViewportSize { Width = 375, Height = 812 });
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/tabs");

        var trigger = page.Locator("[data-slot=sidebar-trigger]").First;
        await Assertions.Expect(trigger).ToBeVisibleAsync();
        await trigger.ClickAsync();

        // The mobile sidebar is a sheet: a dialog carrying the nav links.
        var sheet = DocsServerFixture.VisibleDialog(page);
        await Assertions.Expect(sheet).ToBeVisibleAsync();
        await Assertions.Expect(sheet.Locator("a[href*='docs/components']").First).ToBeVisibleAsync();

        await DocsServerFixture.WaitForDialogFocusAsync(page);
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(sheet).Not.ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task Virtualizer_infinite_demo_loads_the_next_batch_on_scroll()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/virtualizer");

        var demo = page.Locator("div.space-y-3").Filter(new() { HasText = "of 500 rows loaded" });
        var status = demo.Locator("p").Filter(new() { HasText = "of 500 rows loaded" });
        await status.ScrollIntoViewIfNeededAsync();

        // The first batch ships with the page and nothing loads eagerly.
        await Assertions.Expect(status).ToContainTextAsync("30 of 500");

        // Scrolling to the bottom arms OnLoadMore: the demo's simulated round trip lands batch 2.
        var viewport = demo.Locator("[data-slot=virtualizer-viewport]");
        await viewport.EvaluateAsync("el => { el.scrollTop = el.scrollHeight; }");
        await Assertions.Expect(status).ToContainTextAsync("60 of 500", new() { Timeout = 5000 });
    }

    [E2EFact]
    public async Task Virtualizer_scroll_to_index_jumps_across_the_list()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/virtualizer");

        var jump = page.GetByRole(AriaRole.Button, new() { Name = "Row 50,000" });
        await jump.ScrollIntoViewIfNeededAsync();

        // InitialItemIndex opened the list at row 500, not the top.
        var demo = page.Locator("div.space-y-3").Filter(new() { Has = jump });
        var viewport = demo.Locator("[data-slot=virtualizer-viewport]");
        await Assertions.Expect(viewport.Locator("[data-bz-virtual-index='500']")).ToBeVisibleAsync();

        // ScrollToIndexAsync lands the target row at the top of the box.
        await jump.ClickAsync();
        await Assertions.Expect(viewport.Locator("[data-bz-virtual-index='50000']")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task Virtualizer_provider_demo_fetches_windows_on_demand()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/virtualizer");

        var demo = page.Locator("div.space-y-3").Filter(new() { HasText = "Windows served" });
        var viewport = demo.Locator("[data-slot=virtualizer-viewport]");
        await viewport.ScrollIntoViewIfNeededAsync();

        // The seed window landed and the scrollbar spans all 500,000 rows (22M px - the demo
        // stays under the browser's ~33M px element-height cap on purpose).
        await Assertions.Expect(viewport.Locator("[data-bz-virtual-index='0']")).ToBeVisibleAsync();
        var scrollHeight = await viewport.EvaluateAsync<double>("el => el.scrollHeight");
        Assert.True(scrollHeight > 20_000_000, $"scroll range should span the total, got {scrollHeight}");

        // Jumping deep into the list fetches just that window: skeleton placeholders first,
        // then the fetched rows replace them.
        await viewport.EvaluateAsync("el => { el.scrollTop = el.scrollHeight / 2; }");
        await Assertions.Expect(viewport.Locator("[data-slot=skeleton]").First).ToBeVisibleAsync();
        await Assertions.Expect(demo.Locator("[data-bz-virtual-index] span.font-medium").First)
            .ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [E2EFact]
    public async Task Theme_composer_mounts_its_control_surface()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "themes");

        // The composer's control surface and live preview are both up (lazy-mounted pages from
        // audit batch 7 still land on a working first visit). No h1 here - it is a full-bleed
        // composer, so presence = an interactive control surface.
        Assert.True(await page.Locator("button").CountAsync() > 10, "control surface should be interactive");
    }
}
