using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// The script modules driven in a real page. Most run the built module over markup made for the
/// test, with a stand-in for the .NET reference that records what the script reports: timers,
/// observers, key defaults and form submission are the browser's, which bUnit does not have.
/// </summary>
[Collection("docs-e2e")]
public sealed partial class ScriptModuleTests(DocsServerFixture fx)
{
    // Helpers every page script below shares: the module import, a recording stand-in for the
    // .NET reference, a wait, and a wait for the page's own scrolling to be over.
    //
    // scrollsSettled: the docs page scrolls its sidebar to the current entry as it renders, and
    // the scroll event for that arrives a frame later, which on a slow machine is after the h1
    // the page is opened by. A test that listens for scrolls starts after it. The check runs in an
    // animation frame because a frame's scroll events are dispatched before its callbacks.
    private const string Prelude =
        """
        const load = name => import(new URL(`_content/blaizio.base/dist/${name}.js`, document.baseURI).href);
        const recorder = () => {
            const calls = [];
            return { calls, invokeMethodAsync: (name, ...args) => { calls.push([name, ...args]); return Promise.resolve(); } };
        };
        const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
        const scrollsSettled = () => new Promise(resolve => {
            const started = performance.now();
            let last = started;
            const note = () => { last = performance.now(); };
            document.addEventListener('scroll', note, true);
            const check = () => requestAnimationFrame(() => {
                const now = performance.now();
                if (now - last < 300 && now - started < 5000) { setTimeout(check, 50); return; }
                document.removeEventListener('scroll', note, true);
                resolve();
            });
            check();
        });
        """;

    private static Task<T> RunAsync<T>(IPage page, string body) =>
        page.EvaluateAsync<T>("async () => {\n" + Prelude + "\n" + body + "\n}");

    private async Task<IPage> OpenAsync(IBrowserContext context) =>
        await DocsServerFixture.OpenAsync(context, "docs/components/button");

    [E2EFact]
    public async Task Carousel_arrow_keys_stay_with_a_field_inside_a_slide()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/carousel");
        await page.WaitForFunctionAsync(
            "() => performance.getEntriesByType('resource').some(e => e.name.includes('/dist/carousel.'))");
        await page.WaitForTimeoutAsync(400); // the instance is created right after the import

        // No docs example puts a control with arrow keys of its own inside a slide, so two are
        // put there: a text field and a slider.
        await page.EvaluateAsync(
            """
            () => {
                const item = document.querySelector('[data-slot=carousel-item]');
                const field = document.createElement('input');
                field.id = 'slide-field';
                field.value = 'abc';
                field.style.cssText = 'position:relative;z-index:5;width:8rem';
                const slider = document.createElement('div');
                slider.id = 'slide-slider';
                slider.setAttribute('role', 'slider');
                slider.tabIndex = 0;
                slider.style.cssText = 'position:relative;z-index:5;width:8rem;height:1rem';
                // As a real slider does: the arrows are its own, so their default is prevented.
                slider.addEventListener('keydown', e => { if (e.key.startsWith('Arrow')) e.preventDefault(); });
                item.append(field, slider);
            }
            """);
        var viewport = page.Locator("[data-slot=carousel-content]").First;
        var field = page.Locator("#slide-field");
        await field.ScrollIntoViewIfNeededAsync();
        await field.FocusAsync();
        await field.EvaluateAsync("el => el.setSelectionRange(1, 1)");
        await viewport.EvaluateAsync(
            "el => { window.__slideScrolls = 0; el.addEventListener('scroll', () => window.__slideScrolls++); }");

        // In the field the arrow moves the caret and nothing else.
        await page.Keyboard.PressAsync("ArrowRight");
        await page.WaitForTimeoutAsync(700);
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__slideScrolls"));
        Assert.Equal(2, await field.EvaluateAsync<int>("el => el.selectionStart"));

        // On the slider the arrow is the slider's. It used to move the slide as well: the handler
        // was a Blazor one on the root, which sees neither where the key was pressed nor that its
        // default was already prevented.
        await page.Locator("#slide-slider").FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        await page.WaitForTimeoutAsync(700);
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__slideScrolls"));

