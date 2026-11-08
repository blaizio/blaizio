using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// The editable data table's keyboard grid through a real browser (ts/dataTable.ts owns the cursor,
/// how an edit starts and the edit lifecycle; bUnit cannot run it). Uses the Table page's editing
/// demos, found by a header only they have: cells (Cut-off), rows (Check-in), dialog (Email), your
/// own dialog (Estimate), drawer (Seats), your own drawer (Ships), 10,000 virtualized rows (Unit price),
/// the edit actions column (Hours a week), your own actions column (Spent), custom cells (Open issues).
/// </summary>
[Collection("docs-e2e")]
public sealed class DataTableEditingTests(DocsServerFixture fx)
{
    private const string Cells = "Cut-off";
    private const string Rows = "Check-in";
    private const string Dialog = "Email";
    private const string OwnDialog = "Estimate";
    private const string Drawer = "Seats";
    private const string Large = "Unit price";
    private const string Actions = "Hours a week";
    private const string OwnActions = "Spent";
    private const string Custom = "Open issues";

    private static ILocator Grid(IPage page, string header) =>
        page.Locator("table[role=grid]").Filter(new() { Has = page.Locator($"th:has-text(\"{header}\")") }).First;

    private static ILocator Cell(IPage page, string grid, int row, int col) =>
        Grid(page, grid).Locator($"tbody tr[data-bz-row-index=\"{row}\"] > td").Nth(col);

    // The field an open editor types into: the library control's own input.
    private static ILocator Field(IPage page, string grid) => Grid(page, grid).Locator("[data-bz-cell-editor] input").First;

    private static ILocator Editors(IPage page, string grid) => Grid(page, grid).Locator("[data-bz-cell-editor]");

    // The option holding the focus in an open list (the list portals to the page's body).
    private static ILocator FocusedOption(IPage page) => page.Locator("[role=option]:focus");

    private static async Task<IPage> OpenAsync(IBrowserContext context)
    {
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/table");
        // Each grid's script marks its table once it has attached; keys pressed before that reach
        // no grid (Tab still does: the first tab stop is rendered with the table).
        await page.WaitForFunctionAsync("() => document.querySelectorAll('table[role=grid][data-bz-grid]').length >= 8");
        return page;
    }

    // The point at a fraction of a cell's text, for a double-click that lands on a character.
    private static async Task<(float X, float Y)> TextPointAsync(ILocator cell, double fraction)
    {
        await cell.ScrollIntoViewIfNeededAsync();
        var box = await cell.EvaluateAsync<float[]>(@"td => {
            const r = document.createRange(); r.selectNodeContents(td); const b = r.getBoundingClientRect();
            return [b.x, b.y, b.width, b.height]; }");
        return ((float)(box[0] + box[2] * fraction), box[1] + box[3] / 2);
    }

    [E2EFact]
    public async Task Tab_from_outside_lands_on_the_first_cell_and_Enter_moves_down()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        // Focus a control placed right before the table, then Tab into it.
        await Grid(page, Cells).EvaluateAsync(@"table => {
            const before = document.createElement('button');
            before.id = 'before-grid';
            before.textContent = 'Before';
            table.closest('[data-slot=data-table]').before(before);
            before.focus();
        }");
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(Cell(page, Cells, 0, 0)).ToBeFocusedAsync();

