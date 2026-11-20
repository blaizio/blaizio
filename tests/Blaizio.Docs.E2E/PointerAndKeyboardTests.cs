using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// Behaviour that lives in the scripts and that bUnit cannot see: where a click lands once the
/// carousel has seen the press, a drawer dragged with a finger, the calendar's arrow keys after
/// paging, a menubar tapped on a touch screen, a context menu reopened while it closes, a sheet
/// removed while it is open, and two slider thumbs on the same value.
/// </summary>
[Collection("docs-e2e")]
public sealed class PointerAndKeyboardTests(DocsServerFixture fx)
{
    private static Task ScriptLoadedAsync(IPage page, string module) =>
        page.WaitForFunctionAsync(
            $"() => performance.getEntriesByType('resource').some(e => e.name.includes('/dist/{module}.'))");

    [E2EFact]
    public async Task Carousel_click_inside_a_slide_reaches_its_target_and_a_drag_does_not_click()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/carousel");
        await ScriptLoadedAsync(page, "carousel");
        await page.WaitForTimeoutAsync(400); // the instance is created right after the import

        // No docs example puts a control inside a slide, so one is put there. What is under test is
        // where the browser sends the click once the carousel's script has seen the press: a
        // captured pointer sends it to the viewport, and the control never hears it.
        await page.EvaluateAsync(
            """
            () => {
                const item = document.querySelector('[data-slot=carousel-item]');
                const button = document.createElement('button');
                button.type = 'button';
                button.id = 'slide-probe';
                button.textContent = 'probe';
                button.style.cssText = 'position:relative;z-index:5;padding:8px 16px';
                window.__probeClicks = 0;
                button.addEventListener('click', () => window.__probeClicks++);
                item.appendChild(button);
            }
            """);
        var probe = page.Locator("#slide-probe");
        await probe.ScrollIntoViewIfNeededAsync();
        var box = (await probe.BoundingBoxAsync())!;
        var x = box.X + box.Width / 2;
        var y = box.Y + box.Height / 2;

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.UpAsync();
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__probeClicks"));