        // From one of the carousel's own controls the same key moves the slide.
        await viewport.EvaluateAsync(
            """
            el => {
                const root = el.closest('[data-slot=carousel]');
                [...root.querySelectorAll('button:not([disabled])')].find(b => !el.contains(b)).focus();
            }
            """);
        await page.Keyboard.PressAsync("ArrowRight");
        await page.WaitForFunctionAsync("el => Math.abs(el.scrollLeft) > 10", await viewport.ElementHandleAsync(),
            new() { Timeout = 5_000 });
    }

    [E2EFact]
    public async Task Carousel_reports_slides_added_after_it_started()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var counts = await RunAsync<int[]>(page,
            """
            const { createCarousel } = await load('carousel');
            const host = document.createElement('div');
            const slide = '<div data-slot="carousel-item" style="flex:0 0 200px;height:40px"></div>';
            host.innerHTML = `<div style="display:flex;overflow:auto;width:200px">${slide.repeat(3)}</div>`;
            document.body.append(host);
            const viewport = host.firstElementChild;
            const ref = recorder();
            const carousel = createCarousel(viewport, ref, { horizontal: true, loop: false, autoplayMs: 0 }, host);
            const count = () => ref.calls.filter(c => c[0] === 'OnScrollState').at(-1)[4];

            // Past the report a resize observer makes when it starts watching: a slide added
            // before that one would be counted by it.
            await sleep(300);
            const before = count();
            // Neither the viewport's size nor its scroll position changes: only the child list does.
            viewport.insertAdjacentHTML('beforeend', slide);
            await sleep(300);
            const after = count();
            carousel.dispose();
            host.remove();
            return [before, after];
            """);

        Assert.Equal([3, 4], counts);
    }

    [E2EFact]
    public async Task Message_scroller_keeps_following_after_a_wheel_that_moved_nothing()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var distance = await RunAsync<double>(page,
            """
            const { createMessageScroller } = await load('messageScroller');
            const root = document.createElement('div');
            root.innerHTML =
                '<div data-slot="message-scroller-viewport" style="height:200px;overflow:auto">' +
                '<div data-slot="message-scroller-content"></div></div>';
            document.body.append(root);
            const viewport = root.firstElementChild, content = viewport.firstElementChild;
            const add = n => {
                for (let i = 0; i < n; i++)
                    content.insertAdjacentHTML('beforeend',
                        '<div data-slot="message-scroller-item" style="height:46px">message</div>');
            };
            add(2); // shorter than the viewport: a wheel-up here scrolls nothing
            const scroller = createMessageScroller(root, null);
            await sleep(100);

            viewport.dispatchEvent(new WheelEvent('wheel', { deltaY: -120, bubbles: true }));
            await sleep(100);
            add(20);
            await sleep(400);

            const left = viewport.scrollHeight - viewport.clientHeight - viewport.scrollTop;
            scroller.dispose();
            root.remove();
            return left;
            """);

        Assert.InRange(distance, 0, 2);
    }

    [E2EFact]
    public async Task Message_scroller_stays_at_the_end_when_its_viewport_gets_shorter()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var distance = await RunAsync<double>(page,
            """
            const { createMessageScroller } = await load('messageScroller');
            const root = document.createElement('div');
            root.innerHTML =
                '<div data-slot="message-scroller-viewport" style="height:200px;overflow:auto">' +
                '<div data-slot="message-scroller-content"></div></div>';
            document.body.append(root);
            const viewport = root.firstElementChild, content = viewport.firstElementChild;
            for (let i = 0; i < 30; i++)
                content.insertAdjacentHTML('beforeend',
                    '<div data-slot="message-scroller-item" style="height:46px">message</div>');
            const scroller = createMessageScroller(root, null);
            await sleep(200);

            // The composer grew, or an on-screen keyboard opened: same content, less room.
            viewport.style.height = '120px';
            await sleep(400);

            const left = viewport.scrollHeight - viewport.clientHeight - viewport.scrollTop;
            scroller.dispose();
            root.remove();
            return left;
            """);

        Assert.InRange(distance, 0, 2);
    }

    [E2EFact]
    public async Task Toast_updated_in_place_gets_its_full_time_again()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        // [was the toast still up at the update, ms from the update to its removal]
        var result = await RunAsync<double[]>(page,
            """
            const { createToaster } = await load('toast');
            const root = document.createElement('section');
            root.style.cssText = 'position:fixed;right:0;bottom:0';
            root.innerHTML =
                '<ol data-bz-toaster data-y-position="bottom" data-x-position="right" data-bz-visible-toasts="3">' +
                '<li data-bz-toast data-bz-toast-id="save" data-bz-toast-duration="3000" data-dismissible="true">Saving</li></ol>';
            document.body.append(root);
            const ref = recorder();
            const toaster = createToaster(root, ref);
            toaster.sync();
            const toast = root.querySelector('li');

            await sleep(1000);
            const upAtUpdate = ref.calls.length === 0 && !toast.hasAttribute('data-removed');
            // What the host renders when Show is called again with the same id: new content, the
            // same duration, the next revision.
            toast.textContent = 'Saved';
            toast.setAttribute('data-bz-toast-revision', '1');
            const updated = performance.now();
            toaster.sync();

            while (!toast.hasAttribute('data-removed')) await sleep(25);
            const lived = performance.now() - updated;
            toaster.dispose();
            root.remove();
            return [upAtUpdate ? 1 : 0, lived];
            """);

        Assert.Equal(1, result[0]);
        // The old countdown had 2000 ms left at the update. A timer never fires early, so the full
        // 3000 ms can only be measured longer, never shorter.
        Assert.True(result[1] >= 2900, $"the updated toast closed {result[1]:0} ms after the update");
    }

    [E2EFact]
    public async Task Combobox_enter_with_nothing_highlighted_does_not_submit_the_form()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createCombobox } = await load('combobox');
            const form = document.createElement('form');
            form.id = 'combo-form';
            form.innerHTML =
                '<input id="combo-input" />' +
                '<div id="combo-list" data-bz-combobox-list><div data-bz-combobox-item id="combo-one">One</div></div>';
            document.body.append(form);
            window.__submits = 0;
            window.__picks = 0;
            form.addEventListener('submit', e => { e.preventDefault(); window.__submits++; });
            form.querySelector('#combo-one').addEventListener('click', () => window.__picks++);
            window.__combo = createCombobox(form.querySelector('#combo-list'),
                { inputId: 'combo-input', loop: false, autoHighlight: false });
            return true;
            """);

        // A lone text field submits its form on Enter. With the list up the key is the list's.
        await page.FocusAsync("#combo-input");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__submits"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__picks"));

        // With an option highlighted Enter picks it, and still does not submit.
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__picks"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__submits"));

        // Once the list is gone the field is an ordinary one again.
        await page.EvaluateAsync("() => window.__combo.dispose()");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__submits"));
    }

    [E2EFact]
    public async Task Disabled_hotkey_leaves_the_key_to_the_browser()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createHotkey } = await load('hotkeys');
            const ref = recorder();
            window.__hotkeyCalls = ref.calls;
            // createHotkey(combo, ref, preventDefault, skipContentEditable, enabled)
            window.__hotkey = createHotkey('ctrl+j', ref, true, false, false);
            window.__prevented = [];
            // Registered after the hotkey, so it sees what the hotkey left of the event.
            window.addEventListener('keydown', e => { if (e.key === 'j') window.__prevented.push(e.defaultPrevented); });
            return true;
            """);

        await page.Keyboard.PressAsync("Control+j");
        var prevented = await page.EvaluateAsync<bool[]>("() => window.__prevented");
        Assert.Equal([false], prevented);
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__hotkeyCalls.length"));

        await page.EvaluateAsync("() => window.__hotkey.setEnabled(true)");
        await page.Keyboard.PressAsync("Control+j");
        prevented = await page.EvaluateAsync<bool[]>("() => window.__prevented");
        Assert.Equal([false, true], prevented);
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__hotkeyCalls.length"));

        await page.EvaluateAsync("() => window.__hotkey.dispose()");
    }

    [E2EFact]
    public async Task Input_tags_clears_pasted_text_that_was_split_into_tags()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/input-tags");

        var input = page.Locator("[data-bz-input-tags-input]").First;
        await input.ScrollIntoViewIfNeededAsync();
        var root = input.Locator("xpath=ancestor::*[@data-slot='input-tags'][1]");
        var before = await root.Locator("[data-slot=input-tags-chip]").CountAsync();

        // Fill sets the whole text at once, as a paste does.
        await input.FillAsync("paste-one,paste-two,");

        await Assertions.Expect(root.Locator("[data-slot=input-tags-chip]")).ToHaveCountAsync(before + 2);
        // The query is empty in C# both before and after, so nothing was sent to the box and the
        // pasted text stayed in it.
        await Assertions.Expect(input).ToHaveValueAsync("");
    }

    [E2EFact]
    public async Task Copy_leaves_out_what_is_marked_as_skipped()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        // [a diff of removed lines only, a diff with one line that stays, text never split into lines]
        var copied = await RunAsync<string[]>(page,
            """
            await load('clipboard');
            const written = [];
            Object.defineProperty(navigator.clipboard, 'writeText',
                { configurable: true, value: text => { written.push(text); return Promise.resolve(); } });
            const host = document.createElement('div');
            host.innerHTML =
                '<pre id="copy-removed"><span data-bz-copy-skip>old one</span><span data-bz-copy-skip>old two</span></pre>' +
                '<pre id="copy-mixed"><span data-bz-copy-skip>old</span><span data-bz-copy-line>new</span></pre>' +
                '<pre id="copy-plain">all of it</pre>' +
                '<button data-bz-copy="a" data-bz-copy-from="#copy-removed"></button>' +
                '<button data-bz-copy="b" data-bz-copy-from="#copy-mixed"></button>' +
                '<button data-bz-copy="c" data-bz-copy-from="#copy-plain"></button>';
            document.body.append(host);
            for (const button of host.querySelectorAll('button')) button.click();
            await sleep(50);
            host.remove();
            return written;
            """);

        // The removed lines used to be copied, run together ("old oneold two"): with no line marked
        // for copying the script took the whole text, as it does for the plain block.
        Assert.Equal(["", "new", "all of it"], copied);
    }

    [E2EFact]
    public async Task Enter_and_Space_click_an_element_that_is_a_button_in_role_only()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            await load('core'); // installs the delegated guards, as ICore.EnsureGuardsAsync does
            const host = document.createElement('div');
            host.innerHTML =
                '<div id="row" role="button" tabindex="0" data-bz-key-click style="padding:8px">' +
                'Open <button id="row-action" type="button">More</button> <input id="row-field" /></div>';
            document.body.prepend(host);
            window.__rowClicks = 0;
            window.__spacePrevented = [];
            host.querySelector('#row').addEventListener('click', () => window.__rowClicks++);
            // Registered after the guard, on the way back up: it sees what the guard left of the key.
            window.addEventListener('keydown', e => { if (e.key === ' ') window.__spacePrevented.push(e.defaultPrevented); });
            return true;
            """);
        Task<int> Clicks() => page.EvaluateAsync<int>("() => window.__rowClicks");

        // On the row itself both keys press it, as they would a native button.
        await page.FocusAsync("#row");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(1, await Clicks());
        await page.Keyboard.PressAsync("Space");
        Assert.Equal(2, await Clicks());
        // Space scrolls the page unless its default is taken.
        var spacePrevented = await page.EvaluateAsync<bool[]>("() => window.__spacePrevented");
        Assert.Equal([true], spacePrevented);

        // A button inside keeps its own Enter: one click (its own, passing through the row), not two.
        await page.FocusAsync("#row-action");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(3, await Clicks());

        // A field inside keeps its keys entirely: the space is typed, the row is not pressed.
        await page.FocusAsync("#row-field");
        await page.Keyboard.PressAsync("Space");
        Assert.Equal(3, await Clicks());
        Assert.Equal(" ", await page.InputValueAsync("#row-field"));

        // A disabled row is not pressed.
        await page.EvaluateAsync("() => document.getElementById('row').setAttribute('aria-disabled', 'true')");
        await page.FocusAsync("#row");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(3, await Clicks());
    }

    [E2EFact]
    public async Task Only_the_topmost_layer_answers_Escape_and_a_dialog_layer_ignores_outside_presses()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createDismissableLayer } = await load('dismissableLayer');
            const host = document.createElement('div');
            host.innerHTML = '<div id="layer-popover">popover</div><div id="layer-dialog"><button id="layer-inside">ok</button></div>';
            document.body.prepend(host);
            const popover = recorder(), dialog = recorder();
            window.__popoverCalls = popover.calls;
            window.__dialogCalls = dialog.calls;
            // Opened in this order: the popover first, the dialog from it.
            createDismissableLayer(host.querySelector('#layer-popover'), popover,
                { anchorSelector: null, dismissOnEscape: true });
            window.__dialogLayer = createDismissableLayer(host.querySelector('#layer-dialog'), dialog,
                { anchorSelector: null, dismissOnEscape: true, dismissOnOutsidePress: false });
            return true;
            """);
        // "popover calls,dialog calls"
        Task<string> Calls() =>
            page.EvaluateAsync<string>("() => `${window.__popoverCalls.length},${window.__dialogCalls.length}`");

        // A press inside the dialog is outside the popover, but the popover is not on top.
        await page.ClickAsync("#layer-inside");
        Assert.Equal("0,0", await Calls());

        // A press outside both: the dialog leaves that to its overlay, and still shields the popover.
        await page.Mouse.ClickAsync(600, 400);
        Assert.Equal("0,0", await Calls());

        // One Escape, one surface: the dialog.
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("0,1", await Calls());

        // With the dialog gone the popover is on top again: the next Escape is its own.
        await page.EvaluateAsync("() => window.__dialogLayer.dispose()");
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("1,1", await Calls());
    }

    [E2EFact]
    public async Task A_dialog_takes_its_place_above_a_surface_that_was_open_before_it()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/dialog");

        // A surface that is already open when the dialog opens (a popover the dialog was opened
        // from): a layer of the same module the docs page uses, so both sit in one stack.
        await RunAsync<bool>(page,
            """
            const { createDismissableLayer } = await load('dismissableLayer');
            const beneath = document.createElement('div');
            beneath.id = 'beneath';
            document.body.append(beneath);
            const ref = recorder();
            window.__beneathCalls = ref.calls;
            createDismissableLayer(beneath, ref, { anchorSelector: null, dismissOnEscape: true });
            return true;
            """);
        Task<int> BeneathCalls() => page.EvaluateAsync<int>("() => window.__beneathCalls.length");

        var trigger = page.Locator("[data-slot=dialog-trigger]").First;
        await trigger.ScrollIntoViewIfNeededAsync();
        await trigger.EvaluateAsync("el => el.click()"); // not a pointer press: that one is the surface's to see
        var dialog = DocsServerFixture.VisibleDialog(page);
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await DocsServerFixture.WaitForDialogFocusAsync(page);
        await page.WaitForTimeoutAsync(300); // the dialog's layer is created after its first render

        // A press inside the dialog used to be a press outside the surface beneath, which closed
        // (and took a dialog declared inside it along).
        await dialog.Locator("[data-slot=dialog-title]").First.ClickAsync();
        Assert.Equal(0, await BeneathCalls());

        // Escape closes the dialog and only the dialog.
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog).Not.ToBeVisibleAsync();
        Assert.Equal(0, await BeneathCalls());

        // The surface beneath is on top again once the dialog is on its way out.
        await page.WaitForTimeoutAsync(400);
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal(1, await BeneathCalls());
    }
}
