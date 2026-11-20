using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// The life of an overlay surface in a real page, at the points where bUnit has no say: a surface
/// opened again while its exit animation plays (the same element, and the keyboard focus back
/// inside it), and a surface whose page goes away under it (open, or on its way out). Dialog,
/// Sheet, Popover, Select, Combobox, the menus, Tooltip and Hover Card share one lifecycle
/// (PresenceSurfaceBase), so each case runs over all of them.
/// </summary>
[Collection("docs-e2e")]
public sealed class OverlayLifecycleTests(DocsServerFixture fx)
{
    // The fixture's contexts ask for reduced motion, which leaves no exit animation to be inside
    // of. These cases need the animations.
    private async Task<IBrowserContext> NewAnimatedContextAsync()
    {
        var context = await fx.Browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            ColorScheme = ColorScheme.Light,
            ReducedMotion = ReducedMotion.NoPreference,
        });
        await context.AddInitScriptAsync(
            "localStorage.setItem('blaizio-style', 'ember');" +
            "localStorage.setItem('blaizio-dir', 'ltr');" +
            "localStorage.setItem('blaizio-theme', 'light');");
        return context;
    }

    // An exit animation long enough that the next step is certain to land inside it.
    private const string SlowExit = "[data-state=closed] { animation-duration: 1200ms !important; }";

    // And one with room for two presses and a look at the page in between.
    private const string VerySlowExit = "[data-state=closed] { animation-duration: 4000ms !important; }";

    /// <summary>The page, once Blazor answers clicks: the "On This Page" rail renders only then.</summary>
    private static async Task<(IPage Page, List<string> Errors)> OpenAsync(IBrowserContext context, string route)
    {
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) =>
        {
            if (message.Type == "error") errors.Add(message.Text);
        };
        await DocsServerFixture.OpenAsync(page, route);
        await page.Locator("[data-slot=toc-link]").First.WaitForAsync(new() { Timeout = 30_000 });
        return (page, errors);
    }

    private static async Task OpenSurfaceAsync(IPage page, string triggerSelector, string surfaceSelector, string gesture)
    {
        var trigger = page.Locator($"main {triggerSelector}").First;
        await trigger.ScrollIntoViewIfNeededAsync();
        await page.WaitForTimeoutAsync(200); // a scroll still under way dismisses a context menu
        switch (gesture)
        {
            case "hover":
                await trigger.HoverAsync();
                break;
            case "context":
                var box = (await trigger.BoundingBoxAsync())!;
                await page.Mouse.ClickAsync(box.X + 60, box.Y + 40, new() { Button = MouseButton.Right });
                break;
            default:
                await trigger.ClickAsync();
                break;
        }

        await Assertions.Expect(page.Locator(surfaceSelector)).ToHaveAttributeAsync("data-state", "open");
    }

    // What a surface can leave in a page it is no longer part of. Empty when there is nothing.
    private static Task<string> LeftoversAsync(IPage page, string surfaceSelector) =>
        page.EvaluateAsync<string>(
            """
            selector => {
                const found = [];
                const surfaces = document.querySelectorAll(selector).length;
                if (surfaces) found.push(`${surfaces} surface(s) still in the page`);
                for (const el of document.body.children) {
                    if (el.hasAttribute('data-state') || el.hasAttribute('data-bz-portal') || el.hasAttribute('data-bz-portal-frame'))
                        found.push(`<${el.tagName.toLowerCase()} data-slot="${el.dataset.slot ?? ''}"> left in the body`);
                }
                const walker = document.createTreeWalker(document, NodeFilter.SHOW_COMMENT);
                let placeholders = 0;
                while (walker.nextNode()) if (walker.currentNode.data === 'bz-portal') placeholders++;
                if (placeholders) found.push(`${placeholders} portal placeholder(s)`);
                if (document.body.hasAttribute('data-scroll-locked') || document.body.style.overflow === 'hidden')
                    found.push('the page is still scroll locked');
                const errorUi = document.getElementById('blazor-error-ui');
                if (errorUi && getComputedStyle(errorUi).display !== 'none') found.push('the error bar is showing');
                return found.join('; ');
            }
            """,
            surfaceSelector);

    private static async Task LeaveThePageAsync(IPage page)
    {
        // In-app navigation: the page's components are disposed, the document stays.
        await page.EvaluateAsync("() => window.Blazor.navigateTo('/docs/components/button')");
        var heading = page.Locator("h1", new() { HasTextString = "Button" }).First;
        await heading.WaitForAsync(new() { Timeout = 30_000 });
        // The pointer has not moved: on the new page it may rest on a control that shows a
        // tooltip of its own, which is not a leftover. Park it on the heading. A surface of the
        // old page has nothing left that would close it, so it would still be there afterwards.
        await heading.HoverAsync();
        await page.WaitForTimeoutAsync(600); // releases that follow the disposal, and that tooltip's exit
    }

    [E2ETheory]
    [InlineData("docs/components/dialog", "[data-slot=dialog-trigger]", "[data-slot=dialog-content]")]
    [InlineData("docs/components/sheet", "[data-slot=sheet-trigger]", "[data-slot=sheet-content]")]
    [InlineData("docs/components/popover", "[data-slot=popover-trigger]", "[data-slot=popover-content]")]
    [InlineData("docs/components/select", "[data-slot=select-trigger]", "[data-slot=select-content]:not([hidden])")]
    [InlineData("docs/components/dropdown-menu", "[data-slot=dropdown-menu-trigger]", "[data-slot=dropdown-menu-content]")]
    [InlineData("docs/components/menubar", "[data-slot=menubar-trigger]", "[data-slot=menubar-content]")]
    public async Task A_surface_opened_again_while_it_closes_is_the_same_surface_and_still_closes(
        string route, string triggerSelector, string surfaceSelector)
    {
        await using var context = await NewAnimatedContextAsync();
        var (page, errors) = await OpenAsync(context, route);
        var trigger = page.Locator($"main {triggerSelector}").First;
        var surface = page.Locator(surfaceSelector);

        await OpenSurfaceAsync(page, triggerSelector, surfaceSelector, "click");
        // Escape reaches a list or a menu through its own key handler, so focus has to be inside.
        await page.WaitForFunctionAsync("s => document.querySelector(s)?.contains(document.activeElement)", surfaceSelector);
        var slow = await page.AddStyleTagAsync(new() { Content = SlowExit });
        await surface.EvaluateAsync("el => el.setAttribute('data-first-opening', '')");

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(surface).ToHaveAttributeAsync("data-state", "closed");
        // Not a pointer press: a closing dialog's backdrop is still over the page.
        await trigger.EvaluateAsync("el => el.click()");
        await Assertions.Expect(surface).ToHaveAttributeAsync("data-state", "open");

        // Past the point where the close that was called off would have ended: the surface is
        // still there, it is the element from before (not a new one), and it can be seen.
        await page.WaitForTimeoutAsync(1500);
        await Assertions.Expect(surface).ToHaveCountAsync(1);
        await Assertions.Expect(surface).ToHaveAttributeAsync("data-state", "open");
        await Assertions.Expect(surface).ToHaveAttributeAsync("data-first-opening", "");
        await Assertions.Expect(surface).ToBeVisibleAsync();

        await slow.EvaluateAsync("el => el.remove()");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(surface).ToHaveCountAsync(0);
        Assert.Equal("", await LeftoversAsync(page, surfaceSelector));
        Assert.Empty(errors);
    }

    // ---- focus: a press took it out, the reopening brings it back ----

    // A point of the window a press can land on: the backdrop of a dialog, or a spot of the page
    // that is no control and no part of a surface.
    private static async Task<(float X, float Y)> PointAsync(IPage page, string kind)
    {
        var point = await page.EvaluateAsync<float[]>(
            """
            kind => {
                const backdrop = el => el.matches('[data-slot$=-overlay]');
                const quiet = el => !el.closest(
                    'a, button, input, textarea, select, label, summary, [tabindex], [role], ' +
                    '[contenteditable], [data-state]');
                for (let y = 30; y < innerHeight - 10; y += 30) {
                    for (let x = 10; x < innerWidth - 10; x += 40) {
                        const el = document.elementFromPoint(x, y);
                        if (el && (kind === 'backdrop' ? backdrop(el) : quiet(el))) return [x, y];
                    }
                }
                return [];
            }
            """,
            kind);
        Assert.Equal(2, point.Length);
        return (point[0], point[1]);
    }

    // A press of the mouse where the trigger is, whatever lies over it at that moment.
    private static async Task PressAsync(IPage page, ILocator trigger)
    {
        var box = (await trigger.BoundingBoxAsync())!;
        await page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    // The close is a press that takes the focus out of the surface: on the trigger (focus goes to
    // the trigger), on the page (focus goes to the page), on a dialog's backdrop (the same). The
    // reopening is a press on the trigger where the trigger can be reached, and a click made from
    // code where it lies under the backdrop (an app that opens its dialog from a shortcut).
    [E2ETheory]
    [InlineData("docs/components/select", "[data-slot=select-trigger]", "[data-slot=select-content]:not([hidden])", "trigger", "press")]
    [InlineData("docs/components/select", "[data-slot=select-trigger]", "[data-slot=select-content]:not([hidden])", "page", "code")]
    [InlineData("docs/components/popover", "[data-slot=popover-trigger]", "[data-slot=popover-content]", "trigger", "press")]
    [InlineData("docs/components/popover", "[data-slot=popover-trigger]", "[data-slot=popover-content]", "page", "code")]
    [InlineData("docs/components/dialog", "[data-slot=dialog-trigger]", "[data-slot=dialog-content]", "backdrop", "code")]
    [InlineData("docs/components/sheet", "[data-slot=sheet-trigger]", "[data-slot=sheet-content]", "backdrop", "code")]
    [InlineData("docs/components/dropdown-menu", "[data-slot=dropdown-menu-trigger]", "[data-slot=dropdown-menu-content]", "trigger", "press")]
    [InlineData("docs/components/menubar", "[data-slot=menubar-trigger]", "[data-slot=menubar-content]", "trigger", "press")]
    public async Task A_surface_opened_again_while_it_closes_has_the_focus_inside_and_answers_Escape(
        string route, string triggerSelector, string surfaceSelector, string closeWithAPressOn, string openAgainWith)
    {
        await using var context = await NewAnimatedContextAsync();
        var (page, errors) = await OpenAsync(context, route);
        var trigger = page.Locator($"main {triggerSelector}").First;
        var surface = page.Locator(surfaceSelector);

        await OpenSurfaceAsync(page, triggerSelector, surfaceSelector, "click");
        await page.WaitForFunctionAsync(
            "s => document.querySelector(s)?.contains(document.activeElement)", surfaceSelector);
        await page.WaitForTimeoutAsync(400); // the entry animation, and the focus placed for good
        // Where a first opening puts the focus: the selected option, the first field, the
        // surface itself.
        await page.EvaluateAsync("() => document.activeElement.setAttribute('data-opening-focus', '')");
        var slow = await page.AddStyleTagAsync(new() { Content = VerySlowExit });
        await surface.EvaluateAsync("el => el.setAttribute('data-first-opening', '')");

        if (closeWithAPressOn == "trigger")
        {
            await PressAsync(page, trigger);
        }
        else
        {
            var (x, y) = await PointAsync(page, closeWithAPressOn);
            await page.Mouse.ClickAsync(x, y);
        }

        await Assertions.Expect(surface).ToHaveAttributeAsync("data-state", "closed");
        // What the case is about: the press took the focus out of the closing surface.
        Assert.False(await page.EvaluateAsync<bool>(
            "s => document.querySelector(s).contains(document.activeElement)", surfaceSelector));

        if (openAgainWith == "press") await PressAsync(page, trigger);
        else await trigger.EvaluateAsync("el => el.click()");
        await Assertions.Expect(surface).ToHaveAttributeAsync("data-state", "open");
        // The element from before, not a new one.
        await Assertions.Expect(surface).ToHaveAttributeAsync("data-first-opening", "");

        // The focus is back where the first opening had put it. It used to stay on the trigger or
        // on the page: Tab started from behind the open surface, and an open select list took no
        // key at all, Escape included.
        await page.WaitForFunctionAsync(
            "() => document.activeElement?.hasAttribute('data-opening-focus')", null, new() { Timeout = 5_000 });

        await slow.EvaluateAsync("el => el.remove()");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(surface).ToHaveCountAsync(0);
        Assert.Equal("", await LeftoversAsync(page, surfaceSelector));
        Assert.Empty(errors);
    }

    [E2EFact]
    public async Task A_focus_scope_asked_again_places_the_focus_as_its_first_opening_would()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/button");

        // The built module over markup made for the test, with a stand-in for the .NET reference
        // that answers the question the scope asks before it places the focus.
        var seen = await page.EvaluateAsync<string[]>(
            """
            async () => {
                const { createFocusScope, refocus } =
                    await import(new URL('_content/blaizio.base/dist/focusScope.js', document.baseURI).href);
                const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
                const host = document.createElement('div');
                host.innerHTML = `
                    <button id="outside">outside</button>
                    <div id="scope" tabindex="-1">
                        <button id="first">first</button><button id="second">second</button>
                    </div>
                    <div id="bare" tabindex="-1"><p>nothing to tab to</p></div>
                    <div id="other" tabindex="-1"><button id="inner">inner</button></div>`;
                document.body.append(host);
                const $ = id => document.getElementById(id);
                const where = () =>
                    document.activeElement === document.body ? 'page' : document.activeElement.id;
                const net = {
                    asked: 0,
                    prevent: false,
                    invokeMethodAsync() { this.asked++; return Promise.resolve(this.prevent); },
                };
                const silent = { invokeMethodAsync: () => Promise.resolve(false) };
                const options = {
                    loop: false, trapped: false, hasOpenAutoFocus: true, hasCloseAutoFocus: false,
                };
                const plain = { ...options, hasOpenAutoFocus: false };
                const seen = [];
                const again = async id => { refocus($(id)); await sleep(80); };

                $('outside').focus();
                const scope = createFocusScope($('scope'), net, options);
                await sleep(200);
                seen.push(`first opening: ${where()}, asked ${net.asked}`);

                $('outside').focus(); // a press on the trigger
                await again('scope');
                seen.push(`focus on the trigger: ${where()}, asked ${net.asked}`);

                $('second').focus();
                await again('scope');
                seen.push(`focus inside already: ${where()}, asked ${net.asked}`);

                net.prevent = true;
                $('outside').focus();
                await again('scope');
                seen.push(`prevented by the handler: ${where()}, asked ${net.asked}`);
                net.prevent = false;

                const bare = createFocusScope($('bare'), silent, plain);
                await sleep(200);
                seen.push(`nothing to tab to, first opening: ${where()}`);
                document.activeElement.blur(); // a press on the page
                await again('bare');
                seen.push(`nothing to tab to, focus on the page: ${where()}`);

                $('outside').focus();
                await again('scope'); // under the scope that was created after it
                seen.push(`under another scope: ${where()}`);
                $('outside').focus();
                await bare.dispose();

                const passive = createFocusScope($('other'), silent, { ...plain, passive: true });
                await again('other');
                seen.push(`passive scope: ${where()}`);
                await passive.dispose();
                await again('other');
                seen.push(`no scope on the element: ${where()}`);

                await scope.dispose();
                host.remove();
                return seen;
            }
            """);

        Assert.Equal(
            [
                "first opening: first, asked 1",
                "focus on the trigger: first, asked 2",
                "focus inside already: second, asked 2",
                "prevented by the handler: outside, asked 3",
                "nothing to tab to, first opening: bare",
                "nothing to tab to, focus on the page: bare",
                "under another scope: outside",
                "passive scope: outside",
                "no scope on the element: outside",
            ],
            seen);
    }

    [E2ETheory]
    [InlineData("docs/components/dialog", "[data-slot=dialog-trigger]", "[data-slot=dialog-content]", "click")]
    [InlineData("docs/components/sheet", "[data-slot=sheet-trigger]", "[data-slot=sheet-content]", "click")]
    [InlineData("docs/components/popover", "[data-slot=popover-trigger]", "[data-slot=popover-content]", "click")]
    [InlineData("docs/components/select", "[data-slot=select-trigger]", "[data-slot=select-content]:not([hidden])", "click")]
    [InlineData("docs/components/combobox", "input[role=combobox]", "[data-slot=combobox-content]", "click")]
    [InlineData("docs/components/dropdown-menu", "[data-slot=dropdown-menu-trigger]", "[data-slot=dropdown-menu-content]", "click")]
    [InlineData("docs/components/menubar", "[data-slot=menubar-trigger]", "[data-slot=menubar-content]", "click")]
    [InlineData("docs/components/context-menu", "[data-slot=context-menu-trigger]", "[data-slot=context-menu-content]", "context")]
    [InlineData("docs/components/tooltip", "[data-slot=tooltip-trigger]", "[data-slot=tooltip-content]", "hover")]
    [InlineData("docs/components/hover-card", "[data-slot=hover-card-trigger]", "[data-slot=hover-card-content]", "hover")]
    public async Task A_surface_left_open_when_its_page_goes_away_leaves_nothing_behind(
        string route, string triggerSelector, string surfaceSelector, string gesture)
    {
        await using var context = await fx.NewContextAsync();
        var (page, errors) = await OpenAsync(context, route);

        await OpenSurfaceAsync(page, triggerSelector, surfaceSelector, gesture);
        await LeaveThePageAsync(page);

        // The surface sat in the document body, outside the page that was taken away.
        Assert.Equal("", await LeftoversAsync(page, surfaceSelector));
        Assert.Empty(errors);
    }

    [E2ETheory]
    [InlineData("docs/components/dialog", "[data-slot=dialog-trigger]", "[data-slot=dialog-content]", "click")]
    [InlineData("docs/components/popover", "[data-slot=popover-trigger]", "[data-slot=popover-content]", "click")]
    [InlineData("docs/components/select", "[data-slot=select-trigger]", "[data-slot=select-content]:not([hidden])", "click")]
    [InlineData("docs/components/combobox", "input[role=combobox]", "[data-slot=combobox-content]", "click")]
    [InlineData("docs/components/dropdown-menu", "[data-slot=dropdown-menu-trigger]", "[data-slot=dropdown-menu-content]", "click")]
    [InlineData("docs/components/context-menu", "[data-slot=context-menu-trigger]", "[data-slot=context-menu-content]", "context")]
    public async Task A_surface_whose_page_goes_away_while_it_closes_leaves_nothing_behind(
        string route, string triggerSelector, string surfaceSelector, string gesture)
    {
        await using var context = await NewAnimatedContextAsync();
        var (page, errors) = await OpenAsync(context, route);

        await OpenSurfaceAsync(page, triggerSelector, surfaceSelector, gesture);
        await page.WaitForFunctionAsync(
            "s => document.querySelector(s)?.contains(document.activeElement) || document.activeElement?.matches('input[role=combobox]')",
            surfaceSelector);
        await page.AddStyleTagAsync(new() { Content = SlowExit });
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator(surfaceSelector)).ToHaveAttributeAsync("data-state", "closed");

        await LeaveThePageAsync(page); // the script's report of the exit has nobody left to reach
        await page.WaitForTimeoutAsync(1300);

        Assert.Equal("", await LeftoversAsync(page, surfaceSelector));
        Assert.Empty(errors);
    }

    [E2EFact]
    public async Task Leaving_and_coming_back_with_a_surface_open_each_time_ends_with_a_clean_page()
    {
        await using var context = await fx.NewContextAsync();
        var (page, errors) = await OpenAsync(context, "docs/components/popover");

        (string Route, string Trigger, string Surface)[] stops =
        [
            ("docs/components/popover", "[data-slot=popover-trigger]", "[data-slot=popover-content]"),
            ("docs/components/select", "[data-slot=select-trigger]", "[data-slot=select-content]:not([hidden])"),
            ("docs/components/dialog", "[data-slot=dialog-trigger]", "[data-slot=dialog-content]"),
            ("docs/components/dropdown-menu", "[data-slot=dropdown-menu-trigger]", "[data-slot=dropdown-menu-content]"),
        ];
        for (var round = 0; round < 2; round++)
        {
            foreach (var (route, trigger, surface) in stops)
            {
                await page.EvaluateAsync("route => window.Blazor.navigateTo('/' + route)", route);
                await page.Locator($"main {trigger}").First.WaitForAsync(new() { Timeout = 30_000 });
                await OpenSurfaceAsync(page, trigger, surface, "click");
            }
        }

        await LeaveThePageAsync(page);

        Assert.Equal("", await LeftoversAsync(page, "[data-slot$=-content][data-state=open]"));
        Assert.Empty(errors);
    }

    // ---- data-side: what the surface says about where it is ----

    [E2EFact]
    public async Task A_popover_that_flipped_still_says_so_when_it_is_opened_again_while_it_closes()
    {
        await using var context = await NewAnimatedContextAsync();
        var (page, _) = await OpenAsync(context, "docs/components/popover");
        var trigger = page.Locator("main [data-slot=popover-trigger]").First;
        var surface = page.Locator("[data-slot=popover-content]");

        // With the trigger at the bottom edge of the window there is no room below: the popover,
        // asked for the bottom, goes on top.
        await trigger.EvaluateAsync(
            "el => window.scrollTo(0, el.getBoundingClientRect().bottom + window.scrollY - window.innerHeight + 12)");
        await page.WaitForTimeoutAsync(300);
        await trigger.ClickAsync();
        await Assertions.Expect(surface).ToHaveAttributeAsync("data-side", "top");
        await page.WaitForTimeoutAsync(500); // the entry animation

        await page.AddStyleTagAsync(new() { Content = SlowExit });
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(surface).ToHaveAttributeAsync("data-state", "closed");
        await trigger.EvaluateAsync("el => el.click()");
        await Assertions.Expect(surface).ToHaveAttributeAsync("data-state", "open");
        await page.WaitForTimeoutAsync(400); // any report from the positioning script would be in by now

        // It never moved, and the positioning script reports a side only when it changes: going
        // back to "bottom" here stayed wrong (the slide, the arrow) until the popover closed.
        var above = await page.EvaluateAsync<bool>(
            """
            () => {
                const surface = document.querySelector('[data-slot=popover-content]').getBoundingClientRect();
                const trigger = document.querySelector('main [data-slot=popover-trigger]').getBoundingClientRect();
                return surface.bottom <= trigger.top + 1;
            }
            """);
        Assert.True(above);
        await Assertions.Expect(surface).ToHaveAttributeAsync("data-side", "top");
    }

    // Every value data-side takes on a submenu surface, from the moment it enters the page.
    private const string WatchSubmenuSides =
        """
        () => {
            window.__submenuSides = [];
            const seen = el => window.__submenuSides.push(el.dataset.side);
            new MutationObserver(records => {
                for (const record of records) {
                    const nodes = record.type === 'attributes' ? [record.target] : [...record.addedNodes];
                    for (const node of nodes) {
                        if (!(node instanceof HTMLElement)) continue;
                        if (node.matches('[data-slot$=sub-content]')) seen(node);
                        else if (record.type === 'childList') node.querySelectorAll('[data-slot$=sub-content]').forEach(seen);
                    }
                }
            }).observe(document, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-side'] });
        }
        """;

    [E2ETheory]
    [InlineData("docs/components/dropdown-menu")]
    [InlineData("docs/components/context-menu")]
    public async Task A_submenu_says_from_its_first_frame_that_it_opens_to_the_side(string route)
    {
        await using var context = await fx.NewContextAsync();
        var (page, _) = await OpenAsync(context, route);
        await page.EvaluateAsync(WatchSubmenuSides);

        if (route.EndsWith("context-menu", StringComparison.Ordinal))
        {
            await OpenSurfaceAsync(page, "[data-slot=context-menu-trigger]", "[data-slot=context-menu-content]", "context");
        }
        else
        {
            // The example with a submenu.
            var share = page.Locator("main").GetByRole(AriaRole.Button, new() { Name = "Share", Exact = true }).First;
            await share.ScrollIntoViewIfNeededAsync();
            await share.ClickAsync();
        }

        var subTrigger = page.Locator("[role=menu] [role=menuitem][aria-haspopup=menu]").First;
        await subTrigger.HoverAsync();
        var submenu = page.Locator("[data-slot$=sub-content]").First;
        await Assertions.Expect(submenu).ToHaveAttributeAsync("data-state", "open");
        await Assertions.Expect(submenu).ToBeVisibleAsync();
        await page.WaitForTimeoutAsync(300);

        // It used to enter as "bottom" and turn to "right" once the script had placed it, which
        // restarted its entry slide from another direction.
        var sides = await page.EvaluateAsync<string[]>("() => window.__submenuSides");
        Assert.NotEmpty(sides);
        Assert.All(sides, side => Assert.Equal("right", side));
    }
}
