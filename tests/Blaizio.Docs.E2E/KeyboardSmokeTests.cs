using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// Keyboard contracts through a REAL browser (the roving-focus, dismissal and focus-return logic
/// live in JS, which bUnit cannot exercise): tabs arrow navigation with automatic activation,
/// accordion toggling, dialog escape + focus return.
/// </summary>
[Collection("docs-e2e")]
public sealed class KeyboardSmokeTests(DocsServerFixture fx)
{
    [Fact]
    public async Task Tabs_arrow_moves_selection()
    {
        if (!E2E.Enabled) return;

        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/tabs");

        var firstList = page.Locator("[role=tablist]").First;
        var tabs = firstList.Locator("[role=tab]");
        await tabs.First.FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");

        // Automatic activation: arrowing selects the newly focused trigger.
        await Assertions.Expect(tabs.Nth(1)).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(tabs.First).ToHaveAttributeAsync("aria-selected", "false");
    }

    [Fact]
    public async Task Accordion_enter_toggles_section()
    {
        if (!E2E.Enabled) return;

        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/accordion");

        // The default demo starts with its first item open; the second is closed.
        var trigger = page.Locator("[data-slot=accordion-trigger]").Nth(1);
        await trigger.FocusAsync();
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "false");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true");
    }

    [Fact]
    public async Task Dialog_escape_closes_and_returns_focus()
    {
        if (!E2E.Enabled) return;

        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/dialog");

        var trigger = page.GetByRole(AriaRole.Button, new() { Name = "Edit profile" }).First;
        await trigger.ClickAsync();

        var dialog = page.Locator("[role=dialog]:visible").First;
        await Assertions.Expect(dialog).ToBeVisibleAsync();

        // The focus scope moves focus into the dialog asynchronously; an Escape pressed before
        // that lands on the page body and never reaches the dialog's keydown handler.
        await page.WaitForFunctionAsync("() => document.activeElement?.closest('[role=dialog]') != null");

        // Escape closes and returns focus to the trigger.
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog).Not.ToBeVisibleAsync();
        await Assertions.Expect(trigger).ToBeFocusedAsync();
    }
}
