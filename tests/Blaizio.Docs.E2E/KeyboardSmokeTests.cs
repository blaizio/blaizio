using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// Keyboard contracts through a REAL browser (the roving-focus, dismissal and focus-return logic
/// live in JS, which bUnit cannot exercise): tabs arrow navigation with manual activation,
/// accordion toggling, dialog escape + focus return.
/// </summary>
[Collection("docs-e2e")]
public sealed class KeyboardSmokeTests(DocsServerFixture fx)
{
    [E2EFact]
    public async Task Tabs_arrow_moves_focus_and_enter_selects()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/tabs");

        var firstList = page.Locator("[role=tablist]").First;
        var tabs = firstList.Locator("[role=tab]");
        await tabs.First.FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");

        // Manual activation (the default): arrowing only moves focus - the selection stays put.
        await Assertions.Expect(tabs.Nth(1)).ToBeFocusedAsync();
        await Assertions.Expect(tabs.First).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(tabs.Nth(1)).ToHaveAttributeAsync("aria-selected", "false");

        // Enter activates the focused tab.
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(tabs.Nth(1)).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(tabs.First).ToHaveAttributeAsync("aria-selected", "false");
    }

    [E2EFact]
    public async Task Accordion_enter_toggles_section()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/accordion");

        // The default demo starts with its first item open; the second is closed.
        var trigger = page.Locator("[data-slot=accordion-trigger]").Nth(1);
        await trigger.FocusAsync();
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "false");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true");
    }

    [E2EFact]
    public async Task Dialog_escape_closes_and_returns_focus()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/dialog");

        var trigger = page.GetByRole(AriaRole.Button, new() { Name = "Edit profile" }).First;
        await trigger.ClickAsync();

        var dialog = DocsServerFixture.VisibleDialog(page);
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await DocsServerFixture.WaitForDialogFocusAsync(page);

        // Escape closes and returns focus to the trigger.
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog).Not.ToBeVisibleAsync();
        await Assertions.Expect(trigger).ToBeFocusedAsync();
    }
}
