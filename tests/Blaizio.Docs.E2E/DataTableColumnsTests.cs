using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// BzDataTable's column features through a real browser: ts/dataTable.ts lays the widths out, keeps
/// pinned columns in place and runs every drag and key (bUnit cannot). Uses the Table page's column
/// demos, found by a header only each one has: sizing (Customer), pinning (Location), moving
/// (Estimate), the large editable table (SKU) and right to left (the email key).
/// </summary>
[Collection("docs-e2e")]
public sealed class DataTableColumnsTests(DocsServerFixture fx)
{
    private static ILocator Table(IPage page, string column) =>
        page.Locator("table[data-bz-columns]").Filter(new() { Has = page.Locator($"th[data-bz-column=\"{column}\"]") }).First;

    private static ILocator Head(ILocator table, string column) => table.Locator($"th[data-bz-column=\"{column}\"]");

    // What a demo prints under its table (the saved widths, the order).
    private static ILocator Note(ILocator table) =>
        table.Locator("xpath=ancestor::*[@data-slot='data-table'][1]/following-sibling::div[1]");

    private static Task<double> WidthAsync(ILocator element) =>
        element.EvaluateAsync<double>("e => e.getBoundingClientRect().width");

    private static Task<string> KeysAsync(ILocator table) =>
        table.EvaluateAsync<string>("t => [...t.tHead.rows[0].cells].map(c => c.dataset.bzColumn).filter(Boolean).join()");

    private static async Task<IPage> OpenAsync(IBrowserContext context)
    {
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/table");
        // Each table's column script marks it once attached.
        await page.WaitForFunctionAsync("() => document.querySelectorAll('table[data-bz-columns]').length >= 5");
        return page;
    }

    // Press the handle at an element's centre and drag it dx pixels sideways in steps.
    private static async Task DragAsync(IPage page, ILocator from, float dx, Func<Task>? beforeRelease = null)
    {
        await from.ScrollIntoViewIfNeededAsync();
        var box = (await from.BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + dx, y, new() { Steps = 8 });
        if (beforeRelease is not null) await beforeRelease();
        await page.Mouse.UpAsync();
    }

    [E2EFact]
    public async Task Dragging_a_header_edge_resizes_the_column_and_reports_the_width_once()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var table = Table(page, "Customer");

        // The declared widths, and the Fill column taking the rest of the row exactly.
        Assert.Equal(128, Math.Round(await WidthAsync(Head(table, "Placed"))));
        Assert.Equal(
            await table.EvaluateAsync<double>("t => t.parentElement.clientWidth"),
            await table.EvaluateAsync<double>("t => t.getBoundingClientRect().width"), 1.0);
        var customer = await WidthAsync(Head(table, "Customer"));

        await DragAsync(page, Head(table, "Placed").Locator("[data-bz-resizer]"), 60);

        await Assertions.Expect(Note(table)).ToContainTextAsync("Placed 188px");
        Assert.Equal(188, Math.Round(await WidthAsync(Head(table, "Placed"))));
        // The columns before the dragged one keep their widths.
        Assert.Equal(customer, await WidthAsync(Head(table, "Customer")), 1.0);

        // Escape mid-drag puts the width back, and nothing is reported.
        await DragAsync(page, Head(table, "Order").Locator("[data-bz-resizer]"), 40, () => page.Keyboard.PressAsync("Escape"));
        Assert.Equal(96, Math.Round(await WidthAsync(Head(table, "Order"))));
        await Assertions.Expect(Note(table)).Not.ToContainTextAsync("Order");

