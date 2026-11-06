using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// The editable data table's keyboard grid through a real browser (ts/dataTable.ts owns the cursor
/// and the edit lifecycle; bUnit cannot run it). Uses the Table page's editing demos, in page order:
/// cells (0), rows (1), dialog (2), your own dialog (3), 10,000 virtualized rows (4).
/// </summary>
[Collection("docs-e2e")]
public sealed class DataTableEditingTests(DocsServerFixture fx)
{
    private static ILocator Grid(IPage page, int index) => page.Locator("table[role=grid]").Nth(index);

    private static ILocator Cell(IPage page, int grid, int row, int col) =>
        Grid(page, grid).Locator($"tbody tr[data-bz-row-index=\"{row}\"] > td").Nth(col);

    // The field an open editor types into: the library control's own input.
    private static ILocator Field(IPage page, int grid) => Grid(page, grid).Locator("[data-bz-cell-editor] input").First;

    private static async Task<IPage> OpenAsync(IBrowserContext context)
    {
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/table");
        // Each grid's script marks its table once it has attached; keys pressed before that reach
        // no grid (Tab still does: the first tab stop is rendered with the table).
        await page.WaitForFunctionAsync("() => document.querySelectorAll('table[role=grid][data-bz-grid]').length >= 5");
        return page;
    }

    [E2EFact]
    public async Task Tab_from_outside_lands_on_the_first_cell_and_Enter_moves_down()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        // Focus a control placed right before the table, then Tab into it.
        await Grid(page, 0).EvaluateAsync(@"table => {
            const before = document.createElement('button');
            before.id = 'before-grid';
            before.textContent = 'Before';
            table.closest('[data-slot=data-table]').before(before);
            before.focus();
        }");
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(Cell(page, 0, 0, 0)).ToBeFocusedAsync();

