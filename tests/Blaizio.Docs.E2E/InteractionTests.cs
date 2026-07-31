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
    public async Task Theme_composer_mounts_its_control_surface()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "create");

        // The composer's control surface and live preview are both up (lazy-mounted pages from
        // audit batch 7 still land on a working first visit). No h1 here - it is a full-bleed
        // composer, so presence = an interactive control surface.
        Assert.True(await page.Locator("button").CountAsync() > 10, "control surface should be interactive");
    }
}