        // A double-click on the edge fits the column to its content; Reset goes back.
        await Head(table, "Total").Locator("[data-bz-resizer]").DblClickAsync();
        await Assertions.Expect(Note(table)).ToContainTextAsync("Total");
        Assert.True(await WidthAsync(Head(table, "Total")) < 112);
        await page.GetByRole(AriaRole.Button, new() { Name = "Reset widths" }).ClickAsync();
        await Assertions.Expect(Note(table)).ToContainTextAsync("Drag an edge");
        Assert.Equal(128, Math.Round(await WidthAsync(Head(table, "Placed"))));
    }

    [E2EFact]
    public async Task Alt_shift_arrows_resize_a_focused_header_and_alt_shift_enter_fits_it()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var table = Table(page, "Customer");
        var status = Head(table, "Status");
        var before = await WidthAsync(status);

        await status.Locator("button[data-bz-sort]").FocusAsync();
        for (var i = 0; i < 3; i++) await page.Keyboard.PressAsync("Alt+Shift+ArrowRight");

        Assert.Equal(before + 30, await WidthAsync(status), 1.0);
        // A run of presses is one report, once it pauses; the keys neither sort nor move the focus.
        await Assertions.Expect(Note(table)).ToContainTextAsync($"Status {before + 30:0}px");
        await Assertions.Expect(status).ToHaveAttributeAsync("aria-sort", "none");
        await Assertions.Expect(status.Locator("button[data-bz-sort]")).ToBeFocusedAsync();

        await page.Keyboard.PressAsync("Alt+Shift+Enter");
        await Assertions.Expect(Note(table)).Not.ToContainTextAsync($"Status {before + 30:0}px");
        Assert.True(await WidthAsync(status) < before + 30);
    }

    [E2EFact]
    public async Task Pinned_columns_stay_in_place_while_the_rows_scroll_sideways()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var table = Table(page, "Location");
        await table.ScrollIntoViewIfNeededAsync();
        const string Edges = @"t => {
            const row = t.tBodies[0].rows[2];
            return [row.cells[0].getBoundingClientRect().left, row.cells[1].getBoundingClientRect().left,
                    row.cells[row.cells.length - 1].getBoundingClientRect().right, t.tHead.rows[0].cells[2].getBoundingClientRect().left];
        }";
        var before = await table.EvaluateAsync<double[]>(Edges);

        await table.EvaluateAsync("t => t.parentElement.scrollLeft = 300");
        await page.WaitForTimeoutAsync(200);
        var after = await table.EvaluateAsync<double[]>(Edges);

        // The selection column and Name (start), the actions (end): in place. Email: scrolled under.
        Assert.Equal(before[0], after[0], 0.5);
        Assert.Equal(before[1], after[1], 0.5);
        Assert.Equal(before[2], after[2], 0.5);
        Assert.True(after[3] < before[3] - 250);
        // Name's inset is the selection column's width, measured by the script.
        Assert.Equal(await WidthAsync(table.Locator("thead th").First),
            await table.EvaluateAsync<double>("t => parseFloat(t.style.getPropertyValue('--bz-pin-s1'))"), 1.0);

        // The pinned cells sit over the cells scrolled under them: the point near Name's end edge
        // is Name's.
        var hit = await table.Locator("tbody tr").Nth(2).Locator("td").Nth(1).EvaluateAsync<string>(@"td => {
            const r = td.getBoundingClientRect();
            return document.elementFromPoint(r.right - 6, r.top + r.height / 2)?.closest('td')?.dataset.pinned ?? '';
        }");
        Assert.Equal("start", hit);
    }

    [E2EFact]
    public async Task Dragging_a_header_moves_its_column_and_a_plain_click_still_sorts()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var table = Table(page, "Estimate");
        Assert.Equal("#,Title,Owner,Due,Estimate", await KeysAsync(table));

        // Owner dropped before Title; the release is the drop's, not a sort.
        var title = (await Head(table, "Title").BoundingBoxAsync())!;
        var owner = (await Head(table, "Owner").BoundingBoxAsync())!;
        await DragAsync(page, Head(table, "Owner"), title.X + 10 - (owner.X + owner.Width / 2));
        await Assertions.Expect(Note(table)).ToContainTextAsync("Order: #, Owner, Title, Due, Estimate");
        Assert.Equal("#,Owner,Title,Due,Estimate", await KeysAsync(table));
        await Assertions.Expect(Head(table, "Owner")).ToHaveAttributeAsync("aria-sort", "none");

        // The # column stays put: nothing crosses it.
        await DragAsync(page, Head(table, "Owner"), -400);
        Assert.Equal("#,Owner,Title,Due,Estimate", await KeysAsync(table));

        // Escape mid-drag: no move and no sort.
        await DragAsync(page, Head(table, "Title"), 200, () => page.Keyboard.PressAsync("Escape"));
        Assert.Equal("#,Owner,Title,Due,Estimate", await KeysAsync(table));
        await Assertions.Expect(Head(table, "Title")).ToHaveAttributeAsync("aria-sort", "none");

        await Head(table, "Title").Locator("button[data-bz-sort]").ClickAsync();
        await Assertions.Expect(Head(table, "Title")).ToHaveAttributeAsync("aria-sort", "ascending");
    }

    [E2EFact]
    public async Task Ctrl_shift_arrows_move_the_focused_column_and_the_focus_goes_with_it()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var table = Table(page, "Estimate");

        await Head(table, "Due").Locator("button[data-bz-sort]").FocusAsync();
        await page.Keyboard.PressAsync("Control+Shift+ArrowRight");

        await Assertions.Expect(Note(table)).ToContainTextAsync("Order: #, Title, Owner, Estimate, Due");
        await Assertions.Expect(Head(table, "Due").Locator("button[data-bz-sort]")).ToBeFocusedAsync();

        // The last place: nowhere further to go. Back left twice.
        await page.Keyboard.PressAsync("Control+Shift+ArrowRight");
        await page.Keyboard.PressAsync("Control+Shift+ArrowLeft");
        await Assertions.Expect(Note(table)).ToContainTextAsync("Order: #, Title, Owner, Due, Estimate");
        await page.Keyboard.PressAsync("Control+Shift+ArrowLeft");
        await Assertions.Expect(Note(table)).ToContainTextAsync("Order: #, Title, Due, Owner, Estimate");
        await Assertions.Expect(Head(table, "Due").Locator("button[data-bz-sort]")).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task Right_to_left_pins_to_the_right_and_the_keys_follow_the_screen()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var table = Table(page, "email");
        await table.ScrollIntoViewIfNeededAsync();

        // The selection column and Name sit at the right edge, and stay there while scrolled.
        var right = await table.EvaluateAsync<double>("t => t.parentElement.getBoundingClientRect().right");
        var name = await table.EvaluateAsync<double>("t => t.tHead.rows[0].cells[1].getBoundingClientRect().right");
        Assert.Equal(right, await table.EvaluateAsync<double>("t => t.tHead.rows[0].cells[0].getBoundingClientRect().right"), 1.0);
        await table.EvaluateAsync("t => t.parentElement.scrollLeft = -150");
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(name, await table.EvaluateAsync<double>("t => t.tHead.rows[0].cells[1].getBoundingClientRect().right"), 0.5);
        await table.EvaluateAsync("t => t.parentElement.scrollLeft = 0");

        // Alt+Shift+Left widens (the end edge is on the left); dragging that edge left does too.
        var city = Head(table, "city");
        var before = await WidthAsync(city);
        await city.Locator("button[data-bz-sort]").FocusAsync();
        await page.Keyboard.PressAsync("Alt+Shift+ArrowLeft");
        Assert.Equal(before + 10, await WidthAsync(city), 1.0);
        var team = Head(table, "team");
        // The far end of the row (the left, right to left), clear of the pinned columns.
        await table.EvaluateAsync("t => t.parentElement.scrollLeft = -(t.parentElement.scrollWidth - t.parentElement.clientWidth)");
        var teamBefore = await WidthAsync(team);
        await DragAsync(page, team.Locator("[data-bz-resizer]"), -40);
        Assert.Equal(teamBefore + 40, await WidthAsync(team), 1.5);

        // Ctrl+Shift+Left moves a column left on screen: later in the order.
        await Head(table, "email").FocusAsync();
        await page.Keyboard.PressAsync("Control+Shift+ArrowLeft");
        await Assertions.Expect(Head(table, "email")).ToBeFocusedAsync();
        Assert.Equal("name,city,email,team,balance", await KeysAsync(table));
    }

    [E2EFact]
    public async Task A_virtualized_editable_table_keeps_its_widths_through_edits_and_scrolling()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var table = Table(page, "SKU");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('table[role=grid][data-bz-grid]').length >= 6");
        await table.ScrollIntoViewIfNeededAsync();
        const string Widths = "t => [...t.tHead.rows[0].cells].map(c => Math.round(c.getBoundingClientRect().width)).join()";
        var widths = await table.EvaluateAsync<string>(Widths);

        // Typing over the pinned Item cell edits it; the widths hold.
        var item = table.Locator("tbody tr[data-bz-row-index=\"0\"] > td").Nth(1);
        await item.ClickAsync();
        await page.Keyboard.TypeAsync("Dock");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(item).ToHaveTextAsync("Dock");
        Assert.Equal(widths, await table.EvaluateAsync<string>(Widths));

        // The cursor moving right is scrolled clear of the pinned columns.
        await table.Locator("tbody tr[data-bz-row-index=\"1\"] > td").Nth(1).ClickAsync();
        for (var i = 0; i < 5; i++) await page.Keyboard.PressAsync("ArrowRight");
        var discount = table.Locator("tbody tr[data-bz-row-index=\"1\"] > td").Nth(6);
        await Assertions.Expect(discount).ToBeFocusedAsync();
        Assert.True(await discount.EvaluateAsync<bool>(@"td => {
            const row = td.parentElement, r = td.getBoundingClientRect();
            return r.left >= row.cells[1].getBoundingClientRect().right - 1
                && r.right <= row.cells[row.cells.length - 1].getBoundingClientRect().left + 1;
        }"));

        // Rows the virtualizer renders far down take the same widths.
        await table.EvaluateAsync("t => t.parentElement.scrollTop = 49 * 400");
        await page.WaitForFunctionAsync("t => !!t.querySelector('tbody tr[data-bz-row-index=\"400\"]')", await table.ElementHandleAsync());
        Assert.Equal(widths, await table.EvaluateAsync<string>(
            "t => [...t.querySelector('tbody tr[data-bz-row-index=\"400\"]').cells].map(c => Math.round(c.getBoundingClientRect().width)).join()"));
    }
}