        // Spreadsheet-style: Enter moves down, Shift+Enter up; after a run of Tabs, Enter returns
        // to the column the run began in.
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Cell(page, 0, 1, 0)).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Shift+Enter");
        await Assertions.Expect(Cell(page, 0, 0, 0)).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Cell(page, 0, 1, 0)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task Typing_over_a_cell_edits_it_in_the_library_text_box_and_enter_commits_and_moves_down()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, 0, 0, 0).ClickAsync();
        await page.Keyboard.TypeAsync("Wireless mouse");
        var editor = Field(page, 0);
        await Assertions.Expect(editor).ToBeFocusedAsync();
        await Assertions.Expect(editor).ToHaveValueAsync("Wireless mouse");
        await Assertions.Expect(editor).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bbz-input\b"));

        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Cell(page, 0, 0, 0)).ToHaveTextAsync("Wireless mouse");
        await Assertions.Expect(Cell(page, 0, 1, 0)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task Keys_typed_right_after_a_commit_are_not_lost()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        // Price, then Tab into Stock and keep typing with no pause: the commit's re-render must not
        // take the focus (and the next keys) with it.
        await Cell(page, 0, 2, 2).ClickAsync();
        await page.Keyboard.TypeAsync("80");
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.TypeAsync("5");
        await page.Keyboard.PressAsync("Tab");

        await Assertions.Expect(Cell(page, 0, 2, 2)).ToHaveTextAsync("$80.00");
        await Assertions.Expect(Cell(page, 0, 2, 3)).ToHaveTextAsync("5");
        await Assertions.Expect(Cell(page, 0, 2, 4)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task An_invalid_value_keeps_the_editor_and_escape_restores()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, 0, 0, 2).ClickAsync();
        await page.Keyboard.TypeAsync("0");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(Grid(page, 0).Locator(".bz-table-cell-error")).ToHaveTextAsync("Price must be above zero.");
        await Assertions.Expect(Field(page, 0)).ToBeFocusedAsync();

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Cell(page, 0, 0, 2)).ToHaveTextAsync("$129.00");
        await Assertions.Expect(Cell(page, 0, 0, 2)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task A_pick_list_commits_on_an_arrow_after_typing_and_on_a_pick()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        // Typing over Category jumps the list to the first match; an arrow commits and moves.
        await Cell(page, 0, 2, 1).ClickAsync();
        await page.Keyboard.TypeAsync("h");
        var trigger = Grid(page, 0).Locator("[data-bz-cell-editor=select] [data-slot=select-trigger]");
        await Assertions.Expect(trigger).ToBeFocusedAsync();
        await Assertions.Expect(trigger).ToHaveTextAsync("Hardware");
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(Cell(page, 0, 2, 1)).ToHaveTextAsync("Hardware");
        await Assertions.Expect(Cell(page, 0, 3, 1)).ToBeFocusedAsync();

        // F2 edits in place: Space opens the list, a pick commits and the cursor stays.
        await page.Keyboard.PressAsync("F2");
        await Assertions.Expect(trigger).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(page.Locator("[role=option]:focus")).ToHaveCountAsync(1);
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Cell(page, 0, 3, 1)).ToHaveTextAsync("Software");
        await Assertions.Expect(Cell(page, 0, 3, 1)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task Clicking_another_cell_commits_and_puts_the_cursor_there()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, 0, 5, 0).DblClickAsync();
        await Assertions.Expect(Field(page, 0)).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.TypeAsync(" XL");

        await Cell(page, 0, 0, 3).ClickAsync();

        await Assertions.Expect(Cell(page, 0, 5, 0)).ToHaveTextAsync("Monitor arm XL");
        await Assertions.Expect(Cell(page, 0, 0, 3)).ToBeFocusedAsync();
        await Assertions.Expect(Grid(page, 0).Locator("[data-bz-cell-editor]")).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task A_rejected_row_stays_open_with_the_typed_value_and_its_message()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, 1, 0, 0).ClickAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Grid(page, 1).Locator("tr[data-editing] [data-bz-cell-editor]")).ToHaveCountAsync(5);

        // Tab walks the row's fields, a date's parts included, into Check-out (the fourth cell).
        var checkOut = Grid(page, 1).Locator("tr[data-editing] td").Nth(3).Locator("[data-bz-cell-editor]");
        for (var i = 0; i < 12 && !await checkOut.EvaluateAsync<bool>("e => e.contains(document.activeElement)"); i++)
            await page.Keyboard.PressAsync("Tab");
        // Before the check-in (12 Nov): refused by the demo's OnEdit.
        await page.Keyboard.TypeAsync("11102026");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(Grid(page, 1).Locator(".bz-table-cell-error")).ToHaveTextAsync("Check-out must be after check-in.");
        await Assertions.Expect(checkOut.Locator("input[aria-label=Day]")).ToHaveValueAsync("10");
        Assert.True(await checkOut.EvaluateAsync<bool>("e => e.contains(document.activeElement)"));

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Grid(page, 1).Locator("tr[data-editing]")).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, 1, 0, 3)).ToHaveTextAsync("15 Nov");
    }

    [E2EFact]
    public async Task Enter_on_a_row_opens_the_dialog_and_save_applies_the_change()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, 2, 0, 1).ClickAsync();
        await page.Keyboard.PressAsync("Enter");
        var dialog = DocsServerFixture.VisibleDialog(page);
        await Assertions.Expect(dialog.GetByLabel("Notes")).ToBeVisibleAsync(); // a hidden column, still a field
        await DocsServerFixture.WaitForDialogFocusAsync(page);

        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("amara@example.org");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("[role=dialog]:visible")).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, 2, 0, 1)).ToHaveTextAsync("amara@example.org");
        await Assertions.Expect(Cell(page, 2, 0, 1)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task Your_own_dialog_saves_through_its_context()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, 3, 0, 0).ClickAsync();
        await page.Keyboard.PressAsync("Enter");
        var sheet = DocsServerFixture.VisibleDialog(page);
        await Assertions.Expect(sheet.GetByLabel("Estimate")).ToBeVisibleAsync();
        await DocsServerFixture.WaitForDialogFocusAsync(page);

        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("Fix the login redirect");
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Save ticket" }).ClickAsync();

        await Assertions.Expect(page.Locator("[role=dialog]:visible")).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, 3, 0, 0)).ToHaveTextAsync("Fix the login redirect");
    }

    [E2EFact]
    public async Task Ctrl_end_reaches_the_last_of_ten_thousand_virtualized_rows()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, 4, 0, 1).ClickAsync();
        await page.Keyboard.PressAsync("Control+End");

        await Assertions.Expect(Cell(page, 4, 9999, 4)).ToBeFocusedAsync();
        await Assertions.Expect(Grid(page, 4)).ToHaveAttributeAsync("aria-rowcount", "10001");

        await page.Keyboard.PressAsync("Control+Home");
        await Assertions.Expect(Cell(page, 4, 0, 0)).ToBeFocusedAsync();
    }
}
