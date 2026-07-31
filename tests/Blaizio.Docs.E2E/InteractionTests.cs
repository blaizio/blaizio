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
    [Fact]
    public async Task Copy_control_writes_the_snippet_to_the_clipboard()
    {
        if (!E2E.Enabled) return;

        await using var context = await fx.NewContextAsync();
        await context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/accordion");

        var copy = page.GetByLabel("Copy code").First;
        await copy.ScrollIntoViewIfNeededAsync();
        await copy.ClickAsync();

        var clipboard = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        Assert.Contains("BzAccordion", clipboard);
    }

    [Fact]
    public async Task Mobile_rail_opens_the_nav_sheet()
    {
        if (!E2E.Enabled) return;

        await using var context = await fx.NewContextAsync(
            viewport: new ViewportSize { Width = 375, Height = 812 });
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/tabs");

        var trigger = page.Locator("[data-slot=sidebar-trigger]").First;
        await Assertions.Expect(trigger).ToBeVisibleAsync();
        await trigger.ClickAsync();

        // The mobile sidebar is a sheet: a dialog carrying the nav links (:visible skips other
        // dialog surfaces the page keeps mounted while closed).
        var sheet = page.Locator("[role=dialog]:visible").First;
        await Assertions.Expect(sheet).ToBeVisibleAsync();
        await Assertions.Expect(sheet.Locator("a[href*='docs/components']").First).ToBeVisibleAsync();

        // Same rule as the dialog test: wait for the focus scope to land inside before Escape,
        // or the key hits the page body and never reaches the sheet's keydown handler.
        await page.WaitForFunctionAsync("() => document.activeElement?.closest('[role=dialog]') != null");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(sheet).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Theme_composer_mounts_its_control_surface()
    {
        if (!E2E.Enabled) return;

        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "create", readySelector: "main");

        // The composer's control surface and live preview are both up (lazy-mounted pages from
        // audit batch 7 still land on a working first visit). No h1 here - it is a full-bleed
        // composer, so presence = an interactive control surface.
        Assert.True(await page.Locator("button").CountAsync() > 10, "control surface should be interactive");
    }
}
