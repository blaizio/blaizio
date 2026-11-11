using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// Behaviour that lives in the scripts and that bUnit cannot see: where a click lands once the
/// carousel has seen the press, a drawer dragged with a finger, and the calendar's arrow keys
/// after paging.
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
        await using var context = await fx.Browser.NewContextAsync(new()
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
}