        // A real drag still moves the slides, and the release at its end is not a click.
        var viewport = page.Locator("[data-slot=carousel-content]").First;
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        for (var step = 1; step <= 10; step++) await page.Mouse.MoveAsync(x - step * 22, y);
        Assert.NotEqual(0, await viewport.EvaluateAsync<double>("el => Math.abs(el.scrollLeft)"));
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__probeClicks"));
    }

    [E2EFact]
    public async Task Drawer_swiped_with_a_finger_follows_it_and_closes()
    {
        // A touch screen: the browser pans by itself unless the drawer refuses the move, and then
        // cancels the pointer after the first one.
        await using var context = await fx.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 412, Height = 900 },
            HasTouch = true,
            IsMobile = true,
        });
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/drawer");

        var trigger = page.Locator("main button[aria-haspopup=dialog]").First;
        await trigger.ScrollIntoViewIfNeededAsync();
        await trigger.TapAsync();
        var drawer = DocsServerFixture.VisibleDialog(page);
        await Assertions.Expect(drawer).ToBeVisibleAsync();
        await ScriptLoadedAsync(page, "drawerDrag");
        await page.WaitForTimeoutAsync(900); // the open animation, then the drag wired behind the import

        var box = (await drawer.BoundingBoxAsync())!;
        var x = box.X + box.Width / 2;
        var top = box.Y + 24;
        var distance = Math.Min(box.Height * 0.8, 500);
        var session = await context.NewCDPSessionAsync(page);

        Dictionary<string, object> Touch(string type, double? y) => new()
        {
            ["type"] = type,
            ["touchPoints"] = y is { } at
                ? new[] { new Dictionary<string, object> { ["x"] = x, ["y"] = at, ["id"] = 1 } }
                : Array.Empty<Dictionary<string, object>>(),
        };

        await session.SendAsync("Input.dispatchTouchEvent", Touch("touchStart", top));
        for (var step = 1; step <= 24; step++)
        {
            await session.SendAsync("Input.dispatchTouchEvent", Touch("touchMove", top + distance * step / 24));
            await page.WaitForTimeoutAsync(16);
        }
        await session.SendAsync("Input.dispatchTouchEvent", Touch("touchEnd", null));

        await Assertions.Expect(drawer).Not.ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task Calendar_arrow_keys_stay_in_the_month_that_was_paged_to()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/calendar");
        await ScriptLoadedAsync(page, "calendar");

        var calendar = page.Locator("[data-slot=calendar]").First;
        var caption = calendar.Locator("[data-slot=calendar-caption-label]").First;
        await calendar.Locator("[data-slot=calendar-day-button]:not([aria-disabled=true])").Nth(9).ClickAsync();

        var next = calendar.Locator("[data-slot=calendar-button-next]");
        await next.ClickAsync();
        await next.ClickAsync();
        var month = (await caption.InnerTextAsync()).Trim();

        // Tab lands on the grid's one tab stop; the arrow then moves within the month on screen.
        // It used to start from the day clicked before paging and bring that month back.
        var stop = calendar.Locator("[data-slot=calendar-day-button][tabindex='0']");
        await stop.FocusAsync();
        var from = await stop.GetAttributeAsync("aria-label");
        await page.Keyboard.PressAsync("ArrowRight");

        // Wait for focus to have moved to another day before reading the month.
        await page.WaitForFunctionAsync(
            "from => document.activeElement?.getAttribute('data-slot') === 'calendar-day-button' && document.activeElement.getAttribute('aria-label') !== from",
            from);
        Assert.Equal(month, (await caption.InnerTextAsync()).Trim());
    }

    [E2EFact]
    public async Task Menubar_tap_on_another_trigger_switches_menus_on_a_touch_screen()
    {
        await using var context = await fx.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 900, Height = 900 },
            HasTouch = true,
        });
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/menubar");

        var triggers = page.Locator("main [data-bz-menubar]").First.Locator("[role=menuitem][data-bz-roving-item]");
        await triggers.Nth(0).ScrollIntoViewIfNeededAsync();
        await triggers.Nth(0).TapAsync();
        await Assertions.Expect(triggers.Nth(0)).ToHaveAttributeAsync("aria-expanded", "true");

        // A finger's tap is a pointerenter and then a click: the enter switched menus and the
        // click toggled the new one shut, so the whole bar closed.
        await triggers.Nth(1).TapAsync();

        await Assertions.Expect(triggers.Nth(1)).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(triggers.Nth(0)).ToHaveAttributeAsync("aria-expanded", "false");
    }

    [E2EFact]
    public async Task Context_menu_reopened_while_closing_takes_the_keyboard_and_drops_its_submenu()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/context-menu");

        var region = page.Locator("main [data-slot=context-menu-trigger]").First;
        await region.ScrollIntoViewIfNeededAsync();
        // The page reports that scroll with its next frame, and a context menu closes on any
        // scroll of the page. Opened before the report came, the menu was gone again by the time
        // the pointer reached it, about one run in six. Two frames on, the report is past.
        await page.EvaluateAsync(
            "() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done)))");
        var box = (await region.BoundingBoxAsync())!;
        await page.Mouse.ClickAsync(box.X + 60, box.Y + 40, new() { Button = MouseButton.Right });

        var subTrigger = page.Locator("[role=menu] [role=menuitem][aria-haspopup=menu]").First;
        await subTrigger.HoverAsync();
        await Assertions.Expect(subTrigger).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(page.Locator("[role=menu]")).ToHaveCountAsync(2);

        // A second right-click elsewhere closes the menu and opens it again in one go.
        await page.Mouse.ClickAsync(box.X + box.Width - 60, box.Y + box.Height - 30, new() { Button = MouseButton.Right });

        // One menu, visible; the submenu stayed closed and says so; and the menu has the keyboard.
        await Assertions.Expect(page.Locator("[role=menu]")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[role=menu]").First).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[role=menu] [role=menuitem][aria-haspopup=menu]").First)
            .ToHaveAttributeAsync("aria-expanded", "false");
        await page.WaitForFunctionAsync("() => document.activeElement?.closest('[role=menu]') != null");

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator("[role=menu]")).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task Mobile_sidebar_sheet_leaves_no_copy_when_the_window_grows_past_the_breakpoint()
    {
        await using var context = await fx.NewContextAsync(viewport: new ViewportSize { Width = 600, Height = 900 });
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/sidebar");

        await page.Locator("[data-slot=sidebar-trigger]:visible").First.ClickAsync();
        await Assertions.Expect(page.Locator("[role=dialog][data-slot=sidebar]")).ToHaveCountAsync(1);

        // The sheet's component goes away while its element sits in the page-level layer. The
        // layer used to put the removed element back where it was declared, hidden, for good.
        await page.SetViewportSizeAsync(1300, 900);

        await Assertions.Expect(page.Locator("[role=dialog][data-slot=sidebar]")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("[data-slot=sheet-overlay]")).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task Slider_thumbs_on_the_same_value_can_be_pulled_apart()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/slider");
        await ScriptLoadedAsync(page, "slider");

        // The first slider on the page that has two thumbs.
        var slider = page.Locator("main [data-slot=slider]")
            .Filter(new() { Has = page.Locator("[role=slider] >> nth=1") }).First;
        await slider.ScrollIntoViewIfNeededAsync();
        var thumbs = slider.Locator("[role=slider]");
        await Assertions.Expect(thumbs).ToHaveCountAsync(2);

        await thumbs.Nth(0).FocusAsync();
        await page.Keyboard.PressAsync("End"); // the lower thumb runs up against the upper one
        var upper = await thumbs.Nth(1).GetAttributeAsync("aria-valuenow");
        await Assertions.Expect(thumbs.Nth(0)).ToHaveAttributeAsync("aria-valuenow", upper!);

        // Dragging the pair toward the start moves the thumb that can go that way: the lower one.
        var box = (await thumbs.Nth(1).BoundingBoxAsync())!;
        var x = box.X + box.Width / 2;
        var y = box.Y + box.Height / 2;
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        for (var step = 1; step <= 8; step++) await page.Mouse.MoveAsync(x - step * 10, y);
        await page.Mouse.UpAsync();

        await Assertions.Expect(thumbs.Nth(1)).ToHaveAttributeAsync("aria-valuenow", upper!);
        await Assertions.Expect(thumbs.Nth(0)).Not.ToHaveAttributeAsync("aria-valuenow", upper!);
    }

    [E2ETheory]
    [InlineData("docs/components/input-date", "Day")]
    [InlineData("docs/components/input-time", "Hour")]
    public async Task Date_and_time_segments_do_not_keep_a_letter_typed_into_them(string route, string label)
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, route);

        var segment = page.Locator($"input[aria-label={label}]:not([disabled])").First;
        await segment.ScrollIntoViewIfNeededAsync();
        await segment.FocusAsync(); // selects what the segment holds
        await page.Keyboard.PressAsync("Delete");
        await Assertions.Expect(segment).ToHaveValueAsync("");

        // The segment's part stays empty, so the same empty text is rendered again. Written as a
        // plain value, nothing was sent to the box and the letter stayed in it.
        await page.Keyboard.TypeAsync("x");
        await Assertions.Expect(segment).ToHaveValueAsync("");

        // Digits still go in.
        await page.Keyboard.TypeAsync("1");
        await Assertions.Expect(segment).ToHaveValueAsync("1");
    }
}
