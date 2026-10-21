using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// Keyboard contracts through a REAL browser (the roving-focus, dismissal and focus-return logic
/// live in JS, which bUnit cannot exercise): tabs arrow navigation with manual activation,
/// accordion toggling, dialog escape + focus return, and a sortable leaving the keys typed into a
/// control inside an item to that control.
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

    [E2EFact]
    public async Task Sortable_keys_typed_into_a_control_inside_an_item_stay_the_controls()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/sortable");
        // The list's key listener comes with its script, which loads after the first render.
        await page.WaitForFunctionAsync(
            "() => performance.getEntriesByType('resource').some(e => e.name.includes('/dist/sortable.js'))");

        // The Default demo's first item.
        var item = page.Locator("[data-sortable-item]", new() { HasText = "Apple" }).First;

        // A key on the item itself still picks it up, and Escape puts it back.
        await item.FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(item).ToHaveAttributeAsync("data-grabbed", "true");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(item).Not.ToHaveAttributeAsync("data-grabbed", "true");

        // Give the item a text box, like a card holding a search field, and type into it.
        await item.EvaluateAsync(
            "el => el.append(Object.assign(document.createElement('input'), { id: 'e2e-sortable-input' }))");
        var input = page.Locator("#e2e-sortable-input");
        await input.FocusAsync();
        await page.Keyboard.TypeAsync("a b");        // Space types a space,
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.Keyboard.PressAsync("ArrowLeft"); // the arrows move the caret,
        await page.Keyboard.TypeAsync("c");
        await page.Keyboard.PressAsync("Home");      // and so does Home.
        await page.Keyboard.TypeAsync("d");
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(input).ToHaveValueAsync("dac b");
        // No key grabbed the item or moved focus to another one.
        await Assertions.Expect(input).ToBeFocusedAsync();
        await Assertions.Expect(item).Not.ToHaveAttributeAsync("data-grabbed", "true");
    }
}