        // Spreadsheet-style: Enter moves down, Shift+Enter up; after a run of Tabs, Enter returns
        // to the column the run began in.
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Cell(page, Cells, 1, 0)).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Shift+Enter");
        await Assertions.Expect(Cell(page, Cells, 0, 0)).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Cell(page, Cells, 1, 0)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task Typing_over_a_cell_edits_it_in_the_library_text_box_and_enter_commits_and_moves_down()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, Cells, 0, 0).ClickAsync();
        await page.Keyboard.TypeAsync("Wireless mouse");
        var editor = Field(page, Cells);
        await Assertions.Expect(editor).ToBeFocusedAsync();
        await Assertions.Expect(editor).ToHaveValueAsync("Wireless mouse");
        await Assertions.Expect(editor).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bbz-input\b"));

        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Cell(page, Cells, 0, 0)).ToHaveTextAsync("Wireless mouse");
        await Assertions.Expect(Cell(page, Cells, 1, 0)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task A_click_selects_and_a_double_click_edits_in_place_with_the_caret_where_it_landed()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var name = Cell(page, Cells, 0, 0);

        await name.ClickAsync();
        await Assertions.Expect(name).ToBeFocusedAsync();
        await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);

        // Halfway along "Mechanical keyboard": the caret lands inside the text, not at its end.
        var (x, y) = await TextPointAsync(name, 0.5);
        await page.Mouse.DblClickAsync(x, y);
        var field = Field(page, Cells);
        await Assertions.Expect(field).ToBeFocusedAsync();
        await Assertions.Expect(field).ToHaveValueAsync("Mechanical keyboard");
        var caret = await field.EvaluateAsync<int[]>("i => [i.selectionStart, i.selectionEnd]");
        Assert.Equal(caret[0], caret[1]);
        Assert.InRange(caret[0], 5, 14);

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);
        await Assertions.Expect(name).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task Keys_typed_right_after_a_commit_are_not_lost()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        // Price, then Tab into Stock and keep typing with no pause: the commit's re-render must not
        // take the focus (and the next keys) with it.
        await Cell(page, Cells, 2, 2).ClickAsync();
        await page.Keyboard.TypeAsync("80");
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.TypeAsync("5");
        await page.Keyboard.PressAsync("Tab");

        await Assertions.Expect(Cell(page, Cells, 2, 2)).ToHaveTextAsync("$80.00");
        await Assertions.Expect(Cell(page, Cells, 2, 3)).ToHaveTextAsync("5");
        await Assertions.Expect(Cell(page, Cells, 2, 4)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task A_number_starts_on_a_digit_and_a_date_on_a_digit_with_its_parts_empty()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, Cells, 1, 2).ClickAsync();
        await page.Keyboard.TypeAsync("x");
        await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);
        await page.Keyboard.TypeAsync("7");
        await Assertions.Expect(Field(page, Cells)).ToHaveValueAsync("7");
        await page.Keyboard.PressAsync("Escape");

        await Cell(page, Cells, 0, 4).ClickAsync();
        await page.Keyboard.TypeAsync("x");
        await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);
        await page.Keyboard.TypeAsync("1");
        var parts = Grid(page, Cells).Locator("[data-bz-cell-editor=date] input");
        await Assertions.Expect(parts.First).ToHaveValueAsync("1");
        Assert.All(await parts.EvaluateAllAsync<string[]>("els => els.slice(1).map(e => e.value)"), Assert.Empty);
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Cell(page, Cells, 0, 4)).ToHaveTextAsync("1 Dec 2026");
    }

    [E2EFact]
    public async Task An_invalid_value_keeps_the_editor_and_escape_restores()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, Cells, 0, 2).ClickAsync();
        await page.Keyboard.TypeAsync("0");
        await page.Keyboard.PressAsync("Enter");

        var error = Grid(page, Cells).Locator(".bz-table-cell-error");
        await Assertions.Expect(error).ToHaveTextAsync("Price must be above zero.");
        await Assertions.Expect(Field(page, Cells)).ToBeFocusedAsync();
        // The message is tied to the editor for screen readers.
        var errorId = await error.GetAttributeAsync("id");
        await Assertions.Expect(Field(page, Cells)).ToHaveAttributeAsync("aria-describedby", errorId!);

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Cell(page, Cells, 0, 2)).ToHaveTextAsync("$129.00");
        await Assertions.Expect(Cell(page, Cells, 0, 2)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task A_double_click_opens_a_pick_list_on_its_option_and_one_Escape_cancels()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var category = Cell(page, Cells, 0, 1);

        await category.DblClickAsync();
        await Assertions.Expect(FocusedOption(page)).ToHaveTextAsync("Hardware");
        await Assertions.Expect(Grid(page, Cells).Locator("[data-bz-cell-editor=select] [aria-expanded=true]")).ToHaveCountAsync(1);

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("[role=listbox]:visible")).ToHaveCountAsync(0);
        await Assertions.Expect(category).ToBeFocusedAsync();
        await Assertions.Expect(category).ToHaveTextAsync("Hardware");
    }

    [E2EFact]
    public async Task Typing_over_a_pick_list_opens_it_with_type_ahead_and_Tab_picks_and_moves_on()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, Cells, 2, 1).ClickAsync();
        await page.Keyboard.TypeAsync("so");
        await Assertions.Expect(FocusedOption(page)).ToHaveTextAsync("Software");

        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(Cell(page, Cells, 2, 1)).ToHaveTextAsync("Software");
        await Assertions.Expect(Cell(page, Cells, 2, 2)).ToBeFocusedAsync();

        // Enter on an option picks it, commits and stays on the cell.
        await Cell(page, Cells, 3, 1).ClickAsync();
        await page.Keyboard.PressAsync("F2");
        await Assertions.Expect(FocusedOption(page)).ToHaveTextAsync("Hardware");
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Cell(page, Cells, 3, 1)).ToHaveTextAsync("Software");
        await Assertions.Expect(Cell(page, Cells, 3, 1)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task Alt_ArrowDown_and_Space_open_the_list_and_the_mark_opens_it_by_pointer()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var category = Cell(page, Cells, 1, 1);

        foreach (var key in new[] { "Alt+ArrowDown", "Space" })
        {
            await category.ClickAsync();
            await page.Keyboard.PressAsync(key);
            await Assertions.Expect(FocusedOption(page)).ToHaveTextAsync("Software");
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);
        }

        // The chevron shows on hover; a press on it opens the list.
        var other = Cell(page, Cells, 2, 1);
        await other.HoverAsync();
        var mark = other.Locator("[data-bz-pick=select]");
        await Assertions.Expect(mark).ToHaveCSSAsync("opacity", "1");
        await mark.ClickAsync();
        await Assertions.Expect(FocusedOption(page)).ToHaveTextAsync("Service");
        await page.Keyboard.PressAsync("Escape");
    }

    [E2EFact]
    public async Task A_date_opens_its_calendar_on_a_double_click_and_a_picked_day_commits()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var restock = Cell(page, Cells, 0, 4);
        var calendar = page.Locator("[data-slot=popover-content]:visible");

        await restock.DblClickAsync();
        await Assertions.Expect(calendar).ToHaveCountAsync(1);
        // The focus lands on the current day, so the arrows walk the days.
        await page.WaitForFunctionAsync("() => document.activeElement?.closest('[data-slot=popover-content]') != null");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(calendar).ToHaveCountAsync(0);
        await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);
        await Assertions.Expect(restock).ToBeFocusedAsync();

        await restock.DblClickAsync();
        await calendar.GetByRole(AriaRole.Button, new() { Name = "15", Exact = false }).First.ClickAsync();
        await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);
        await Assertions.Expect(restock).ToHaveTextAsync("15 Dec 2026");
        await Assertions.Expect(restock).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task A_double_click_on_a_time_focuses_the_part_under_the_pointer()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var (x, y) = await TextPointAsync(Cell(page, Cells, 0, 5), 0.85);
        await page.Mouse.DblClickAsync(x, y);
        await Assertions.Expect(Grid(page, Cells).Locator("[data-bz-cell-editor=time] input[aria-label=Minute]")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");

        (x, y) = await TextPointAsync(Cell(page, Cells, 0, 5), 0.1);
        await page.Mouse.DblClickAsync(x, y);
        await Assertions.Expect(Grid(page, Cells).Locator("[data-bz-cell-editor=time] input[aria-label=Hour]")).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task A_tap_selects_and_a_tap_on_the_selected_cell_edits_it()
    {
        var context = await fx.Browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            HasTouch = true,
            ReducedMotion = ReducedMotion.Reduce,
        });
        await using var _ = context;
        await context.AddInitScriptAsync("localStorage.setItem('blaizio-style', 'ember');");
        var page = await OpenAsync(context);
        var name = Cell(page, Cells, 3, 0);

        await name.TapAsync();
        await Assertions.Expect(name).ToBeFocusedAsync();
        await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);
        await name.TapAsync();
        await Assertions.Expect(Grid(page, Cells).Locator("[data-bz-cell-editor=text]")).ToHaveCountAsync(1);
    }

    [E2EFact]
    public async Task Clicking_another_cell_commits_and_puts_the_cursor_there()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, Cells, 5, 0).DblClickAsync();
        await Assertions.Expect(Field(page, Cells)).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.TypeAsync(" XL");

        await Cell(page, Cells, 0, 3).ClickAsync();

        await Assertions.Expect(Cell(page, Cells, 5, 0)).ToHaveTextAsync("Monitor arm XL");
        await Assertions.Expect(Cell(page, Cells, 0, 3)).ToBeFocusedAsync();
        await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);
    }

    // Whether a cell shows the cursor (data-active) and the width of its ring, from its box shadow.
    private static Task<string> RingAsync(ILocator cell) => cell.EvaluateAsync<string>(@"td =>
        (td.hasAttribute('data-active') ? 'active' : 'idle') + ' ' + (/0px 0px 0px 2px inset/.test(getComputedStyle(td).boxShadow) ? '2px' : 'none')");

    // The ring fades in over a few frames (the style transitions its shadow): poll for it.
    private static async Task ExpectRingAsync(ILocator cell, string expected)
    {
        var ring = await RingAsync(cell);
        for (var i = 0; i < 40 && ring != expected; i++)
        {
            await Task.Delay(50);
            ring = await RingAsync(cell);
        }
        Assert.Equal(expected, ring);
    }

    [E2EFact]
    public async Task A_click_draws_no_ring_and_the_next_key_shows_it()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        // Cell mode: the clicked cell takes the focus and no ring; a key brings the 2px ring.
        await Cell(page, Cells, 0, 0).ClickAsync();
        await Assertions.Expect(Cell(page, Cells, 0, 0)).ToBeFocusedAsync();
        await ExpectRingAsync(Cell(page, Cells, 0, 0), "idle none");
        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(Cell(page, Cells, 0, 1)).ToBeFocusedAsync();
        await ExpectRingAsync(Cell(page, Cells, 0, 1), "active 2px");
        // The next press hides it again.
        await Cell(page, Cells, 2, 1).ClickAsync();
        await Assertions.Expect(Cell(page, Cells, 2, 1)).ToBeFocusedAsync();
        await ExpectRingAsync(Cell(page, Cells, 2, 1), "idle none");

        // Row mode the same: a click shows only the row's hover tint.
        await Cell(page, Rows, 1, 0).ClickAsync();
        await Assertions.Expect(Cell(page, Rows, 1, 0)).ToBeFocusedAsync();
        await ExpectRingAsync(Cell(page, Rows, 1, 0), "idle none");
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(Cell(page, Rows, 2, 0)).ToBeFocusedAsync();
        await ExpectRingAsync(Cell(page, Rows, 2, 0), "active 2px");
    }

    [E2EFact]
    public async Task EditOnClick_edits_with_one_click_and_a_click_on_another_cell_opens_that_one()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        await page.Locator("#table-edit-on-click").ClickAsync();
        await Assertions.Expect(page.Locator("#table-edit-on-click")).ToHaveAttributeAsync("aria-checked", "true");

        // One click halfway along "Design license": the editor, caret inside the text.
        var name = Cell(page, Cells, 1, 0);
        var (x, y) = await TextPointAsync(name, 0.5);
        await page.Mouse.ClickAsync(x, y);
        var field = Field(page, Cells);
        await Assertions.Expect(field).ToBeFocusedAsync();
        var caret = await field.EvaluateAsync<int[]>("i => [i.selectionStart, i.value.length]");
        Assert.InRange(caret[0], 1, caret[1] - 1);
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.TypeAsync(" Pro");

        // A click on Stock commits Name and opens Stock's editor.
        await Cell(page, Cells, 1, 3).ClickAsync();
        await Assertions.Expect(name).ToHaveTextAsync("Design license Pro");
        await Assertions.Expect(Cell(page, Cells, 1, 3).Locator("[data-bz-cell-editor=number] input")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);

        // A pick list opens its list with one click.
        await Cell(page, Cells, 2, 1).ClickAsync();
        await Assertions.Expect(FocusedOption(page)).ToHaveTextAsync("Service");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Editors(page, Cells)).ToHaveCountAsync(0);
    }

    // The editing row's action button by its name (the built-in column's icon buttons are named by their labels).
    private static ILocator ActionButton(IPage page, string grid, int row, string name) =>
        Grid(page, grid).Locator($"tbody tr[data-bz-row-index=\"{row}\"]").GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    // The editing row's buttons sit inside their cell and the table's scroll box: the column kept room
    // for them (a set that grew once the row began editing would spill over the next cell or out of view).
    private static async Task AssertInCellAsync(params ILocator[] buttons)
    {
        foreach (var button in buttons)
        {
            var gaps = await button.EvaluateAsync<double[]>(@"b => {
                const r = b.getBoundingClientRect(), c = b.closest('td').getBoundingClientRect(),
                      box = b.closest('[data-slot=table-container]').getBoundingClientRect();
                return [r.left - c.left, c.right - r.right, r.left - box.left, box.right - r.right]; }");
            Assert.All(gaps, gap => Assert.True(gap >= -0.5, $"A button spills by {-gap:0.#}px."));
        }
    }

    // Tab until the named button holds the focus (the row's fields come first), at most limit presses.
    private static async Task TabToAsync(IPage page, string name, int limit = 10)
    {
        for (var i = 0; i < limit; i++)
        {
            var label = await page.EvaluateAsync<string>("() => document.activeElement?.getAttribute('aria-label') ?? document.activeElement?.textContent?.trim() ?? ''");
            if (label == name) return;
            await page.Keyboard.PressAsync("Tab");
        }
        Assert.Fail($"Tab never reached {name}.");
    }

    [E2EFact]
    public async Task The_edit_actions_column_saves_and_cancels_by_mouse_and_keyboard()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var name = Cell(page, Actions, 0, 0);
        await Grid(page, Actions).ScrollIntoViewIfNeededAsync();

        // Mouse: Edit, change the name, Cancel puts it back without saving first.
        await ActionButton(page, Actions, 0, "Edit").ClickAsync();
        var field = Grid(page, Actions).Locator("tbody tr[data-bz-row-index=\"0\"] [data-bz-cell-editor=text] input");
        await Assertions.Expect(field).ToBeFocusedAsync();
        await AssertInCellAsync(ActionButton(page, Actions, 0, "Save"), ActionButton(page, Actions, 0, "Cancel"));
        await field.FillAsync("Changed");
        await ActionButton(page, Actions, 0, "Cancel").ClickAsync();
        await Assertions.Expect(Editors(page, Actions)).ToHaveCountAsync(0);
        await Assertions.Expect(name).ToHaveTextAsync("Amara Okafor");
        await Assertions.Expect(page.GetByText("Press Edit on a row, change its fields, then Save or Cancel.")).ToBeVisibleAsync();

        // Mouse: Edit, change, Save.
        await ActionButton(page, Actions, 0, "Edit").ClickAsync();
        await Assertions.Expect(field).ToBeFocusedAsync();
        await field.FillAsync("Amara O.");
        await ActionButton(page, Actions, 0, "Save").ClickAsync();
        await Assertions.Expect(Editors(page, Actions)).ToHaveCountAsync(0);
        await Assertions.Expect(name).ToHaveTextAsync("Amara O.");

        // Keyboard: Enter opens the row; Tab past its last field reaches Save, then Cancel.
        await Cell(page, Actions, 1, 0).ClickAsync();
        await page.Keyboard.PressAsync("Enter");
        var second = Grid(page, Actions).Locator("tbody tr[data-bz-row-index=\"1\"] [data-bz-cell-editor=text] input");
        await Assertions.Expect(second).ToBeFocusedAsync();
        await AssertInCellAsync(ActionButton(page, Actions, 1, "Save"), ActionButton(page, Actions, 1, "Cancel"));
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("Lucas F.");
        await TabToAsync(page, "Save");
        await Assertions.Expect(ActionButton(page, Actions, 1, "Save")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(ActionButton(page, Actions, 1, "Cancel")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Editors(page, Actions)).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, Actions, 1, 0)).ToHaveTextAsync("Lucas Ferreira");

        // Enter opens it again (the cursor waits on the row); Tab to Save, Enter saves.
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(second).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("Lucas F.");
        await TabToAsync(page, "Save");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Editors(page, Actions)).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, Actions, 1, 0)).ToHaveTextAsync("Lucas F.");
    }

    [E2EFact]
    public async Task Your_own_actions_column_opens_a_field_from_code_and_saves_and_cancels()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        await Grid(page, OwnActions).ScrollIntoViewIfNeededAsync();
        ILocator Amount(int row) => Grid(page, OwnActions).Locator($"tbody tr[data-bz-row-index=\"{row}\"] > td").Nth(3);

        // BeginEditAsync(row, "Amount"): the row opens with the Amount field holding the focus.
        await ActionButton(page, OwnActions, 0, "Edit amount").ClickAsync();
        var field = Amount(0).Locator("[data-bz-cell-editor=number] input");
        await Assertions.Expect(field).ToBeFocusedAsync();
        await AssertInCellAsync(ActionButton(page, OwnActions, 0, "Save"), ActionButton(page, OwnActions, 0, "Cancel"));
        await field.FillAsync("99");
        await ActionButton(page, OwnActions, 0, "Cancel").ClickAsync();
        await Assertions.Expect(Editors(page, OwnActions)).ToHaveCountAsync(0);
        await Assertions.Expect(Amount(0)).ToContainTextAsync("86.40");

        await ActionButton(page, OwnActions, 1, "Edit amount").ClickAsync();
        var second = Amount(1).Locator("[data-bz-cell-editor=number] input");
        await Assertions.Expect(second).ToBeFocusedAsync();
        await second.FillAsync("150");
        await ActionButton(page, OwnActions, 1, "Save").ClickAsync();
        await Assertions.Expect(Editors(page, OwnActions)).ToHaveCountAsync(0);
        await Assertions.Expect(Amount(1)).ToContainTextAsync("150.00");

        // Keyboard: Tab from the Amount field (the row's last) reaches Save, then Cancel.
        await ActionButton(page, OwnActions, 2, "Edit amount").ClickAsync();
        var third = Amount(2).Locator("[data-bz-cell-editor=number] input");
        await Assertions.Expect(third).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("20");
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(ActionButton(page, OwnActions, 2, "Save")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(ActionButton(page, OwnActions, 2, "Cancel")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Shift+Tab");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Editors(page, OwnActions)).ToHaveCountAsync(0);
        await Assertions.Expect(Amount(2)).ToContainTextAsync("20.00");

        // Started from the keyboard (the widths lock before the row's buttons change): still inside.
        await Grid(page, OwnActions).Locator("tbody tr[data-bz-row-index=\"3\"] > td").First.ClickAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Editors(page, OwnActions).First).ToBeVisibleAsync();
        await AssertInCellAsync(ActionButton(page, OwnActions, 3, "Save"), ActionButton(page, OwnActions, 3, "Cancel"));
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Editors(page, OwnActions)).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task An_edge_to_edge_cell_is_filled_by_its_template()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var cell = Grid(page, Custom).Locator("tbody tr[data-bz-row-index=\"1\"] > td").Nth(3);
        await cell.ScrollIntoViewIfNeededAsync();

        // No padding, and the template's root (tinted: 14 open issues) covers the cell.
        var fit = await cell.EvaluateAsync<double[]>(@"td => {
            const root = td.firstElementChild.firstElementChild;
            const t = td.getBoundingClientRect(), r = root.getBoundingClientRect();
            return [parseFloat(getComputedStyle(td).paddingLeft), t.width - r.width, t.height - r.height]; }");
        Assert.Equal(0, fit[0]);
        Assert.InRange(fit[1], 0, 2);
        Assert.InRange(fit[2], 0, 2);
    }

    [E2EFact]
    public async Task A_rejected_row_stays_open_with_the_typed_value_and_its_message()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, Rows, 0, 0).ClickAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Grid(page, Rows).Locator("tr[data-editing] [data-bz-cell-editor]")).ToHaveCountAsync(5);

        // Tab walks the row's fields, a date's parts included, into Check-out (the fourth cell).
        var checkOut = Grid(page, Rows).Locator("tr[data-editing] td").Nth(3).Locator("[data-bz-cell-editor]");
        for (var i = 0; i < 12 && !await checkOut.EvaluateAsync<bool>("e => e.contains(document.activeElement)"); i++)
            await page.Keyboard.PressAsync("Tab");
        // Before the check-in (12 Nov): refused by the demo's OnEdit.
        await page.Keyboard.TypeAsync("11102026");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(Grid(page, Rows).Locator(".bz-table-cell-error")).ToHaveTextAsync("Check-out must be after check-in.");
        await Assertions.Expect(checkOut.Locator("input[aria-label=Day]")).ToHaveValueAsync("10");
        Assert.True(await checkOut.EvaluateAsync<bool>("e => e.contains(document.activeElement)"));

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Grid(page, Rows).Locator("tr[data-editing]")).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, Rows, 0, 3)).ToHaveTextAsync("15 Nov");
    }

    [E2EFact]
    public async Task A_double_click_in_row_mode_opens_the_row_ready_on_its_cell()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, Rows, 1, 1).DblClickAsync();
        await Assertions.Expect(Grid(page, Rows).Locator("tr[data-editing]")).ToHaveCountAsync(1);
        await Assertions.Expect(FocusedOption(page)).ToHaveCountAsync(1);
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Grid(page, Rows).Locator("tr[data-editing]")).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task Enter_on_a_row_opens_the_dialog_and_save_applies_the_change()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, Dialog, 0, 1).ClickAsync();
        await page.Keyboard.PressAsync("Enter");
        var dialog = DocsServerFixture.VisibleDialog(page);
        await Assertions.Expect(dialog.GetByLabel("Notes")).ToBeVisibleAsync(); // a hidden column, still a field
        await DocsServerFixture.WaitForDialogFocusAsync(page);
        // The form opens on the field of the column the cursor was on, and the row is marked.
        await Assertions.Expect(dialog.GetByLabel("Email")).ToBeFocusedAsync();
        await Assertions.Expect(Grid(page, Dialog).Locator("tbody tr[data-editing]")).ToHaveAttributeAsync("data-bz-row-index", "0");

        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("amara@example.org");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("[role=dialog]:visible")).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, Dialog, 0, 1)).ToHaveTextAsync("amara@example.org");
        await Assertions.Expect(Cell(page, Dialog, 0, 1)).ToBeFocusedAsync();
        await Assertions.Expect(Grid(page, Dialog).Locator("tbody tr[data-editing]")).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task Closing_a_changed_form_asks_first_and_a_clean_one_closes_at_once()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var email = Cell(page, Dialog, 1, 1);
        var dialog = DocsServerFixture.VisibleDialog(page);

        await email.DblClickAsync();
        await DocsServerFixture.WaitForDialogFocusAsync(page);
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator("[role=dialog]:visible")).ToHaveCountAsync(0);

        await email.DblClickAsync();
        await DocsServerFixture.WaitForDialogFocusAsync(page);
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.TypeAsync("x");
        await page.Keyboard.PressAsync("Escape");

        // The footer asks, in place, with Keep editing focused; no second dialog.
        await Assertions.Expect(dialog.GetByText("Discard your changes?")).ToBeVisibleAsync();
        await Assertions.Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Keep editing" })).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("[role=dialog]:visible, [role=alertdialog]:visible")).ToHaveCountAsync(1);

        // A second Escape answers Keep editing: back in the field, the change kept.
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog.GetByText("Discard your changes?")).ToHaveCountAsync(0);
        await Assertions.Expect(dialog.GetByLabel("Email")).ToBeFocusedAsync();
        await Assertions.Expect(dialog.GetByLabel("Email")).ToHaveValueAsync("lucas.ferreira@example.comx");

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Discard" }).ClickAsync();
        await Assertions.Expect(page.Locator("[role=dialog]:visible")).ToHaveCountAsync(0);
        await Assertions.Expect(email).ToHaveTextAsync("lucas.ferreira@example.com");
        await Assertions.Expect(email).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task Your_own_dialog_saves_through_its_context()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, OwnDialog, 0, 0).ClickAsync();
        await page.Keyboard.PressAsync("Enter");
        var dialog = DocsServerFixture.VisibleDialog(page);
        await Assertions.Expect(dialog.GetByLabel("Estimate")).ToBeVisibleAsync();
        await DocsServerFixture.WaitForDialogFocusAsync(page);

        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("Fix the login redirect");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save ticket" }).ClickAsync();

        await Assertions.Expect(page.Locator("[role=dialog]:visible")).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, OwnDialog, 0, 0)).ToHaveTextAsync("Fix the login redirect");
    }

    [E2ETheory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task The_drawer_opens_at_the_inline_end_edge_and_walks_the_rows(string dir)
    {
        await using var context = await fx.NewContextAsync(dir: dir);
        var page = await OpenAsync(context);

        await Cell(page, Drawer, 0, 3).ClickAsync();
        await page.Keyboard.PressAsync("Enter");
        var sheet = DocsServerFixture.VisibleDialog(page);
        await DocsServerFixture.WaitForDialogFocusAsync(page);

        // Right edge in a left-to-right page, left edge in a right-to-left one (a reserved
        // scrollbar gutter may sit between the sheet and the window's edge).
        var gap = await sheet.EvaluateAsync<double[]>(
            "s => { const r = s.getBoundingClientRect(); return [r.left, window.innerWidth - r.right]; }");
        var (near, far) = dir == "ltr" ? (gap[1], gap[0]) : (gap[0], gap[1]);
        Assert.InRange(near, -1, 20);
        Assert.True(far > 400, $"The sheet should sit at the {(dir == "ltr" ? "right" : "left")} edge: {gap[0]} / {gap[1]}");

        // On the Seats field, the row marked; Previous off on the first row.
        await Assertions.Expect(sheet.GetByLabel("Seats")).ToBeFocusedAsync();
        await Assertions.Expect(Grid(page, Drawer).Locator("tbody tr[data-editing]")).ToHaveAttributeAsync("data-bz-row-index", "0");
        await Assertions.Expect(sheet.GetByRole(AriaRole.Button, new() { Name = "Previous row" })).ToBeDisabledAsync();

        await sheet.GetByRole(AriaRole.Button, new() { Name = "Next row" }).ClickAsync();
        await Assertions.Expect(sheet.GetByLabel("Company")).ToHaveValueAsync("Blue Harbor");
        await Assertions.Expect(Grid(page, Drawer).Locator("tbody tr[data-editing]")).ToHaveAttributeAsync("data-bz-row-index", "1");

        // Closed: the focus returns to the row it showed last, in the column it opened from.
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator("[role=dialog]:visible")).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, Drawer, 1, 3)).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task The_drawer_has_no_serious_axe_violations()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, Drawer, 1, 0).DblClickAsync();
        var sheet = DocsServerFixture.VisibleDialog(page);
        await DocsServerFixture.WaitForDialogFocusAsync(page);
        await Assertions.Expect(sheet.GetByLabel("Company")).ToHaveValueAsync("Blue Harbor");

        AxeResult results = await page.RunAxe(new AxeRunContext { Include = [new AxeSelector("[role=dialog]")] });
        var blocking = results.Violations.Where(v => v.Impact is "serious" or "critical")
            .Select(v => $"{v.Id}: {string.Join(", ", v.Nodes.Select(n => n.Target?.ToString()))}").ToList();
        Assert.True(blocking.Count == 0, string.Join('\n', blocking));
    }

    [E2EFact]
    public async Task Ctrl_end_reaches_the_last_of_ten_thousand_virtualized_rows()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await Cell(page, Large, 0, 1).ClickAsync();
        await page.Keyboard.PressAsync("Control+End");

        await Assertions.Expect(Cell(page, Large, 9999, 4)).ToBeFocusedAsync();
        await Assertions.Expect(Grid(page, Large)).ToHaveAttributeAsync("aria-rowcount", "10001");

        await page.Keyboard.PressAsync("Control+Home");
        await Assertions.Expect(Cell(page, Large, 0, 0)).ToBeFocusedAsync();
    }
}
