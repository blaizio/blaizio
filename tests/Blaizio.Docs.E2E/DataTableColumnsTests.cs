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

    // The script applies a key, a drag or a fit at once, but right after the page loads its main
    // thread can still be rendering the page's other demos, and the input waits for it: read the
    // width until it lands (or 5 s pass), then let the caller assert on it.
    private static async Task<double> WidthWhenAsync(ILocator element, Func<double, bool> landed)
    {
        var width = await WidthAsync(element);
        for (var i = 0; i < 50 && !landed(width); i++)
        {
            await Task.Delay(100);
            width = await WidthAsync(element);
        }
        return width;
    }

    private static Task<double> WidthNearAsync(ILocator element, double expected, double tolerance = 1.0) =>
        WidthWhenAsync(element, width => Math.Abs(width - expected) <= tolerance);

    // A width that held still across three reads 100ms apart (the column script re-lays out after a change).
    private static async Task<double> StableWidthAsync(ILocator element)
    {
        var width = await WidthAsync(element);
        for (int i = 0, still = 0; i < 30 && still < 2; i++)
        {
            await Task.Delay(100);
            var next = await WidthAsync(element);
            still = Math.Abs(next - width) < 0.5 ? still + 1 : 0;
            width = next;
        }
        return width;
    }

    private static Task<string> KeysAsync(ILocator table) =>
        table.EvaluateAsync<string>("t => [...t.tHead.rows[0].cells].map(c => c.dataset.bzColumn).filter(Boolean).join()");

    private static async Task<IPage> OpenAsync(IBrowserContext context)
    {
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/table");
        // Each table's column script marks it once attached, and each editable table's grid script
        // after it: by then the page has mostly finished rendering its demos.
        await page.WaitForFunctionAsync("() => document.querySelectorAll('table[data-bz-columns]').length >= 5");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('table[role=grid][data-bz-grid]').length >= 8");
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

    // Press an element's centre and move dx pixels sideways, leaving the button down.
    private static async Task PressAndMoveAsync(IPage page, ILocator from, float dx)
    {
        await from.ScrollIntoViewIfNeededAsync();
        var box = (await from.BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + dx, y, new() { Steps = 8 });
    }

    // A computed color the browser normalizes the same way as the ones it reports (--ring, --border).
    private static Task<string> ColorAsync(IPage page, string value) =>
        page.EvaluateAsync<string>(@"v => {
            const probe = document.createElement('div');
            probe.style.backgroundColor = v;
            document.body.append(probe);
            const color = getComputedStyle(probe).backgroundColor;
            probe.remove();
            return color;
        }", value);

    private static Task<string> LineColorAsync(ILocator head) =>
        head.Locator("[data-bz-resizer]").EvaluateAsync<string>("h => getComputedStyle(h, '::after').backgroundColor");

    // The landing line a label-and-line drag shows: its x (its centre), or null while it is hidden.
    private static Task<double?> LandingLineAsync(IPage page) =>
        page.EvaluateAsync<double?>(@"() => {
            const line = [...document.body.children].find(e => e.style?.position === 'fixed' && e.style.width === '2px');
            return line && line.style.display !== 'none' ? parseFloat(line.style.left) + 1 : null;
        }");

    // How far each data column's header and first body cell are shifted sideways, by key.
    private static Task<Dictionary<string, double[]>> ShiftsAsync(ILocator table) =>
        table.EvaluateAsync<Dictionary<string, double[]>>(@"t => {
            const row = t.querySelector('tbody tr[data-slot=table-row]');
            const shift = el => { const v = getComputedStyle(el).translate; return v === 'none' ? 0 : parseFloat(v); };
            return Object.fromEntries([...t.tHead.rows[0].cells].filter(c => c.dataset.bzColumn)
                .map(c => [c.dataset.bzColumn, [shift(c), shift(row.cells[c.cellIndex])]]));
        }");

    // Every column back in place, header and cells: polled, since on a busy page the frame that
    // drops the live move's sheet can land after the attribute check.
    private static async Task<Dictionary<string, double[]>> SettledShiftsAsync(ILocator table)
    {
        var shifts = await ShiftsAsync(table);
        for (var i = 0; i < 20 && shifts.Values.Any(s => s[0] != 0 || s[1] != 0); i++)
        {
            await Task.Delay(100);
            shifts = await ShiftsAsync(table);
        }
        return shifts;
    }

    [E2EFact]
    public async Task Hovering_or_focusing_a_resizable_header_lights_its_resize_line()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var table = Table(page, "Customer");
        var placed = Head(table, "Placed");
        await placed.ScrollIntoViewIfNeededAsync();
        var ring = await ColorAsync(page, "var(--ring)");
        var border = await ColorAsync(page, "var(--border)");
        await page.Mouse.MoveAsync(1, 1);

        // At rest a hairline in the border color.
        await page.WaitForFunctionAsync("([h, c]) => getComputedStyle(h, '::after').backgroundColor === c",
            new object[] { await placed.Locator("[data-bz-resizer]").ElementHandleAsync(), border });

        // Anywhere on the header: the ring color, still a hairline.
        var box = (await placed.BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(box.X + 12, box.Y + box.Height / 2);
        await page.WaitForFunctionAsync("([h, c]) => getComputedStyle(h, '::after').backgroundColor === c",
            new object[] { await placed.Locator("[data-bz-resizer]").ElementHandleAsync(), ring });
        Assert.Equal("1px", await placed.Locator("[data-bz-resizer]").EvaluateAsync<string>("h => getComputedStyle(h, '::after').width"));
        // The neighbours' lines stay at rest.
        Assert.Equal(border, await LineColorAsync(Head(table, "Status")));

        // On the handle itself: the strongest, two pixels wide.
        var handle = (await placed.Locator("[data-bz-resizer]").BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(handle.X + handle.Width / 2, handle.Y + handle.Height / 2);
        await Assertions.Expect(placed.Locator("[data-bz-resizer]")).ToHaveCSSAsync("cursor", "col-resize");
        await page.WaitForFunctionAsync("h => getComputedStyle(h, '::after').width === '2px'", await placed.Locator("[data-bz-resizer]").ElementHandleAsync());

        // The keyboard focus on the header's sort button (Alt+Shift+arrows resize from there).
        await page.Mouse.MoveAsync(1, 1);
        await Head(table, "Customer").Locator("button[data-bz-sort]").FocusAsync();
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(placed.Locator("button[data-bz-sort]")).ToBeFocusedAsync();
        await page.WaitForFunctionAsync("([h, c]) => getComputedStyle(h, '::after').backgroundColor === c",
            new object[] { await placed.Locator("[data-bz-resizer]").ElementHandleAsync(), ring });
    }

    [E2EFact]
    public async Task A_header_that_can_move_shows_the_grab_cursor()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var table = Table(page, "Estimate");
        await table.ScrollIntoViewIfNeededAsync();

        // Movable headers, their sort buttons included; # stays put, so no hand.
        await Assertions.Expect(Head(table, "Title")).ToHaveCSSAsync("cursor", "grab");
        await Assertions.Expect(Head(table, "Title").Locator("button[data-bz-sort]")).ToHaveCSSAsync("cursor", "grab");
        await Assertions.Expect(Head(table, "Estimate")).ToHaveCSSAsync("cursor", "grab");
        await Assertions.Expect(Head(table, "#")).Not.ToHaveCSSAsync("cursor", "grab");
        // A table that only resizes: no hand, and the handle keeps its own cursor.
        var sizing = Table(page, "Customer");
        await Assertions.Expect(Head(sizing, "Placed")).Not.ToHaveCSSAsync("cursor", "grab");
        await Assertions.Expect(Head(sizing, "Placed").Locator("[data-bz-resizer]")).ToHaveCSSAsync("cursor", "col-resize");

        // Dragging: the closed hand.
        await PressAndMoveAsync(page, Head(table, "Owner"), 12);
        await Assertions.Expect(Head(table, "Owner")).ToHaveCSSAsync("cursor", "grabbing");
        await Assertions.Expect(Head(table, "Owner").Locator("button[data-bz-sort]")).ToHaveCSSAsync("cursor", "grabbing");
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();
        await Assertions.Expect(Head(table, "Owner")).ToHaveCSSAsync("cursor", "grab");

        // A click still sorts.
        await Head(table, "Title").Locator("button[data-bz-sort]").ClickAsync();
        await Assertions.Expect(Head(table, "Title")).ToHaveAttributeAsync("aria-sort", "ascending");
    }

    [E2EFact]
    public async Task The_landing_line_marks_the_gap_the_same_way_for_every_column()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        // Left to right: Title is the first column that moves (# stays put), Owner one in the middle.
        var table = Table(page, "Estimate");
        foreach (var (key, next) in new[] { ("Title", "Owner"), ("Owner", "Due") })
        {
            var head = Head(table, key);
            // A few pixels: over its own place nothing would move, and no line shows.
            await PressAndMoveAsync(page, head, 8);
            Assert.Null(await LandingLineAsync(page));
            // Past the middle of the next column: the line sits after it.
            var nextBox = (await Head(table, next).BoundingBoxAsync())!;
            var headBox = (await head.BoundingBoxAsync())!;
            await page.Mouse.MoveAsync(nextBox.X + nextBox.Width * 0.75f, headBox.Y + headBox.Height / 2, new() { Steps = 6 });
            Assert.Equal(nextBox.X + nextBox.Width, (await LandingLineAsync(page))!.Value, 1.5);
            await page.Keyboard.PressAsync("Escape");
            await page.Mouse.UpAsync();
        }
        // Back past the middle of the column before it: the line sits before that one.
        var owner = Head(table, "Owner");
        var title = (await Head(table, "Title").BoundingBoxAsync())!;
        await PressAndMoveAsync(page, owner, -8);
        Assert.Null(await LandingLineAsync(page));
        await page.Mouse.MoveAsync(title.X + title.Width * 0.25f, title.Y + title.Height / 2, new() { Steps = 6 });
        Assert.Equal(title.X, (await LandingLineAsync(page))!.Value, 1.5);
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();
        Assert.Equal("#,Title,Owner,Due,Estimate", await KeysAsync(table));

        // Right to left (the same table, turned around): the next column sits to the left, and the
        // line on its left edge; the column before sits to the right.
        await table.EvaluateAsync("t => t.closest('[data-slot=data-table]').setAttribute('dir', 'rtl')");
        foreach (var (key, next) in new[] { ("Title", "Owner"), ("Owner", "Due") })
        {
            var head = Head(table, key);
            await PressAndMoveAsync(page, head, -8);
            Assert.Null(await LandingLineAsync(page));
            var nextBox = (await Head(table, next).BoundingBoxAsync())!;
            var headBox = (await head.BoundingBoxAsync())!;
            await page.Mouse.MoveAsync(nextBox.X + nextBox.Width * 0.25f, headBox.Y + headBox.Height / 2, new() { Steps = 6 });
            Assert.Equal(nextBox.X, (await LandingLineAsync(page))!.Value, 1.5);
            await page.Keyboard.PressAsync("Escape");
            await page.Mouse.UpAsync();
        }
        title = (await Head(table, "Title").BoundingBoxAsync())!;
        await PressAndMoveAsync(page, owner, 8);
        Assert.Null(await LandingLineAsync(page));
        await page.Mouse.MoveAsync(title.X + title.Width * 0.75f, title.Y + title.Height / 2, new() { Steps = 6 });
        Assert.Equal(title.X + title.Width, (await LandingLineAsync(page))!.Value, 1.5);
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();
        Assert.Equal("#,Title,Owner,Due,Estimate", await KeysAsync(table));
    }

    [E2EFact]
    public async Task A_live_move_slides_the_columns_while_dragging_and_reports_once_on_release()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var table = Table(page, "Estimate");
        await table.ScrollIntoViewIfNeededAsync();
        await page.Locator("#table-reorder-live").ClickAsync();
        await Assertions.Expect(page.Locator("#table-reorder-live")).ToHaveAttributeAsync("aria-checked", "true");
        // The switch's render reaches the column script after it lands.
        await page.WaitForTimeoutAsync(500);
        // Every change to the order the demo prints, counted.
        await Note(table).EvaluateAsync(@"note => {
            window.__orderChanges = 0;
            new MutationObserver(() => window.__orderChanges++).observe(note, { subtree: true, characterData: true, childList: true });
        }");

        var owner = (await Head(table, "Owner").BoundingBoxAsync())!;
        var title = (await Head(table, "Title").BoundingBoxAsync())!;
        await PressAndMoveAsync(page, Head(table, "Owner"), -title.Width * 0.7f);
        await page.WaitForTimeoutAsync(300);

        // Mid-drag: Title has slid over by Owner's width, header and cells alike; Owner follows the
        // pointer. No label, no line, and nothing reported yet.
        var shifts = await ShiftsAsync(table);
        Assert.Equal(owner.Width, shifts["Title"][0], 1.0);
        Assert.Equal(owner.Width, shifts["Title"][1], 1.0);
        Assert.Equal(-title.Width * 0.7, shifts["Owner"][0], 2.0);
        Assert.Equal(shifts["Owner"][0], shifts["Owner"][1], 0.5);
        Assert.Equal(0, shifts["Due"][0]);
        Assert.Null(await LandingLineAsync(page));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__orderChanges"));

        await page.Mouse.UpAsync();

        await Assertions.Expect(Note(table)).ToContainTextAsync("Order: #, Owner, Title, Due, Estimate");
        Assert.Equal("#,Owner,Title,Due,Estimate", await KeysAsync(table));
        await Assertions.Expect(table).Not.ToHaveAttributeAsync("data-bz-live-move", new System.Text.RegularExpressions.Regex(".*"));
        Assert.All((await SettledShiftsAsync(table)).Values, s => Assert.Equal([0, 0], s));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__orderChanges"));
        await Assertions.Expect(Head(table, "Owner")).ToHaveAttributeAsync("aria-sort", "none");

        // Escape mid-drag: everything slides back, and nothing is reported.
        await PressAndMoveAsync(page, Head(table, "Title"), 180);
        await page.WaitForTimeoutAsync(200);
        Assert.NotEqual(0, (await ShiftsAsync(table))["Title"][0]);
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();
        await Assertions.Expect(table).Not.ToHaveAttributeAsync("data-bz-live-move", new System.Text.RegularExpressions.Regex(".*"));
        Assert.All((await SettledShiftsAsync(table)).Values, s => Assert.Equal([0, 0], s));
        Assert.Equal("#,Owner,Title,Due,Estimate", await KeysAsync(table));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__orderChanges"));

        // Right to left (the same table, turned around): Title dragged right, back over Owner.
        await table.EvaluateAsync("t => t.closest('[data-slot=data-table]').setAttribute('dir', 'rtl')");
        var ownerWidth = await WidthAsync(Head(table, "Owner"));
        var titleWidth = await WidthAsync(Head(table, "Title"));
        await PressAndMoveAsync(page, Head(table, "Title"), (float)(ownerWidth * 0.7));
        await page.WaitForTimeoutAsync(300);
        shifts = await ShiftsAsync(table);
        Assert.Equal(-titleWidth, shifts["Owner"][1], 1.0);
        Assert.Equal(ownerWidth * 0.7, shifts["Title"][1], 2.0);
        await page.Mouse.UpAsync();
        await Assertions.Expect(Note(table)).ToContainTextAsync("Order: #, Title, Owner, Due, Estimate");
        Assert.Equal(2, await page.EvaluateAsync<int>("() => window.__orderChanges"));
    }

    [E2EFact]
    public async Task A_live_move_shifts_the_rows_the_virtualizer_adds_mid_drag()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);
        var table = Table(page, "SKU");
        await table.ScrollIntoViewIfNeededAsync();
        var sku = (await Head(table, "SKU").BoundingBoxAsync())!;
        var warehouse = (await Head(table, "Warehouse").BoundingBoxAsync())!;

        await PressAndMoveAsync(page, Head(table, "SKU"), warehouse.Width * 0.7f);
        // Rows far down render while the column is held.
        await table.EvaluateAsync("t => t.parentElement.scrollTop = 49 * 300");
        await page.WaitForFunctionAsync("t => !!t.querySelector('tbody tr[data-bz-row-index=\"300\"]')", await table.ElementHandleAsync());
        await page.WaitForTimeoutAsync(250);
        var shifted = await table.EvaluateAsync<double[]>(@"t => {
            const heads = [...t.tHead.rows[0].cells];
            const row = t.querySelector('tbody tr[data-bz-row-index=""300""]');
            const shift = el => { const v = getComputedStyle(el).translate; return v === 'none' ? 0 : parseFloat(v); };
            const at = key => heads.findIndex(c => c.dataset.bzColumn === key);
            return [shift(row.cells[at('Warehouse')]), shift(row.cells[at('SKU')]), shift(row.cells[at('Item')])];
        }");
        Assert.Equal(-sku.Width, shifted[0], 1.0);
        Assert.Equal(warehouse.Width * 0.7, shifted[1], 2.0);
        // The pinned Item column never moves.
        Assert.Equal(0, shifted[2]);

        await page.Mouse.UpAsync();
        await page.WaitForFunctionAsync("t => !t.hasAttribute('data-bz-live-move')", await table.ElementHandleAsync());
        Assert.StartsWith("#,Item,Warehouse,SKU,", await KeysAsync(table));
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
        Assert.Equal(188, Math.Round(await WidthNearAsync(Head(table, "Placed"), 188)));
        // The columns before the dragged one keep their widths.
        Assert.Equal(customer, await WidthAsync(Head(table, "Customer")), 1.0);

        // Escape mid-drag puts the width back, and nothing is reported.
        await DragAsync(page, Head(table, "Order").Locator("[data-bz-resizer]"), 40, () => page.Keyboard.PressAsync("Escape"));
        Assert.Equal(96, Math.Round(await WidthNearAsync(Head(table, "Order"), 96)));
        await Assertions.Expect(Note(table)).Not.ToContainTextAsync("Order");

        // A double-click on the edge fits the column to its content; Reset goes back.
        await Head(table, "Total").Locator("[data-bz-resizer]").DblClickAsync();
        await Assertions.Expect(Note(table)).ToContainTextAsync("Total");
        Assert.True(await WidthWhenAsync(Head(table, "Total"), width => width < 112) < 112);
        await page.GetByRole(AriaRole.Button, new() { Name = "Reset widths" }).ClickAsync();
        await Assertions.Expect(Note(table)).ToContainTextAsync("Drag an edge");
        Assert.Equal(128, Math.Round(await WidthNearAsync(Head(table, "Placed"), 128)));
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

        Assert.Equal(before + 30, await WidthNearAsync(status, before + 30), 1.0);
        // A run of presses is one report, once it pauses; the keys neither sort nor move the focus.
        await Assertions.Expect(Note(table)).ToContainTextAsync($"Status {before + 30:0}px");
        await Assertions.Expect(status).ToHaveAttributeAsync("aria-sort", "none");
        await Assertions.Expect(status.Locator("button[data-bz-sort]")).ToBeFocusedAsync();

        await page.Keyboard.PressAsync("Alt+Shift+Enter");
        await Assertions.Expect(Note(table)).Not.ToContainTextAsync($"Status {before + 30:0}px");
        Assert.True(await WidthWhenAsync(status, width => width < before + 30) < before + 30);
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
        Assert.Equal(before + 10, await WidthNearAsync(city, before + 10), 1.0);
        var team = Head(table, "team");
        // The far end of the row (the left, right to left), clear of the pinned columns. The widen
        // above shares the row out again a frame or two later on a busy page: measure once settled.
        await table.EvaluateAsync("t => t.parentElement.scrollLeft = -(t.parentElement.scrollWidth - t.parentElement.clientWidth)");
        var teamBefore = await StableWidthAsync(team);
        await DragAsync(page, team.Locator("[data-bz-resizer]"), -40);
        Assert.Equal(teamBefore + 40, await WidthNearAsync(team, teamBefore + 40, 1.5), 1.5);

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
