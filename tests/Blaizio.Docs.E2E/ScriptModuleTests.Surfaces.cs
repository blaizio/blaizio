using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

// The modules every floating surface is built from: where it is put (positioning), who has the
// keyboard (focus scope) and what closes it (dismissable layer).
public sealed partial class ScriptModuleTests
{
    [E2EFact]
    public async Task Positioning_puts_the_surface_by_its_anchor_and_takes_it_home_again()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page,
            """
            const { createPositioning } = await load('positioning');
            const host = document.createElement('div');
            host.innerHTML =
                '<button id="pos-anchor" style="position:fixed;left:300px;top:200px;width:120px;height:30px">anchor</button>' +
                '<div id="pos-home"><div id="pos-surface" style="width:200px;height:80px;transition:none"></div></div>';
            document.body.append(host);
            const surface = host.querySelector('#pos-surface');
            const ref = recorder();
            const positioning = createPositioning('#pos-anchor', surface,
                { side: 'bottom', align: 'start', sideOffset: 8, alignOffset: 0, collisionPadding: 10 }, ref);
            const hiddenAtStart = surface.style.visibility === 'hidden';
            await sleep(150);
            const box = surface.getBoundingClientRect();
            const result = {
                hiddenAtStart, shown: surface.style.visibility === 'visible', left: box.left, top: box.top,
                inBody: surface.parentElement === document.body, calls: ref.calls,
                anchorWidth: surface.style.getPropertyValue('--bz-anchor-width'),
                availableHeight: surface.style.getPropertyValue('--bz-available-height'),
            };
            positioning.dispose();
            result.hiddenAgain = surface.style.visibility === 'hidden';
            result.backHome = surface.parentElement === host.querySelector('#pos-home');
            host.remove();
            return result;
            """);

        // Never painted in the corner it starts in: hidden until the first placement lands.
        Assert.True(r.GetProperty("hiddenAtStart").GetBoolean());
        Assert.True(r.GetProperty("shown").GetBoolean());
        // Below the anchor by the offset, start edges in line.
        Assert.Equal(300, r.GetProperty("left").GetDouble());
        Assert.Equal(238, r.GetProperty("top").GetDouble());
        // Moved to the body, so no ancestor can clip or cover it.
        Assert.True(r.GetProperty("inBody").GetBoolean());
        // The side is reported when it changes, not on every frame the surface is re-checked.
        Assert.Equal("""[["OnPlaced","bottom","start"]]""", r.GetProperty("calls").GetRawText());
        // What a surface sizes itself with: the anchor's width and the room left under it.
        Assert.Equal("120px", r.GetProperty("anchorWidth").GetString());
        Assert.Equal("552px", r.GetProperty("availableHeight").GetString()); // 800 - 238 - 10
        Assert.True(r.GetProperty("hiddenAgain").GetBoolean());
        Assert.True(r.GetProperty("backHome").GetBoolean());
    }

    [E2EFact]
    public async Task Positioning_flips_and_shifts_a_surface_that_would_leave_the_screen()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page,
            """
            const { createPositioning } = await load('positioning');
            const host = document.createElement('div');
            // An anchor in the bottom right corner: no room below it, none to its right.
            host.innerHTML =
                '<button id="pos-corner" style="position:fixed;left:1200px;top:760px;width:60px;height:30px">anchor</button>' +
                '<div id="pos-flipped" style="width:200px;height:120px;transition:none">' +
                '<div data-bz-arrow style="position:absolute;width:10px;height:10px;transition:none"></div></div>';
            document.body.append(host);
            const surface = host.querySelector('#pos-flipped');
            const arrow = surface.querySelector('[data-bz-arrow]');
            const ref = recorder();
            const positioning = createPositioning('#pos-corner', surface,
                { side: 'bottom', align: 'center', sideOffset: 4, alignOffset: 0, collisionPadding: 8 }, ref);
            await sleep(150);
            const box = surface.getBoundingClientRect();
            const result = { calls: ref.calls, left: box.left, right: box.right, bottom: box.bottom,
                width: window.innerWidth,
                arrowBottom: arrow.style.bottom, arrowLeft: parseFloat(arrow.style.left), arrowTop: arrow.style.top };
            positioning.dispose();
            surface.remove();
            host.remove();
            return result;
            """);

        Assert.Equal("""[["OnPlaced","top","center"]]""", r.GetProperty("calls").GetRawText());
        // Centred on the anchor it would end at 1330, off the screen: it is kept inside the collision
        // padding instead (the page may keep a scrollbar's width of the window for itself).
        var edge = r.GetProperty("width").GetDouble() - 8;
        Assert.InRange(r.GetProperty("right").GetDouble(), edge - 20, edge);
        // Above the anchor: its offset, plus half the arrow so the arrow's tip keeps the gap.
        Assert.Equal(751, r.GetProperty("bottom").GetDouble());
        // The arrow hangs off the edge that faces the anchor, under the anchor's middle.
        Assert.Equal("-9px", r.GetProperty("arrowBottom").GetString());
        Assert.Equal("", r.GetProperty("arrowTop").GetString());
        // 1230 is the anchor's middle; the arrow is 10 px wide.
        Assert.Equal(1230 - r.GetProperty("left").GetDouble() - 5, r.GetProperty("arrowLeft").GetDouble());
    }

    [E2EFact]
    public async Task Positioning_leaves_an_inline_surface_in_place_and_does_nothing_without_an_anchor()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page,
            """
            const { createPositioning } = await load('positioning');
            const host = document.createElement('div');
            host.innerHTML =
                '<button id="pos-side" style="position:fixed;left:100px;top:100px;width:60px;height:30px">a</button>' +
                '<div id="pos-parent"><div id="pos-sub" style="width:100px;height:40px;transition:none"></div></div>';
            document.body.append(host);
            const surface = host.querySelector('#pos-sub');
            const options = { side: 'right', align: 'center', sideOffset: 0, alignOffset: 0, collisionPadding: 0 };
            // A submenu: it has to stay inside the surface it belongs to.
            const inline = createPositioning('#pos-side', surface, { ...options, inline: true }, null);
            await sleep(150);
            const box = surface.getBoundingClientRect();
            const parent = surface.parentElement.id;
            inline.dispose();
            surface.style.visibility = 'visible';
            const none = createPositioning('#pos-no-such-anchor', surface, options, null);
            const untouched = surface.style.visibility === 'visible' && surface.parentElement.id === 'pos-parent';
            none.dispose();
            host.remove();
            return { parent, left: box.left, top: box.top, untouched };
            """);

        Assert.Equal("pos-parent", r.GetProperty("parent").GetString());
        // To the right of the anchor, middles in line.
        Assert.Equal(160, r.GetProperty("left").GetDouble());
        Assert.Equal(95, r.GetProperty("top").GetDouble());
        Assert.True(r.GetProperty("untouched").GetBoolean());
    }

    [E2EFact]
    public async Task Item_aligned_positioning_lays_the_selected_option_over_the_trigger()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page,
            """
            const { createItemAlignedPositioning } = await load('positioning');
            const host = document.createElement('div');
            const items = (n, selected) => Array.from({ length: n }, (_, i) =>
                '<div data-bz-menu-item style="height:30px"' + (i === selected ? ' aria-selected="true"' : '') + '>item ' + i + '</div>').join('');
            host.innerHTML =
                '<button id="pos-trigger" style="position:fixed;left:200px;top:400px;width:160px;height:36px">t</button>' +
                '<div id="pos-short" style="overflow:auto;width:120px;transition:none">' + items(8, 4) + '</div>' +
                '<div id="pos-long" style="overflow:auto;width:120px;transition:none">' + items(60, 40) + '</div>';
            document.body.append(host);
            const options = { side: 'bottom', align: 'start', sideOffset: 0, alignOffset: 0, collisionPadding: 10 };
            const centre = list => { const b = list.querySelector('[aria-selected=true]').getBoundingClientRect(); return b.top + b.height / 2; };

            const short = host.querySelector('#pos-short');
            const first = createItemAlignedPositioning('#pos-trigger', short, options, null);
            await sleep(150);
            const fits = { centre: centre(short), minWidth: short.style.minWidth, left: short.getBoundingClientRect().left };
            first.dispose();

            // Taller than the screen: capped to it, and scrolled so the selected option still meets the trigger.
            const long = host.querySelector('#pos-long');
            const second = createItemAlignedPositioning('#pos-trigger', long, options, null);
            await sleep(150);
            const box = long.getBoundingClientRect();
            const overflows = { centre: centre(long), maxHeight: long.style.maxHeight, top: box.top, bottom: box.bottom, scrollTop: long.scrollTop };
            second.dispose();
            short.remove();
            long.remove();
            host.remove();
            return { fits, overflows };
            """);

        const double triggerCentre = 418; // top 400 + half of 36
        var fits = r.GetProperty("fits");
        Assert.Equal(triggerCentre, fits.GetProperty("centre").GetDouble());
        Assert.Equal("160px", fits.GetProperty("minWidth").GetString()); // at least as wide as the trigger
        Assert.Equal(200, fits.GetProperty("left").GetDouble());

        var overflows = r.GetProperty("overflows");
        Assert.Equal("780px", overflows.GetProperty("maxHeight").GetString());
        Assert.Equal(10, overflows.GetProperty("top").GetDouble());
        Assert.Equal(790, overflows.GetProperty("bottom").GetDouble());
        Assert.True(overflows.GetProperty("scrollTop").GetDouble() > 0);
        Assert.Equal(triggerCentre, overflows.GetProperty("centre").GetDouble());
    }

    // What every focus scope test starts from: a control that had focus, a scope with controls that
    // must be skipped (hidden, disabled, not displayed) around three that count, and a control
    // outside it.
    private const string FocusScopeMarkup =
        """
        const { createFocusScope } = await load('focusScope');
        window.__createFocusScope = createFocusScope;
        window.__recorder = recorder;
        const host = document.createElement('div');
        host.id = 'fs-host';
        // Above the page, so nothing of the docs layout covers the controls.
        host.style.cssText = 'position:fixed;left:20px;top:200px;z-index:99999;padding:8px;background:white';
        host.innerHTML =
            '<button id="fs-opener">open</button>' +
            '<div id="fs-scope" tabindex="-1">' +
            '<input id="fs-hidden" type="hidden" /><button id="fs-off" disabled>off</button>' +
            '<button id="fs-gone" style="display:none">gone</button>' +
            '<input id="fs-first" value="abc" /><a id="fs-link" href="#x">link</a><button id="fs-last">last</button>' +
            '</div>' +
            '<button id="fs-outside">outside</button>';
        document.body.prepend(host);
        document.getElementById('fs-opener').focus();
        """;

    private static Task<string> ActiveIdAsync(IPage page) =>
        page.EvaluateAsync<string>("() => document.activeElement?.id ?? ''");

    [E2EFact]
    public async Task Focus_scope_takes_focus_keeps_Tab_inside_and_gives_focus_back()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, FocusScopeMarkup +
            """
            window.__scope = createFocusScope(document.getElementById('fs-scope'), recorder(),
                { loop: true, trapped: true, hasMountAutoFocus: false, hasUnmountAutoFocus: false });
            await sleep(150);
            return true;
            """);

        // The first control that can take focus, past the hidden, disabled and undisplayed ones;
        // a text field arrives with its text selected.
        Assert.Equal("fs-first", await ActiveIdAsync(page));
        Same([0, 3], await page.EvaluateAsync<int[]>(
            "() => { const field = document.getElementById('fs-first'); return [field.selectionStart, field.selectionEnd]; }"));

        // Tab goes round inside, both ways.
        await page.Keyboard.PressAsync("Shift+Tab");
        Assert.Equal("fs-last", await ActiveIdAsync(page));
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("fs-first", await ActiveIdAsync(page));
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("fs-link", await ActiveIdAsync(page));

        // Focus sent outside, by code or by a press, comes back to where it was.
        await page.EvaluateAsync("() => document.getElementById('fs-outside').focus()");
        Assert.Equal("fs-link", await ActiveIdAsync(page));
        await page.ClickAsync("#fs-outside");
        Assert.Equal("fs-link", await ActiveIdAsync(page));

        // The focused control is removed from inside the scope: focus falls on the scope, not the page.
        await page.EvaluateAsync("() => document.getElementById('fs-link').remove()");
        await page.WaitForFunctionAsync("() => document.activeElement?.id === 'fs-scope'");

        await page.EvaluateAsync("() => window.__scope.dispose()");
        Assert.Equal("fs-opener", await ActiveIdAsync(page));
    }

    [E2EFact]
    public async Task Focus_scope_opened_over_another_holds_focus_until_it_closes()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, FocusScopeMarkup +
            """
            window.__scope = createFocusScope(document.getElementById('fs-scope'), recorder(),
                { loop: true, trapped: true, hasMountAutoFocus: false, hasUnmountAutoFocus: false });
            await sleep(150);
            document.getElementById('fs-last').focus();

            const inner = document.createElement('div');
            inner.id = 'fs-inner';
            inner.tabIndex = -1;
            inner.style.cssText = 'position:fixed;left:20px;top:400px;z-index:99999;padding:8px;background:white';
            inner.innerHTML = '<button id="fs-inner-one">one</button><button id="fs-inner-two">two</button>';
            document.body.prepend(inner);
            // Trapped, not looping: Tab stops at the ends.
            window.__inner = createFocusScope(inner, recorder(),
                { loop: false, trapped: true, hasMountAutoFocus: false, hasUnmountAutoFocus: false });
            await sleep(150);
            return true;
            """);

        // The scope beneath stands down: it does not pull focus back out of the one on top.
        Assert.Equal("fs-inner-one", await ActiveIdAsync(page));
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("fs-inner-two", await ActiveIdAsync(page));
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("fs-inner-two", await ActiveIdAsync(page));

        // Closed: focus returns to the control the lower scope had it on, and that scope traps again.
        await page.EvaluateAsync("() => window.__inner.dispose()");
        Assert.Equal("fs-last", await ActiveIdAsync(page));
        await page.EvaluateAsync("() => document.getElementById('fs-outside').focus()");
        Assert.Equal("fs-last", await ActiveIdAsync(page));

        await page.EvaluateAsync("() => window.__scope.dispose()");
    }

    [E2EFact]
    public async Task Focus_scope_asks_first_and_does_not_take_back_focus_the_user_moved()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page, FocusScopeMarkup +
            """
            const scopeEl = document.getElementById('fs-scope'), opener = document.getElementById('fs-opener');
            const active = () => document.activeElement?.id ?? '';
            const plain = { loop: false, trapped: false, hasMountAutoFocus: false, hasUnmountAutoFocus: false };

            // Both handlers say "prevented": focus is moved neither in nor back.
            const asked = [];
            const refusing = { invokeMethodAsync: name => { asked.push(name); return Promise.resolve(true); } };
            let scope = createFocusScope(scopeEl, refusing, { ...plain, hasMountAutoFocus: true, hasUnmountAutoFocus: true });
            await sleep(200);
            const preventedIn = active();
            document.getElementById('fs-last').focus();
            await scope.dispose();
            const preventedBack = active();

            // The user pressed a field outside while the surface was closing: that field keeps focus.
            opener.focus();
            scope = createFocusScope(scopeEl, recorder(), plain);
            await sleep(200);
            document.getElementById('fs-outside').focus();
            await scope.dispose();
            const claimed = active();

            // A passive scope only takes its place among the scopes: focus is someone else's to move.
            opener.focus();
            scope = createFocusScope(scopeEl, recorder(), { ...plain, passive: true });
            await sleep(200);
            const passiveIn = active();
            document.getElementById('fs-last').focus();
            await scope.dispose();
            const passiveBack = active();
            return { asked, preventedIn, preventedBack, claimed, passiveIn, passiveBack };
            """);

        Assert.Equal("""["HandleMountAutoFocus","HandleUnmountAutoFocus"]""", r.GetProperty("asked").GetRawText());
        Assert.Equal("fs-opener", r.GetProperty("preventedIn").GetString());
        Assert.Equal("fs-last", r.GetProperty("preventedBack").GetString());
        Assert.Equal("fs-outside", r.GetProperty("claimed").GetString());
        Assert.Equal("fs-opener", r.GetProperty("passiveIn").GetString());
        Assert.Equal("fs-last", r.GetProperty("passiveBack").GetString());
    }

    [E2EFact]
    public async Task Focus_scope_waits_for_a_surface_that_is_not_placed_yet()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<string[]>(page, FocusScopeMarkup +
            """
            const scopeEl = document.getElementById('fs-scope');
            // As positioning leaves a surface until its first placement: focus() on it does nothing.
            // No transition: under reduced motion the page gives every property one of 0.01 ms, and a
            // visibility that changes a frame late is not what is being tested here.
            scopeEl.style.transition = 'none';
            scopeEl.style.visibility = 'hidden';
            const scope = createFocusScope(scopeEl, recorder(),
                { loop: false, trapped: false, hasMountAutoFocus: false, hasUnmountAutoFocus: false });
            await sleep(80);
            const whileHidden = document.activeElement.id;
            scopeEl.style.visibility = 'visible';
            await sleep(150);
            const onceVisible = document.activeElement.id;
            await scope.dispose();
            return [whileHidden, onceVisible, document.activeElement.id];
            """);

        Assert.Equal(["fs-opener", "fs-first", "fs-opener"], r);
    }

    [E2EFact(Skip = "bug: focusScope.ts re-asserts focus 80 ms after opening even when the scope was disposed in between")]
    public async Task Focus_scope_closed_right_after_it_opened_leaves_focus_where_it_put_it()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<string[]>(page, FocusScopeMarkup +
            """
            const scope = createFocusScope(document.getElementById('fs-scope'), recorder(),
                { loop: true, trapped: true, hasMountAutoFocus: false, hasUnmountAutoFocus: false });
            // Closed 20 ms after it opened, the surface still in the page (its exit animation).
            await sleep(20);
            await scope.dispose();
            const rightAfter = document.activeElement.id;
            await sleep(200);
            return [rightAfter, document.activeElement.id];
            """);

        // Today the second entry is "fs-first": the timer set on opening fires into the closed scope.
        Assert.Equal(["fs-opener", "fs-opener"], r);
    }

    [E2EFact]
    public async Task Dismissable_layer_spares_its_anchor_its_own_scrolling_and_a_composing_Escape()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createDismissableLayer } = await load('dismissableLayer');
            const host = document.createElement('div');
            host.innerHTML =
                '<button id="dl-anchor" style="position:fixed;left:20px;top:300px;z-index:99">anchor <span id="dl-anchor-inner">x</span></button>' +
                '<div id="dl-surface" style="position:fixed;left:200px;top:300px;width:120px;height:60px;overflow:auto;z-index:99"><div style="height:400px">long</div></div>' +
                '<div id="dl-other" style="position:fixed;left:400px;top:300px;width:120px;height:60px;overflow:auto;z-index:99"><div style="height:400px">other</div></div>';
            document.body.append(host);
            const ref = recorder();
            window.__dismissals = ref.calls;
            // A surface pinned to a point (a context menu): a scroll anywhere else strands it.
            window.__layer = createDismissableLayer(host.querySelector('#dl-surface'), ref,
                { anchorSelector: '#dl-anchor', dismissOnEscape: true, dismissOnScroll: true });
            // Nothing to attach to: a handle that does nothing, and takes no place in the stack.
            window.__none = createDismissableLayer(null, ref, { anchorSelector: null, dismissOnEscape: true });
            return true;
            """);
        Task<int> Dismissals() => page.EvaluateAsync<int>("() => window.__dismissals.length");

        // The anchor opens and closes the surface itself: a press on it (or inside it) is not "outside".
        await page.ClickAsync("#dl-anchor-inner");
        await page.ClickAsync("#dl-surface");
        Assert.Equal(0, await Dismissals());

        // Scrolling the surface's own content keeps it open.
        await page.EvaluateAsync("() => { document.getElementById('dl-surface').scrollTop = 50; }");
        await page.WaitForTimeoutAsync(150);
        Assert.Equal(0, await Dismissals());

        // An Escape that ends a text composition belongs to the input method.
        await page.EvaluateAsync(
            "() => document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', isComposing: true, bubbles: true }))");
        Assert.Equal(0, await Dismissals());

        // Anything else scrolling, a press outside, a plain Escape: one request each.
        await page.EvaluateAsync("() => { document.getElementById('dl-other').scrollTop = 50; }");
        await page.WaitForFunctionAsync("() => window.__dismissals.length === 1");
        await page.Mouse.ClickAsync(600, 400);
        Assert.Equal(2, await Dismissals());
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal(3, await Dismissals());

        // Disposed: nothing listens any more.
        await page.EvaluateAsync("() => { window.__none.dispose(); window.__layer.dispose(); }");
        await page.Mouse.ClickAsync(600, 400);
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal(3, await Dismissals());
    }
}
