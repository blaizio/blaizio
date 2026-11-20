using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

// The modules that move the keyboard through a set of controls: one menu level and its submenus,
// the navigation menu, the single tab stop of a group, and the two that decide what a crowded row
// shows (a toolbar's overflow, a multi-select's tokens).
public sealed partial class ScriptModuleTests
{
    // Presses each key in turn and says which element had focus after each.
    private static async Task<string[]> FocusAfterEachKeyAsync(IPage page, params string[] keys)
    {
        var ids = new string[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            await page.Keyboard.PressAsync(keys[i]);
            ids[i] = await ActiveIdAsync(page);
        }
        return ids;
    }

    // Reads and empties a list a page script keeps on the window (keys seen, clicks made).
    private static Task<string[]> DrainAsync(IPage page, string list) =>
        page.EvaluateAsync<string[]>("name => window[name].splice(0)", list);

    // A key pressed with a modifier arrives as two keydowns: the modifier's own is not the point.
    private static string[] WithoutModifierKeys(string[] keys) =>
        [.. keys.Where(k => !k.StartsWith("Control") && !k.StartsWith("Alt") && !k.StartsWith("Shift"))];

    // One menu item as the components render it: a role, no tab stop of its own, and the marker
    // the script finds it by.
    private const string MenuItemScript =
        """
        const item = (id, text, extra = '') =>
            '<div id="' + id + '" role="menuitem" tabindex="-1" data-bz-menu-item style="height:30px" ' + extra + '>' + text + '</div>';
        """;

    [E2EFact]
    public async Task Menu_keys_walk_the_enabled_items_of_one_level_and_press_the_highlighted_one()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, MenuItemScript +
            """
            const { createMenu } = await load('menu');
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white;display:flex;gap:20px';
            host.innerHTML =
                '<button id="mn-trigger">Open</button>' +
                '<div id="mn-menu" role="menu" tabindex="-1" data-bz-menu-content style="width:200px">' +
                item('mn-apple', 'Apple') +
                item('mn-avocado', 'Avocado', 'aria-disabled="true" data-disabled') +
                '<div id="mn-label" style="height:30px">Fruit</div>' +
                item('mn-banana', 'Banana') +
                '<div id="mn-blueberry" role="menuitemcheckbox" tabindex="-1" data-bz-menu-item style="height:30px">Blueberry</div>' +
                item('mn-more', 'More', 'aria-haspopup="menu" aria-expanded="true" aria-controls="mn-sub"') +
                // An open submenu: its surface is a descendant of this one, its items are not this level's.
                '<div id="mn-sub" role="menu" tabindex="-1" data-bz-menu-content style="position:fixed;left:400px;top:300px;width:160px;background:white">' +
                item('mn-sub-berry', 'Boysenberry') + item('mn-sub-cherry', 'Cherry') + '</div>' +
                item('mn-cherry', 'Cherry') +
                '<button id="mn-citron" role="menuitem" tabindex="-1" data-bz-menu-item disabled>Citron</button>' +
                '</div>' +
                '<div id="mn-flat" role="menu" tabindex="-1" data-bz-menu-content style="width:120px">' +
                item('mn-flat-one', 'One') + item('mn-flat-two', 'Two') + item('mn-flat-three', 'Three') + '</div>' +
                '<div id="mn-none" role="menu" tabindex="-1" data-bz-menu-content style="width:60px;height:30px"></div>';
            document.body.append(host);
            const options = { initialFocus: 'none', typeaheadTimeout: 400, isSubmenu: false, triggerSelector: '#mn-trigger' };
            const menu = document.getElementById('mn-menu');
            window.__mn = createMenu(menu, { ...options, loop: true }, null);
            createMenu(document.getElementById('mn-flat'), { ...options, loop: false }, null);
            createMenu(document.getElementById('mn-none'), { ...options, loop: true }, null);
            // Registered after the menu's own listener, on the same surface: what the menu left of the key.
            window.__mnKeys = [];
            menu.addEventListener('keydown', e => window.__mnKeys.push(e.key + ':' + e.defaultPrevented));
            // The keys that travelled on past the menu, to whatever it is inside of.
            window.__mnPassed = [];
            host.addEventListener('keydown', e => window.__mnPassed.push(e.key));
            window.__mnClicks = [];
            host.addEventListener('click', e => window.__mnClicks.push(e.target.id));
            return true;
            """);

        // From the surface itself (a menu opened by the pointer) the first arrow steps onto an item.
        // The arrows pass over what cannot be chosen (a disabled item, a label, a disabled button),
        // stay out of the open submenu, and go round at the ends.
        await page.FocusAsync("#mn-menu");
        Same(["mn-apple", "mn-banana", "mn-blueberry", "mn-more", "mn-cherry", "mn-apple", "mn-cherry", "mn-more"],
            await FocusAfterEachKeyAsync(page, "ArrowDown", "ArrowDown", "ArrowDown", "ArrowDown", "ArrowDown", "ArrowDown", "ArrowUp", "ArrowUp"));
        Same(["mn-cherry", "mn-apple"], await FocusAfterEachKeyAsync(page, "End", "Home"));
        await page.FocusAsync("#mn-menu");
        Same(["mn-cherry"], await FocusAfterEachKeyAsync(page, "ArrowUp"));
        // The menu's keys neither scroll the page nor reach a surface the menu is inside of.
        Same(["ArrowDown:true", "ArrowDown:true", "ArrowDown:true", "ArrowDown:true", "ArrowDown:true", "ArrowDown:true",
            "ArrowUp:true", "ArrowUp:true", "End:true", "Home:true", "ArrowUp:true"], await DrainAsync(page, "__mnKeys"));
        Same([], await DrainAsync(page, "__mnPassed"));

        // A shortcut is not navigation.
        Same(["mn-cherry", "mn-cherry"], await FocusAfterEachKeyAsync(page, "Control+ArrowDown", "Alt+ArrowUp"));
        Same(["ArrowDown:false", "ArrowUp:false"], WithoutModifierKeys(await DrainAsync(page, "__mnKeys")));
        Same(["ArrowDown", "ArrowUp"], WithoutModifierKeys(await DrainAsync(page, "__mnPassed")));

        // Typing goes to the item that starts with what was typed: the next one down, round the end.
        // A second letter narrows the search for as long as the typing goes on.
        await page.FocusAsync("#mn-apple");
        Same(["mn-banana", "mn-blueberry"], await FocusAfterEachKeyAsync(page, "b", "l"));
        await page.WaitForTimeoutAsync(500); // past the 400 ms after which typing starts over
        Same(["mn-cherry", "mn-cherry"], await FocusAfterEachKeyAsync(page, "c", "h"));
        await page.WaitForTimeoutAsync(500);
        // The same letter again and again goes through the items that start with it.
        await page.FocusAsync("#mn-apple");
        Same(["mn-banana", "mn-blueberry", "mn-banana"], await FocusAfterEachKeyAsync(page, "b", "b", "b"));
        await page.WaitForTimeoutAsync(500);
        // Nothing starts with it: focus stays. A disabled item is not found either ("av" is Avocado's alone).
        Same(["mn-banana"], await FocusAfterEachKeyAsync(page, "z"));
        await page.WaitForTimeoutAsync(500);
        Same(["mn-apple", "mn-apple"], await FocusAfterEachKeyAsync(page, "a", "v"));
        // A letter is the menu's alone (a surface around it has its own type-ahead), and has no default to take.
        Same([], await DrainAsync(page, "__mnPassed"));
        Assert.All(await DrainAsync(page, "__mnKeys"), key => Assert.EndsWith(":false", key));

        // Enter and Space press the highlighted item. One that closes the menu hands focus back to
        // the trigger at once; a checkbox item and a submenu's trigger may leave the menu open, so
        // focus stays with them.
        await page.FocusAsync("#mn-blueberry");
        Same(["mn-blueberry"], await FocusAfterEachKeyAsync(page, "Enter"));
        await page.FocusAsync("#mn-more");
        Same(["mn-more"], await FocusAfterEachKeyAsync(page, "Space"));
        await page.FocusAsync("#mn-banana");
        Same(["mn-trigger"], await FocusAfterEachKeyAsync(page, "Enter"));
        Same(["mn-blueberry", "mn-more", "mn-banana"], await DrainAsync(page, "__mnClicks"));
        Same(["Enter:true", " :true", "Enter:true"], await DrainAsync(page, "__mnKeys"));
        // With nothing highlighted there is nothing to press, and the key is not the menu's.
        await page.FocusAsync("#mn-menu");
        await page.Keyboard.PressAsync("Enter");
        Same([], await DrainAsync(page, "__mnClicks"));
        Same(["Enter:false"], await DrainAsync(page, "__mnKeys"));

        // Focus in the open submenu: its keys are that level's, and this one leaves them alone.
        await page.FocusAsync("#mn-sub-berry");
        Same(["mn-sub-berry", "mn-sub-berry"], await FocusAfterEachKeyAsync(page, "ArrowDown", "End"));
        Same(["ArrowDown:false", "End:false"], await DrainAsync(page, "__mnKeys"));

        // A menu that does not loop stops at its ends; an empty one has nowhere to go.
        await page.FocusAsync("#mn-flat-one");
        Same(["mn-flat-one", "mn-flat-three", "mn-flat-three", "mn-flat-two"], await FocusAfterEachKeyAsync(page, "ArrowUp", "End", "ArrowDown", "ArrowUp"));
        await page.FocusAsync("#mn-none");
        Same(["mn-none", "mn-none", "mn-none"], await FocusAfterEachKeyAsync(page, "ArrowDown", "End", "x"));

        // Disposed: the keys are the page's again.
        await page.EvaluateAsync("() => window.__mn.dispose()");
        await page.FocusAsync("#mn-apple");
        await DrainAsync(page, "__mnKeys");
        Same(["mn-apple"], await FocusAfterEachKeyAsync(page, "ArrowDown"));
        Same(["ArrowDown:false"], await DrainAsync(page, "__mnKeys"));
    }

    [E2EFact]
    public async Task Menu_type_ahead_moves_on_from_an_item_that_already_starts_with_the_letter()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, MenuItemScript +
            """
            const { createMenu } = await load('menu');
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white';
            host.innerHTML =
                '<div id="mta" role="menu" tabindex="-1" data-bz-menu-content style="width:200px">' +
                item('mta-apple', 'Apple') + item('mta-banana', 'Banana') + item('mta-blueberry', 'Blueberry') + '</div>';
            document.body.append(host);
            // The component's own timing: typing starts over after a second.
            createMenu(document.getElementById('mta'),
                { initialFocus: 'first', typeaheadTimeout: 1000, loop: true, isSubmenu: false, triggerSelector: '' }, null);
            await sleep(60);
            return true;
            """);

        // One press of a letter goes to the next item that starts with it. Pressed again once the
        // typing has started over (anyone slower than a press a second), it goes on to the one
        // after. It used to find the item it was already on, and "Blueberry" could not be reached
        // by pressing b at that pace.
        Same(["mta-banana"], await FocusAfterEachKeyAsync(page, "b"));
        await page.WaitForTimeoutAsync(1200);
        Same(["mta-blueberry"], await FocusAfterEachKeyAsync(page, "b"));
        await page.WaitForTimeoutAsync(1200);
        Same(["mta-banana"], await FocusAfterEachKeyAsync(page, "b"));
    }

    [E2EFact]
    public async Task Menu_puts_the_opening_focus_where_it_was_asked_once_the_surface_can_take_it()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page,
            """
            const { createMenu } = await load('menu');
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white;display:flex;gap:10px';
            // A listbox of twelve options that shows three of them; the tenth is the chosen one.
            const list = (id, style = '') =>
                '<div id="' + id + '" role="listbox" tabindex="-1" data-bz-menu-content style="width:120px;height:90px;overflow:auto;' + style + '">' +
                Array.from({ length: 12 }, (_, i) => '<div id="' + id + '-' + i + '" role="option" tabindex="-1" data-bz-menu-item style="height:30px"' +
                    (i === 9 ? ' aria-selected="true"' : '') + '>Option ' + i + '</div>').join('') + '</div>';
            host.innerHTML = '<button id="mo-trigger">Open</button>' + list('mo-a') + list('mo-b') +
                // As positioning leaves a surface until its first placement: focus() on it does nothing.
                list('mo-late', 'visibility:hidden;transition:none') + list('mo-never', 'visibility:hidden;transition:none') +
                '<div id="mo-empty" role="menu" tabindex="-1" data-bz-menu-content style="width:60px;height:30px"></div>';
            document.body.append(host);
            const trigger = document.getElementById('mo-trigger');
            const active = () => document.activeElement?.id ?? '';
            // Opened from the trigger, which has focus, as every menu is.
            const open = async (id, initialFocus) => {
                trigger.focus();
                const menu = createMenu(document.getElementById(id),
                    { initialFocus, typeaheadTimeout: 400, loop: true, isSubmenu: false, triggerSelector: '#mo-trigger' }, null);
                await sleep(60);
                return menu;
            };
            const result = {};

            let menu = await open('mo-a', 'none'); result.none = active(); menu.dispose();
            menu = await open('mo-a', 'first'); result.first = active(); menu.dispose();
            menu = await open('mo-a', 'last'); result.last = active(); menu.dispose();

            const box = document.getElementById('mo-b'), chosen = document.getElementById('mo-b-9');
            menu = await open('mo-b', 'selected');
            result.selected = active();
            const boxRect = box.getBoundingClientRect(), chosenRect = chosen.getBoundingClientRect();
            result.selectedInView = chosenRect.top >= boxRect.top - 0.5 && chosenRect.bottom <= boxRect.bottom + 0.5;
            menu.dispose();
            chosen.removeAttribute('aria-selected');
            menu = await open('mo-b', 'selected'); result.nothingSelected = active(); menu.dispose();

            // Not placed yet: the menu keeps trying, and takes focus the moment the surface shows.
            const late = document.getElementById('mo-late');
            menu = await open('mo-late', 'first');
            result.whileHidden = active();
            late.style.visibility = 'visible';
            await sleep(100);
            result.onceVisible = active();
            // Having placed it once, it does not take focus back from wherever it goes next.
            trigger.focus();
            await sleep(100);
            result.leftAlone = active();
            // Reopened while it was still closing, the same surface is asked to place focus again.
            menu.refocus();
            await sleep(60);
            result.refocused = active();
            menu.dispose();

            // No items ever arrive: after its tries (about half a second) focus goes to the surface,
            // so the keyboard is at least inside the menu. The tries are 16 ms timers, and a page
            // that has been open for about three seconds gets them at half that pace (the same on
            // an empty page), so this waits for the outcome and not for a set time: a fixed 700 ms
            // passed or failed with how long the docs app had taken to start.
            menu = await open('mo-empty', 'first');
            result.emptyAtFirst = active();
            for (let waited = 0; waited < 3000 && active() !== 'mo-empty'; waited += 50) await sleep(50);
            result.emptyInTheEnd = active();
            menu.dispose();

            // Disposed while still waiting: a surface that shows later is not given focus.
            menu = await open('mo-never', 'first');
            menu.dispose();
            document.getElementById('mo-never').style.visibility = 'visible';
            await sleep(150);
            result.disposedWhileWaiting = active();

            // Nothing to attach to: a handle that does nothing.
            createMenu(null, { initialFocus: 'first', typeaheadTimeout: 400, loop: true, isSubmenu: false, triggerSelector: '' }, null).dispose();
            host.remove();
            return result;
            """);
        string At(string name) => r.GetProperty(name).GetString()!;

        // Opened by the pointer: the surface, so nothing is highlighted under the cursor. Opened by
        // ArrowDown or ArrowUp: the first or the last item.
        Assert.Equal("mo-a", At("none"));
        Assert.Equal("mo-a-0", At("first"));
        Assert.Equal("mo-a-11", At("last"));
        // A select opens on its chosen option, scrolled to where it can be seen; with nothing
        // chosen, on the first.
        Assert.Equal("mo-b-9", At("selected"));
        Assert.True(r.GetProperty("selectedInView").GetBoolean());
        Assert.Equal("mo-b-0", At("nothingSelected"));
        Assert.Equal("mo-trigger", At("whileHidden"));
        Assert.Equal("mo-late-0", At("onceVisible"));
        Assert.Equal("mo-trigger", At("leftAlone"));
        Assert.Equal("mo-late-0", At("refocused"));
        Assert.Equal("mo-trigger", At("emptyAtFirst"));
        Assert.Equal("mo-empty", At("emptyInTheEnd"));
        Assert.Equal("mo-trigger", At("disposedWhileWaiting"));
    }

    [E2EFact]
    public async Task Submenu_closes_on_the_inline_start_arrow_and_when_focus_leaves_for_anything_but_its_trigger()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, MenuItemScript +
            """
            const { createMenu } = await load('menu');
            // A menu with its submenu open: the submenu's surface sits inside the parent's.
            const tree = (prefix, dir) =>
                '<div id="' + prefix + '-root" dir="' + dir + '" role="menu" tabindex="-1" data-bz-menu-content style="width:160px">' +
                item(prefix + '-one', 'One') +
                item(prefix + '-more', 'More', 'aria-haspopup="menu" aria-expanded="true" aria-controls="' + prefix + '-sub" data-sub-anchor="' + prefix + '"') +
                '<div id="' + prefix + '-sub" role="menu" tabindex="-1" data-bz-menu-content style="width:160px">' +
                item(prefix + '-sub-one', 'Sub one') + item(prefix + '-sub-two', 'Sub two') + '</div>' +
                item(prefix + '-two', 'Two') + '</div>';
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white;display:flex;gap:20px';
            host.innerHTML = tree('ms', 'ltr') + tree('mr', 'rtl') + '<button id="ms-outside">outside</button>';
            document.body.append(host);
            window.__openSubmenu = async prefix => {
                const options = { typeaheadTimeout: 400, loop: true };
                const root = document.getElementById(prefix + '-root');
                createMenu(root, { ...options, initialFocus: 'none', isSubmenu: false, triggerSelector: '#ms-outside' }, null);
                const ref = recorder();
                const menu = createMenu(document.getElementById(prefix + '-sub'),
                    { ...options, initialFocus: 'first', isSubmenu: true, triggerSelector: '[data-sub-anchor=' + prefix + ']' }, ref);
                // The parent's own handlers (Escape, Tab, the arrow that opens a submenu) sit on its
                // surface: these are the keys that still reach them.
                const reached = [];
                root.addEventListener('keydown', e => reached.push(e.key));
                await sleep(60);
                return { calls: ref.calls, reached, menu };
            };
            window.__ms = await window.__openSubmenu('ms');
            return true;
            """);
        Task<string[]> ReachedParent(string tree) => page.EvaluateAsync<string[]>("name => window[name].reached.splice(0)", "__" + tree);

        // Opened by the keyboard, focus is on its first item and nothing asks it to close.
        Assert.Equal("ms-sub-one", await ActiveIdAsync(page));
        Assert.Equal("[]", await TakeCallsAsync(page, "__ms"));

        // Its own arrows stay its own: the parent never sees them, so it cannot move as well.
        Same(["ms-sub-two"], await FocusAfterEachKeyAsync(page, "ArrowDown"));
        Same([], await ReachedParent("ms"));
        // The arrow that would open a further submenu is left for the item's own handler.
        Same(["ms-sub-two"], await FocusAfterEachKeyAsync(page, "ArrowRight"));
        Same(["ArrowRight"], await ReachedParent("ms"));
        Assert.Equal("[]", await TakeCallsAsync(page, "__ms"));

        // The arrow towards the start closes it: focus is on its trigger before anything re-renders,
        // one close is asked for, and the key stops there (it would close every level above too).
        Same(["ms-more"], await FocusAfterEachKeyAsync(page, "ArrowLeft"));
        Assert.Equal("""[["OnSubCloseRequested"]]""", await TakeCallsAsync(page, "__ms"));
        Same([], await ReachedParent("ms"));

        // Focus going back to the trigger keeps it open (the pointer returned to it). A sibling in
        // the parent menu, or anything else on the page, closes it.
        await page.FocusAsync("#ms-sub-two");
        await page.FocusAsync("#ms-more");
        Assert.Equal("[]", await TakeCallsAsync(page, "__ms"));
        await page.FocusAsync("#ms-two");
        Assert.Equal("""[["OnSubCloseRequested"]]""", await TakeCallsAsync(page, "__ms"));
        await page.FocusAsync("#ms-sub-one");
        await page.FocusAsync("#ms-outside");
        Assert.Equal("""[["OnSubCloseRequested"]]""", await TakeCallsAsync(page, "__ms"));

        // Right to left the start is on the right: the left arrow is the one that passes through.
        await page.EvaluateAsync("async () => { window.__mr = await window.__openSubmenu('mr'); }");
        Assert.Equal("mr-sub-one", await ActiveIdAsync(page));
        Same(["mr-sub-one"], await FocusAfterEachKeyAsync(page, "ArrowLeft"));
        Same(["ArrowLeft"], await ReachedParent("mr"));
        Assert.Equal("[]", await TakeCallsAsync(page, "__mr"));
        Same(["mr-more"], await FocusAfterEachKeyAsync(page, "ArrowRight"));
        Same([], await ReachedParent("mr"));
        Assert.Equal("""[["OnSubCloseRequested"]]""", await TakeCallsAsync(page, "__mr"));

        // Disposed: neither the key nor focus leaving asks for anything.
        await page.EvaluateAsync("() => window.__ms.menu.dispose()");
        await TakeCallsAsync(page, "__ms");
        await page.FocusAsync("#ms-sub-one");
        Same(["ms-sub-one"], await FocusAfterEachKeyAsync(page, "ArrowLeft"));
        await page.FocusAsync("#ms-two");
        Assert.Equal("[]", await TakeCallsAsync(page, "__ms"));
    }

    [E2EFact]
    public async Task Menu_highlight_follows_the_mouse_and_holds_still_on_the_way_to_an_open_submenu()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, MenuItemScript +
            """
            const { createMenu } = await load('menu');
            const host = document.createElement('div');
            // Rows of 30 px from y = 210: One, More, a label, a disabled item, Two, Three. The open
            // submenu is to the right of the menu, from x = 300 and y = 230 to 380.
            host.innerHTML =
                '<div id="mp-root" role="menu" tabindex="-1" data-bz-menu-content style="position:fixed;z-index:99999;left:100px;top:200px;width:200px;padding:10px 0;background:white">' +
                item('mp-one', 'One') +
                item('mp-more', 'More', 'aria-haspopup="menu" aria-expanded="true" aria-controls="mp-sub"') +
                '<div id="mp-label" style="height:30px">Label</div>' +
                item('mp-off', 'Off', 'aria-disabled="true" data-disabled') +
                item('mp-two', 'Two') + item('mp-three', 'Three') +
                '<div id="mp-sub" role="menu" tabindex="-1" data-bz-menu-content data-side="right" style="position:fixed;left:300px;top:230px;width:160px;background:white">' +
                ['one', 'two', 'three', 'four', 'five'].map(name => item('mp-sub-' + name, 'Sub ' + name)).join('') + '</div>' +
                '</div>';
            document.body.append(host);
            const options = { initialFocus: 'none', typeaheadTimeout: 400, loop: true };
            window.__mp = createMenu(document.getElementById('mp-root'), { ...options, isSubmenu: false, triggerSelector: '' }, null);
            window.__mpSub = recorder();
            window.__mpSubMenu = createMenu(document.getElementById('mp-sub'), { ...options, isSubmenu: true, triggerSelector: '#mp-more' }, window.__mpSub);
            document.getElementById('mp-root').focus();
            await sleep(60);
            window.__mpSub.calls.length = 0;
            return true;
            """);
        Task FocusIs(string id) =>
            page.WaitForFunctionAsync("id => document.activeElement?.id === id", id, new() { Timeout = 5_000 });

        // The highlight is focus, and it is on the item under the pointer.
        await page.Mouse.MoveAsync(150, 225);
        Assert.Equal("mp-one", await ActiveIdAsync(page));
        await page.Mouse.MoveAsync(150, 345);
        Assert.Equal("mp-two", await ActiveIdAsync(page));
        // Resting on something that cannot be chosen (a disabled item, a label) takes it off: back
        // to the surface, a frame later, so that crossing a label on the way to an item never blinks.
        await page.Mouse.MoveAsync(150, 315);
        await FocusIs("mp-root");
        await page.Mouse.MoveAsync(150, 375);
        Assert.Equal("mp-three", await ActiveIdAsync(page));
        await page.Mouse.MoveAsync(150, 285);
        await FocusIs("mp-root");
        // Leaving the menu altogether takes it off at once.
        await page.Mouse.MoveAsync(150, 345);
        await page.Mouse.MoveAsync(600, 600);
        Assert.Equal("mp-root", await ActiveIdAsync(page));

        // From the submenu's trigger towards the submenu the pointer crosses the item below the
        // trigger. Heading that way, inside the triangle between where it left the trigger and the
        // submenu's near edge, the highlight stays on the trigger: the submenu is not asked to close.
        await page.Mouse.MoveAsync(150, 255);
        Assert.Equal("mp-more", await ActiveIdAsync(page));
        await TakeCallsAsync(page, "__mpSub");
        await page.Mouse.MoveAsync(220, 280);
        await page.Mouse.MoveAsync(280, 335); // over "Two"
        Assert.Equal("mp-more", await ActiveIdAsync(page));
        Assert.Equal("[]", await TakeCallsAsync(page, "__mpSub"));
        // Arrived: the submenu's own item takes the highlight.
        await page.Mouse.MoveAsync(320, 335);
        Assert.Equal("mp-sub-four", await ActiveIdAsync(page));
        Assert.Equal("[]", await TakeCallsAsync(page, "__mpSub"));

        // Moving away from the submenu instead is an ordinary move: the item below gets the
        // highlight, and with focus gone to a sibling the submenu asks to be closed.
        await page.Mouse.MoveAsync(250, 255);
        Assert.Equal("mp-more", await ActiveIdAsync(page));
        await page.Mouse.MoveAsync(200, 290);
        await page.Mouse.MoveAsync(150, 345);
        Assert.Equal("mp-two", await ActiveIdAsync(page));
        Assert.Contains("OnSubCloseRequested", await TakeCallsAsync(page, "__mpSub"));

        // A pointer parked half way does not hold the highlight for good: after 300 ms the next
        // movement is an ordinary one again.
        await page.Mouse.MoveAsync(150, 255);
        await TakeCallsAsync(page, "__mpSub");
        await page.Mouse.MoveAsync(220, 280);
        await page.Mouse.MoveAsync(280, 335);
        Assert.Equal("mp-more", await ActiveIdAsync(page));
        await page.WaitForTimeoutAsync(400);
        await page.Mouse.MoveAsync(282, 336);
        Assert.Equal("mp-two", await ActiveIdAsync(page));
        Assert.Equal("""[["OnSubCloseRequested"]]""", await TakeCallsAsync(page, "__mpSub"));

        // A finger does not hover: its movement leaves the highlight where the keyboard put it.
        await page.Mouse.MoveAsync(600, 600);
        await page.FocusAsync("#mp-one");
        await page.EvaluateAsync(
            "() => document.getElementById('mp-three').dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerType: 'touch', clientX: 150, clientY: 375 }))");
        Assert.Equal("mp-one", await ActiveIdAsync(page));

        // Disposed: the pointer moves nothing.
        await page.EvaluateAsync("() => { window.__mp.dispose(); window.__mpSubMenu.dispose(); }");
        await page.Mouse.MoveAsync(150, 345);
        Assert.Equal("mp-one", await ActiveIdAsync(page));
    }

    [E2EFact]
    public async Task Menu_in_a_menubar_steps_to_the_neighbouring_menu_with_the_inline_arrows()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, MenuItemScript +
            """
            const { createMenu } = await load('menu');
            // A bar of three menus with the middle one open, a submenu open inside it.
            const bar = (prefix, attributes) =>
                '<div id="' + prefix + '-bar" role="menubar" data-bz-menubar ' + attributes + ' style="display:flex;gap:4px">' +
                ['file', 'edit', 'view'].map(name => '<button id="' + prefix + '-' + name + '" data-bz-dropdown-menu-anchor="' + prefix + '-' + name +
                    '" aria-expanded="' + (name === 'edit') + '">' + name + '</button>').join('') +
                '<div id="' + prefix + '-menu" role="menu" tabindex="-1" data-bz-menu-content style="width:120px">' +
                item(prefix + '-undo', 'Undo') +
                item(prefix + '-more', 'More', 'aria-haspopup="menu" aria-expanded="true" aria-controls="' + prefix + '-sub"') +
                '<div id="' + prefix + '-sub" role="menu" tabindex="-1" data-bz-menu-content style="width:120px">' + item(prefix + '-sub-one', 'Sub one') + '</div>' +
                '</div></div>';
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white;display:flex;gap:30px';
            host.innerHTML = bar('mb', 'data-bz-menubar-loop="true"') + bar('me', 'data-bz-menubar-loop="false"') + bar('mt', 'dir="rtl" data-bz-menubar-loop="true"');
            document.body.append(host);
            // What the bar does with a click on a trigger: that menu becomes the open one. A click
            // made by a key has no pointer detail, which is how the trigger knows to focus the first item.
            window.__barClicks = [];
            host.addEventListener('click', e => {
                const trigger = e.target.closest('[data-bz-dropdown-menu-anchor]');
                if (!trigger) return;
                window.__barClicks.push(trigger.id + ':' + e.detail);
                for (const other of trigger.parentElement.querySelectorAll('[data-bz-dropdown-menu-anchor]'))
                    other.setAttribute('aria-expanded', String(other === trigger));
            });
            // The keys the menus did not take.
            window.__barKeys = [];
            host.addEventListener('keydown', e => window.__barKeys.push(e.key));
            const options = { initialFocus: 'none', typeaheadTimeout: 400, loop: true };
            for (const prefix of ['mb', 'me', 'mt']) {
                createMenu(document.getElementById(prefix + '-menu'), { ...options, isSubmenu: false, triggerSelector: '#' + prefix + '-edit' }, null);
                createMenu(document.getElementById(prefix + '-sub'), { ...options, isSubmenu: true, triggerSelector: '#' + prefix + '-more' }, recorder());
            }
            await sleep(60);
            return true;
            """);
        async Task<string> Step(string key)
        {
            await page.Keyboard.PressAsync(key);
            return string.Join(",", await DrainAsync(page, "__barClicks")) + "|" + string.Join(",", await DrainAsync(page, "__barKeys"));
        }

        // The arrow towards the end opens the next menu, the other one the previous; a bar that
        // loops goes round. The key is used up: nothing else gets it.
        await page.FocusAsync("#mb-undo");
        Assert.Equal("mb-view:0|", await Step("ArrowRight"));
        Assert.Equal("mb-file:0|", await Step("ArrowRight"));
        Assert.Equal("mb-view:0|", await Step("ArrowLeft"));

        // On an item that opens a submenu the arrow towards the end is that item's own; the other
        // one still steps along the bar.
        await page.FocusAsync("#mb-more");
        Assert.Equal("|ArrowRight", await Step("ArrowRight"));
        Assert.Equal("mb-edit:0|", await Step("ArrowLeft"));

        // Inside a submenu the arrow towards the start closes the submenu and nothing more; the
        // arrow towards the end, on an item with no submenu of its own, steps along the bar.
        await page.FocusAsync("#mb-sub-one");
        Assert.Equal("|", await Step("ArrowLeft"));
        Assert.Equal("mb-more", await ActiveIdAsync(page));
        await page.FocusAsync("#mb-sub-one");
        Assert.Equal("mb-view:0|", await Step("ArrowRight"));

        // A bar that does not loop has nowhere to go past its last menu, and leaves the key alone.
        await page.EvaluateAsync("() => document.getElementById('me-view').click()");
        await DrainAsync(page, "__barClicks");
        await page.FocusAsync("#me-undo");
        Assert.Equal("|ArrowRight", await Step("ArrowRight"));
        Assert.Equal("me-edit:0|", await Step("ArrowLeft"));

        // Right to left the next menu is to the left.
        await page.FocusAsync("#mt-undo");
        Assert.Equal("mt-view:0|", await Step("ArrowLeft"));
        Assert.Equal("mt-edit:0|", await Step("ArrowRight"));

        // With no menu of the bar marked open there is no neighbour to work out.
        await page.EvaluateAsync(
            "() => { for (const trigger of document.querySelectorAll('#mb-bar [data-bz-dropdown-menu-anchor]')) trigger.setAttribute('aria-expanded', 'false'); }");
        await page.FocusAsync("#mb-undo");
        Assert.Equal("|ArrowRight", await Step("ArrowRight"));
    }

    [E2EFact]
    public async Task Menu_trigger_keeps_its_arrows_from_scrolling_and_gets_focus_back_only_when_it_is_stranded()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { guardTrigger, focusTrigger } = await load('menu');
            window.__focusTrigger = focusTrigger;
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white';
            host.innerHTML = '<button id="mg-trigger" data-anchor="mg">Open</button><button id="mg-other">Other</button><input id="mg-field" />' +
                '<div id="mg-surface" tabindex="-1"><button id="mg-inside">inside</button></div>';
            document.body.append(host);
            window.__guard = guardTrigger('[data-anchor=mg]');
            // No such trigger: a handle that does nothing.
            guardTrigger('#mg-no-such-trigger').dispose();
            window.__guardKeys = [];
            window.addEventListener('keydown', e => window.__guardKeys.push(e.key + ':' + e.defaultPrevented));
            return true;
            """);

        // ArrowDown and ArrowUp open the menu from its trigger (the component's own handler does
        // that): here they only lose the default that would scroll the page first. Other keys and
        // shortcuts keep theirs.
        await page.FocusAsync("#mg-trigger");
        foreach (var key in new[] { "ArrowDown", "ArrowUp", "ArrowLeft", "Enter", "Control+ArrowDown", "Alt+ArrowUp" })
            await page.Keyboard.PressAsync(key);
        Same(["ArrowDown:true", "ArrowUp:true", "ArrowLeft:false", "Enter:false", "ArrowDown:false", "ArrowUp:false"],
            WithoutModifierKeys(await DrainAsync(page, "__guardKeys")));
        // Only the trigger is guarded.
        await page.FocusAsync("#mg-other");
        await page.Keyboard.PressAsync("ArrowDown");
        Same(["ArrowDown:false"], await DrainAsync(page, "__guardKeys"));
        await page.EvaluateAsync("() => window.__guard.dispose()");
        await page.FocusAsync("#mg-trigger");
        await page.Keyboard.PressAsync("ArrowDown");
        Same(["ArrowDown:false"], await DrainAsync(page, "__guardKeys"));

        // focusTrigger(selector, onlyIfStranded, within), from each place focus can be when a menu closes.
        var r = await RunAsync<JsonElement>(page,
            """
            const focusTrigger = window.__focusTrigger;
            const from = id => document.getElementById(id).focus();
            const active = () => document.activeElement?.id ?? '';
            const surface = document.getElementById('mg-surface');
            const result = {};
            from('mg-field'); focusTrigger('[data-anchor=mg]'); result.always = active();
            from('mg-field'); focusTrigger('[data-anchor=mg]', true); result.userMovedOn = active();
            document.activeElement.blur(); focusTrigger('[data-anchor=mg]', true); result.nowhere = active();
            from('mg-inside'); focusTrigger('[data-anchor=mg]', true); result.insideUnnamed = active();
            from('mg-inside'); focusTrigger('[data-anchor=mg]', true, surface); result.insideClosing = active();
            from('mg-field'); focusTrigger('[data-anchor=mg]', true, surface); result.userMovedOnWhileClosing = active();
            from('mg-field'); focusTrigger('#mg-no-such-trigger'); result.triggerGone = active();
            return result;
            """);
        string At(string name) => r.GetProperty(name).GetString()!;

        // Escape on a menubar menu: straight back to the trigger, wherever focus was.
        Assert.Equal("mg-trigger", At("always"));
        // Stranded only: the press that closed the menu put focus in a field, and it stays there.
        Assert.Equal("mg-field", At("userMovedOn"));
        Assert.Equal("mg-field", At("userMovedOnWhileClosing"));
        // Focus fell to the page with the removed surface, or is still inside the surface that is
        // closing: both are stranded, and the trigger takes it.
        Assert.Equal("mg-trigger", At("nowhere"));
        Assert.Equal("mg-trigger", At("insideClosing"));
        // Inside some other surface nobody said is closing, it is where the user is.
        Assert.Equal("mg-inside", At("insideUnnamed"));
        // The whole menu is gone, trigger included: nothing to do, and no error.
        Assert.Equal("mg-field", At("triggerGone"));
    }

    [E2EFact]
    public async Task Navigation_menu_arrows_walk_the_bar_and_the_open_panel()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, NavigationMenuMarkup +
            """
            window.__navMenu = createNavMenu(document.getElementById('nv'), window.__nav);
            createNavMenu(document.getElementById('nr'), recorder());
            window.__navKeys = [];
            window.addEventListener('keydown', e => window.__navKeys.push(e.key + ':' + e.defaultPrevented));
            return true;
            """);

        // Along the bar: left and right, Home and End, stopping at the ends. The triggers and the
        // plain link are the stops; up is not a direction of the bar.
        await page.FocusAsync("#nv-products");
        Same(["nv-company", "nv-pricing", "nv-pricing", "nv-company", "nv-products", "nv-products", "nv-pricing", "nv-pricing"],
            await FocusAfterEachKeyAsync(page, "ArrowRight", "ArrowRight", "ArrowRight", "ArrowLeft", "Home", "ArrowLeft", "End", "ArrowUp"));
        Same(["ArrowRight:true", "ArrowRight:true", "ArrowRight:true", "ArrowLeft:true", "Home:true", "ArrowLeft:true", "End:true", "ArrowUp:false"],
            await DrainAsync(page, "__navKeys"));
        // Right to left the same keys go the other way.
        await page.FocusAsync("#nr-products");
        Same(["nr-company", "nr-pricing", "nr-company"], await FocusAfterEachKeyAsync(page, "ArrowLeft", "ArrowLeft", "ArrowRight"));
        await DrainAsync(page, "__navKeys");

        // ArrowDown on a closed trigger presses it and waits for its panel, which the component
        // renders a moment later; focus then goes to the first thing in it that can take focus
        // (a lead card that is a link without an address cannot).
        await page.FocusAsync("#nv-company");
        await page.Keyboard.PressAsync("ArrowDown");
        Same(["nv-company"], await DrainAsync(page, "__navClicks"));
        Assert.Equal("nv-company", await ActiveIdAsync(page));
        await page.WaitForTimeoutAsync(80);
        await page.EvaluateAsync("() => window.__navOpen('nv', 'company', window.__navCompanyPanel)");
        await page.WaitForFunctionAsync("() => document.activeElement?.id === 'nv-a'", null, new() { Timeout = 5_000 });
        Same(["ArrowDown:true"], await DrainAsync(page, "__navKeys"));

        // In the panel all four arrows walk its links and buttons and go round at the ends, so
        // focus stays inside; Home and End go to the first and the last. An element out of the tab
        // order is not a stop.
        Same(["nv-b", "nv-c", "nv-a", "nv-c", "nv-a", "nv-c", "nv-a", "nv-c"],
            await FocusAfterEachKeyAsync(page, "ArrowDown", "ArrowDown", "ArrowDown", "ArrowUp", "ArrowRight", "ArrowLeft", "Home", "End"));
        Assert.All(await DrainAsync(page, "__navKeys"), key => Assert.EndsWith(":true", key));

        // Escape closes the panel (the component does that): focus is put back on its trigger
        // first, so it is not lost with the panel. The key itself is left for the component.
        Same(["nv-company"], await FocusAfterEachKeyAsync(page, "Escape"));
        Same(["Escape:false"], await DrainAsync(page, "__navKeys"));

        // On a trigger that is already open ArrowDown only goes in.
        Same(["nv-a"], await FocusAfterEachKeyAsync(page, "ArrowDown"));
        Same([], await DrainAsync(page, "__navClicks"));

        // A panel part that runs its own keyboard (a list that moves a selection, not focus) keeps
        // focus where it is; its arrows still do not scroll the page.
        await page.EvaluateAsync("() => window.__navOpen('nv', 'products', window.__navProductsPanel)");
        await page.FocusAsync("#nv-own-keys");
        await DrainAsync(page, "__navKeys");
        Same(["nv-own-keys", "nv-own-keys"], await FocusAfterEachKeyAsync(page, "ArrowDown", "End"));
        Same(["ArrowDown:true", "End:true"], await DrainAsync(page, "__navKeys"));

        // Disposed: the keys are the page's again.
        await page.EvaluateAsync("() => window.__navMenu.dispose()");
        await page.FocusAsync("#nv-products");
        Same(["nv-products"], await FocusAfterEachKeyAsync(page, "ArrowRight"));
        Same(["ArrowRight:false"], await DrainAsync(page, "__navKeys"));
    }

    // Two navigation menus (one right to left) as the component renders them closed, and
    // __navOpen(menu, item, panel), which changes the markup the way the component does when an
    // item opens: the trigger says so, and the shared viewport holds that item's content.
    private const string NavigationMenuMarkup =
        """
        const { createNavMenu } = await load('navigationMenu');
        const host = document.createElement('div');
        host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white';
        // Triggers 90 and 110 px wide, 10 px apart.
        const menu = (prefix, dir) =>
            '<div id="' + prefix + '" dir="' + dir + '" data-slot="navigation-menu" style="position:relative;width:400px;height:260px">' +
            '<ul data-slot="navigation-menu-list" style="display:flex;gap:10px;list-style:none;margin:0;padding:0">' +
            '<li data-slot="navigation-menu-item"><button id="' + prefix + '-products" data-slot="navigation-menu-trigger" data-state="closed" style="width:90px">Products</button></li>' +
            '<li data-slot="navigation-menu-item"><button id="' + prefix + '-company" data-slot="navigation-menu-trigger" data-state="closed" style="width:110px">Company</button></li>' +
            '<li data-slot="navigation-menu-item"><a id="' + prefix + '-pricing" data-slot="navigation-menu-link" href="#pricing">Pricing</a></li>' +
            '</ul>' +
            '<div data-slot="navigation-menu-arrow" data-state="hidden"></div>' +
            '<div id="' + prefix + '-viewport-home"></div></div>';
        host.innerHTML = menu('nv', 'ltr') + menu('nr', 'rtl') + '<button id="nv-outside">outside</button>';
        document.body.append(host);
        window.__navOpen = (prefix, name, panel) => {
            for (const trigger of document.querySelectorAll('#' + prefix + ' [data-slot=navigation-menu-trigger]'))
                trigger.setAttribute('data-state', trigger.id === prefix + '-' + name ? 'open' : 'closed');
            document.getElementById(prefix + '-viewport-home').innerHTML = name === null ? '' :
                '<div data-slot="navigation-menu-viewport" data-state="open"><div>' +
                '<div data-slot="navigation-menu-content" data-state="open" style="position:absolute;' + panel.size + '">' + panel.body + '</div></div></div>';
        };
        window.__navCompanyPanel = { size: 'width:300px;height:120px', body:
            '<a id="nv-lead">no address</a><a id="nv-a" href="#a">A</a><button id="nv-b">B</button>' +
            '<span id="nv-skipped" tabindex="-1">skipped</span><span id="nv-c" tabindex="0">C</span>' };
        window.__navProductsPanel = { size: 'width:240px;height:80px', body:
            '<a id="nv-p" href="#p">P</a><div data-nav-manual><button id="nv-own-keys">own keys</button></div>' };
        window.__nav = recorder();
        window.__navClicks = [];
        host.addEventListener('click', e => { if (e.target.closest('[data-slot=navigation-menu-trigger]')) window.__navClicks.push(e.target.id); });
        """;

    [E2EFact]
    public async Task Navigation_menu_sizes_its_viewport_points_its_arrow_and_asks_to_close_on_a_press_outside()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, NavigationMenuMarkup +
            """
            window.__navMenu = createNavMenu(document.getElementById('nv'), window.__nav);
            // [viewport width, viewport height, arrow state, arrow offset, arrow width]
            window.__navState = () => {
                const viewport = document.querySelector('#nv [data-slot=navigation-menu-viewport]');
                const arrow = document.querySelector('#nv [data-slot=navigation-menu-arrow]');
                return [viewport?.style.getPropertyValue('--bz-nav-vw') ?? '', viewport?.style.getPropertyValue('--bz-nav-vh') ?? '',
                    arrow.getAttribute('data-state'), arrow.style.getPropertyValue('--bz-nav-ind-left'), arrow.style.getPropertyValue('--bz-nav-ind-width')].join(' ');
            };
            return true;
            """);
        Task StateIs(string state) =>
            page.WaitForFunctionAsync("state => window.__navState() === state", state, new() { Timeout = 5_000 });

        // Nothing open: no arrow, and a press outside has nothing to close.
        Assert.Equal("  hidden  ", await page.EvaluateAsync<string>("() => window.__navState()"));
        await page.ClickAsync("#nv-outside");
        Assert.Equal("[]", await TakeCallsAsync(page, "__nav"));

        // An item opens: the viewport is given the size of that item's content, and the arrow the
        // place and width of its trigger (the second one: 90 px and a 10 px gap before it).
        await page.EvaluateAsync("() => window.__navOpen('nv', 'company', window.__navCompanyPanel)");
        await StateIs("300px 120px visible 100px 110px");
        // Content that changes size afterwards is measured again without anyone asking.
        await page.EvaluateAsync("() => { document.querySelector('#nv [data-slot=navigation-menu-content]').style.height = '200px'; }");
        await StateIs("300px 200px visible 100px 110px");
        // Another item: both follow.
        await page.EvaluateAsync("() => window.__navOpen('nv', 'products', window.__navProductsPanel)");
        await StateIs("240px 80px visible 0px 90px");

        // A press inside the menu (its bar or its panel) is not one outside it.
        await page.ClickAsync("#nv-p");
        await page.ClickAsync("#nv-company");
        Assert.Equal("[]", await TakeCallsAsync(page, "__nav"));
        await page.ClickAsync("#nv-outside");
        Assert.Equal("""[["CloseFromOutside"]]""", await TakeCallsAsync(page, "__nav"));

        // Closed again: the arrow hides.
        await page.EvaluateAsync("() => window.__navOpen('nv', null)");
        await page.WaitForFunctionAsync("() => window.__navState().includes('hidden')", null, new() { Timeout = 5_000 });

        // Disposed: an item opening is not measured, and a press outside is not reported.
        await page.EvaluateAsync("() => { window.__navMenu.dispose(); window.__navOpen('nv', 'company', window.__navCompanyPanel); }");
        await page.WaitForTimeoutAsync(200);
        await page.ClickAsync("#nv-outside");
        Assert.Equal("[]", await TakeCallsAsync(page, "__nav"));
        Assert.StartsWith("  hidden", await page.EvaluateAsync<string>("() => window.__navState()"));
    }

    [E2EFact]
    public async Task Roving_focus_moves_between_the_items_of_a_group_with_the_arrow_keys()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, RovingItemScript +
            """
            const { createRovingFocus } = await load('rovingFocus');
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white';
            host.innerHTML =
                // b is disabled, d is marked disabled, e is disabled but may still be reached (a
                // toolbar button that says why it is off); the span is inside the group, not of it.
                '<div id="rf">' + button('rf-a') + button('rf-b', 'disabled') + button('rf-c') +
                button('rf-d', 'aria-disabled="true" data-disabled') + button('rf-e', 'aria-disabled="true" data-focusable') +
                '<span id="rf-plain" tabindex="0">not an item</span>' + button('rf-f') + '</div>' +
                '<div id="rv" style="display:flex;flex-direction:column;width:100px">' + button('rv-a') + button('rv-b') + button('rv-c') + '</div>' +
                '<div id="rr" dir="rtl">' + button('rr-a') + button('rr-b') + button('rr-c') + '</div>' +
                '<div id="rt">' + button('rt-a') + '<input id="rt-field" data-bz-roving-item value="abc" style="width:60px" />' +
                '<input id="rt-number" type="number" data-bz-roving-item value="5" style="width:60px" />' +
                '<textarea id="rt-area" data-bz-roving-item rows="1" style="width:60px">xy</textarea>' + button('rt-z') + '</div>';
            document.body.append(host);
            window.__rfClicks = [];
            host.addEventListener('click', e => window.__rfClicks.push(e.target.id));
            window.__rfKeys = [];
            window.addEventListener('keydown', e => window.__rfKeys.push(e.key + ':' + e.defaultPrevented));
            window.__rf = createRovingFocus(document.getElementById('rf'), { orientation: 'horizontal', dir: 'ltr', loop: true, selectOnFocus: false });
            createRovingFocus(document.getElementById('rv'), { orientation: 'vertical', dir: 'ltr', loop: false, selectOnFocus: true });
            // Told "left to right" when it was made: the direction that counts is the one on the page.
            createRovingFocus(document.getElementById('rr'), { orientation: 'both', dir: 'ltr', loop: false, selectOnFocus: false });
            createRovingFocus(document.getElementById('rt'), { orientation: 'horizontal', dir: 'ltr', loop: false, selectOnFocus: false });
            return true;
            """);

        // A horizontal group that loops: disabled items are passed over, the one that stays
        // reachable is not, and the ends join up. Home, End and the Page keys go to the ends.
        await page.FocusAsync("#rf-c");
        Same(["rf-e", "rf-f", "rf-a", "rf-f", "rf-e", "rf-f", "rf-a", "rf-f", "rf-a"],
            await FocusAfterEachKeyAsync(page, "ArrowRight", "ArrowRight", "ArrowRight", "ArrowLeft", "ArrowLeft", "End", "Home", "PageDown", "PageUp"));
        // The other axis, and any key held with a modifier, are not this group's: they keep their default.
        Same(["rf-a", "rf-a", "rf-a"], await FocusAfterEachKeyAsync(page, "ArrowDown", "Shift+ArrowRight", "Control+ArrowRight"));
        Same(["ArrowRight:true", "ArrowRight:true", "ArrowRight:true", "ArrowLeft:true", "ArrowLeft:true", "End:true", "Home:true", "PageDown:true", "PageUp:true",
            "ArrowDown:false", "ArrowRight:false", "ArrowRight:false"], WithoutModifierKeys(await DrainAsync(page, "__rfKeys")));
        // Moving focus is not choosing, unless the group says so.
        Same([], await DrainAsync(page, "__rfClicks"));
        // A key from something that merely sits inside the group is left alone.
        await page.FocusAsync("#rf-plain");
        Same(["rf-plain"], await FocusAfterEachKeyAsync(page, "ArrowRight"));
        Same(["ArrowRight:false"], await DrainAsync(page, "__rfKeys"));

        // Vertical, not looping, selection follows focus (a radio group): up and down only, a stop
        // at each end, and a click on every item focus actually moved to.
        await page.FocusAsync("#rv-a");
        Same(["rv-b", "rv-c", "rv-c", "rv-c", "rv-b", "rv-a"],
            await FocusAfterEachKeyAsync(page, "ArrowDown", "ArrowDown", "ArrowDown", "ArrowRight", "ArrowUp", "Home"));
        Same(["rv-b", "rv-c", "rv-b", "rv-a"], await DrainAsync(page, "__rfClicks"));

        // Right to left, left is forwards. Up and down do not turn round.
        await page.FocusAsync("#rr-a");
        Same(["rr-b", "rr-c", "rr-c", "rr-b", "rr-c", "rr-b"],
            await FocusAfterEachKeyAsync(page, "ArrowLeft", "ArrowLeft", "ArrowLeft", "ArrowRight", "ArrowDown", "ArrowUp"));
        // The direction changes while the page is up (a language switch): the very next key follows it.
        await page.EvaluateAsync("() => document.getElementById('rr').setAttribute('dir', 'ltr')");
        await page.FocusAsync("#rr-a");
        Same(["rr-b"], await FocusAfterEachKeyAsync(page, "ArrowRight"));
        await DrainAsync(page, "__rfKeys");

        // A text field in the group shares its arrows with the caret: while the caret can move the
        // key is the text's, and Home and End always are.
        await page.FocusAsync("#rt-field");
        await page.EvaluateAsync("() => document.getElementById('rt-field').setSelectionRange(1, 1)");
        Same(["rt-field", "rt-field", "rt-field", "rt-field"], await FocusAfterEachKeyAsync(page, "ArrowRight", "ArrowLeft", "Home", "End"));
        Same(["ArrowRight:false", "ArrowLeft:false", "Home:false", "End:false"], await DrainAsync(page, "__rfKeys"));
        // With the caret at the end (End put it there) the same key walks out to the next item.
        Same(["rt-number"], await FocusAfterEachKeyAsync(page, "ArrowRight"));
        // A selection is the caret's too; at the very start the arrow walks out the other way.
        await page.FocusAsync("#rt-field");
        await page.EvaluateAsync("() => document.getElementById('rt-field').setSelectionRange(0, 3)");
        Same(["rt-field"], await FocusAfterEachKeyAsync(page, "ArrowRight"));
        await page.EvaluateAsync("() => document.getElementById('rt-field').setSelectionRange(0, 0)");
        Same(["rt-a"], await FocusAfterEachKeyAsync(page, "ArrowLeft"));
        // A number field has no caret position to ask about, so it never holds the arrows; an
        // editor of several lines needs all of them and keeps them.
        await page.FocusAsync("#rt-number");
        Same(["rt-area", "rt-area", "rt-area", "rt-area"], await FocusAfterEachKeyAsync(page, "ArrowRight", "ArrowRight", "ArrowRight", "ArrowLeft"));

        // Disposed: the arrows move nothing.
        await page.EvaluateAsync("() => window.__rf.dispose()");
        await page.FocusAsync("#rf-a");
        Same(["rf-a"], await FocusAfterEachKeyAsync(page, "ArrowRight"));
    }

    private const string RovingItemScript =
        """
        const button = (id, extra = '') => '<button id="' + id + '" data-bz-roving-item ' + extra + '>' + id + '</button>';
        // The tab index of every item of a group, by the last part of its id: "a=0 b= c=-1".
        window.__tabStops = group => [...document.querySelectorAll('#' + group + ' [data-bz-roving-item]')]
            .map(el => el.id.split('-')[1] + '=' + (el.getAttribute('tabindex') ?? '')).join(' ');
        """;

    [E2EFact]
    public async Task Roving_focus_keeps_exactly_one_item_in_the_tab_order()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, RovingItemScript +
            """
            const { createRovingFocus } = await load('rovingFocus');
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white';
            host.innerHTML =
                '<div id="rs">' + button('rs-a') + button('rs-b', 'disabled') + button('rs-c', 'data-roving-active') +
                button('rs-d', 'aria-disabled="true" data-focusable') + button('rs-e') + '</div>' +
                '<div id="rp">' + button('rp-a') + button('rp-b') + '</div>' +
                // A group in a closed panel, its first item one that is never drawn.
                '<div id="rh" style="display:none">' + button('rh-a', 'style="display:none"') + button('rh-b') + button('rh-c') + '</div>' +
                '<div id="rn"></div>';
            document.body.append(host);
            const options = { orientation: 'horizontal', dir: 'ltr', loop: true, selectOnFocus: false };
            window.__rs = createRovingFocus(document.getElementById('rs'), options);
            createRovingFocus(document.getElementById('rp'), options);
            createRovingFocus(document.getElementById('rh'), options);
            // No items yet.
            createRovingFocus(document.getElementById('rn'), options);
            return true;
            """);
        Task<string> Stops(string group) => page.EvaluateAsync<string>("group => window.__tabStops(group)", group);
        Task StopsAre(string group, string stops) =>
            page.WaitForFunctionAsync("([group, stops]) => window.__tabStops(group) === stops", new[] { group, stops }, new() { Timeout = 5_000 });

        // One stop: the item marked as the group's current one, else the first. A disabled item is
        // not given a tab index at all; one that stays reachable while disabled is an item like any other.
        Assert.Equal("a=-1 b= c=0 d=-1 e=-1", await Stops("rs"));
        Assert.Equal("a=0 b=-1", await Stops("rp"));
        // In a group that is not shown any item will do for now.
        Assert.Equal("a=0 b=-1 c=-1", await Stops("rh"));

        // The stop follows focus however it got there: an arrow, or a click.
        await page.FocusAsync("#rs-c");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("a=-1 b= c=-1 d=0 e=-1", await Stops("rs"));
        await page.ClickAsync("#rs-a");
        Assert.Equal("a=0 b= c=-1 d=-1 e=-1", await Stops("rs"));

        // The item with the stop gets disabled (an Undo button with nothing left to undo): another
        // one takes it, or Tab could no longer get into the group.
        await page.EvaluateAsync("() => { document.getElementById('rs-a').disabled = true; }");
        await StopsAre("rs", "a=-1 b= c=0 d=-1 e=-1");
        // ... is hidden: an item that is not drawn cannot take focus.
        await page.EvaluateAsync("() => { document.getElementById('rs-c').style.display = 'none'; }");
        await StopsAre("rs", "a=-1 b= c=-1 d=0 e=-1");
        // ... is removed.
        await page.EvaluateAsync("() => document.getElementById('rs-d').remove()");
        await StopsAre("rs", "a=-1 b= c=-1 e=0");
        // An item that arrives later is taken out of the tab order like the rest.
        await page.EvaluateAsync("() => document.getElementById('rs').insertAdjacentHTML('beforeend', '<button id=\"rs-new\" data-bz-roving-item>new</button>')");
        await StopsAre("rs", "a=-1 b= c=-1 e=0 new=-1");
        // A group that starts empty gets its stop when its first items do.
        await page.EvaluateAsync("() => document.getElementById('rn').insertAdjacentHTML('beforeend', '<button id=\"rn-a\" data-bz-roving-item>a</button><button id=\"rn-b\" data-bz-roving-item>b</button>')");
        await StopsAre("rn", "a=0 b=-1");

        // The closed panel opens: the stop moves off the item that is still not drawn.
        await page.EvaluateAsync("() => { document.getElementById('rh').style.display = 'block'; }");
        await StopsAre("rh", "a=-1 b=0 c=-1");

        // Disposed: the group is left as it is.
        await page.EvaluateAsync("() => { window.__rs.dispose(); document.getElementById('rs-e').disabled = true; }");
        await page.WaitForTimeoutAsync(200);
        Assert.Equal("a=-1 b= c=-1 e=0 new=-1", await Stops("rs"));
    }

    [E2EFact]
    public async Task Toolbar_that_scrolls_says_whether_it_overflows_and_which_way_it_can_still_go()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page, ToolbarScript +
            """
            const { createScroller, createScrollerFromViewport } = await load('toolbar');
            // Ten buttons of 60 px in a row that shows five.
            host.innerHTML = bar('tb', '', 'overflow:auto;scrollbar-width:none', 10) + bar('tr', 'dir="rtl"', 'overflow:auto;scrollbar-width:none', 10) +
                bar('tf', '', 'overflow:auto;scrollbar-width:none', 3);
            document.body.append(host);
            const result = {};

            // Given the row only: the bar it belongs to is found from it.
            const view = document.getElementById('tb-view');
            const scroller = createScrollerFromViewport(view, 'scroll');
            result.start = state('tb');
            scroller.scroll('next');
            await until(() => state('tb') === 'true true true -');
            result.paged = [view.scrollLeft, state('tb')];
            scroller.scroll('next');
            await until(() => state('tb') === 'true true false -');
            result.end = [view.scrollLeft, state('tb')];
            scroller.scroll('prev');
            await until(() => state('tb') === 'true true true -');
            result.back = [view.scrollLeft, state('tb')];

            const rtlView = document.getElementById('tr-view');
            const rtl = createScroller(rtlView, document.getElementById('tr'), 'scroll');
            result.rtlStart = state('tr');
            rtl.scroll('next');
            await until(() => state('tr') === 'true true true -');
            result.rtlPaged = [rtlView.scrollLeft, state('tr')];

            const fitsView = document.getElementById('tf-view');
            const fits = createScrollerFromViewport(fitsView);
            result.fits = state('tf');
            fitsView.insertAdjacentHTML('beforeend', buttons(4));
            result.grown = await until(() => state('tf') === 'true false true -');
            fitsView.style.width = '500px';
            result.widened = await until(() => state('tf') === 'false false false -');
            fits.dispose();
            result.disposed = state('tf');
            fitsView.style.width = '100px';
            await sleep(200);
            result.disposedThenNarrowed = state('tf');
            scroller.dispose();
            rtl.dispose();
            host.remove();
            return result;
            """);
        string Json(string name) => r.GetProperty(name).GetRawText();

        // [overflowing, can go back, can go on, a revealed control is clipped (not this mode's)]:
        // at the start there is only forwards.
        Assert.Equal("\"true false true -\"", Json("start"));
        // A page is most of what shows (four fifths of 300 px), so the last control seen stays in sight.
        Assert.Equal("[240,\"true true true -\"]", Json("paged"));
        // At the far end the forward button has nowhere to go.
        Assert.Equal("[300,\"true true false -\"]", Json("end"));
        Assert.Equal("[60,\"true true true -\"]", Json("back"));
        // Right to left "next" scrolls the other way, and the answers are the same.
        Assert.Equal("\"true false true -\"", Json("rtlStart"));
        Assert.Equal("[-240,\"true true true -\"]", Json("rtlPaged"));
        // A bar that fits has no use for its buttons, until controls are added to it, or again once it is wider.
        Assert.Equal("\"false false false -\"", Json("fits"));
        Assert.True(r.GetProperty("grown").GetBoolean());
        Assert.True(r.GetProperty("widened").GetBoolean());
        // Disposed: what it published is taken off the bar, and nothing is published again.
        Assert.Equal("\"- - - -\"", Json("disposed"));
        Assert.Equal("\"- - - -\"", Json("disposedThenNarrowed"));
    }

    // A toolbar root of 300 px with its row of controls inside, what the script publishes on the
    // root as one string, and a wait for an observer to have caught up.
    private const string ToolbarScript =
        """
        const host = document.createElement('div');
        host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white';
        const buttons = n => Array.from({ length: n }, (_, i) => '<button style="flex:none;width:60px;height:30px">' + i + '</button>').join('');
        const bar = (id, attributes, style, count, before = '') =>
            '<div id="' + id + '" role="toolbar" ' + attributes + ' style="width:300px"><div id="' + id + '-view" style="display:flex;width:300px;' + style + '">' +
            before + buttons(count) + '</div></div>';
        const state = id => ['data-overflowing', 'data-can-scroll-prev', 'data-can-scroll-next', 'data-reveal-open']
            .map(name => document.getElementById(id).getAttribute(name) ?? '-').join(' ');
        const until = async (condition, ms = 3000) => {
            for (const end = performance.now() + ms; performance.now() < end; await sleep(20)) if (condition()) return true;
            return condition();
        };
        """;

    [E2EFact]
    public async Task Toolbar_that_expands_says_when_it_wraps_and_when_a_control_that_must_be_seen_is_clipped()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page, ToolbarScript +
            """
            const { createScrollerFromViewport } = await load('toolbar');
            // One row shows; what wraps onto a second one is clipped. The first child is a closed
            // popup of some control in the bar: in the row's markup, not in its layout.
            host.innerHTML = bar('tx', '', 'flex-wrap:wrap;height:30px;overflow:hidden', 3, '<div style="display:none;position:fixed;top:0">closed popup</div>');
            document.body.append(host);
            const view = document.getElementById('tx-view');
            const expand = createScrollerFromViewport(view, 'expand');
            const result = { fits: state('tx') };

            view.insertAdjacentHTML('beforeend', buttons(4));
            result.wraps = await until(() => state('tx') === 'true - - false');
            // A contextual control appears, inside a group, on the clipped row.
            view.insertAdjacentHTML('beforeend', '<span id="tx-group" style="display:flex"><button data-reveal="expand" style="width:60px;height:30px">context</button></span>');
            result.revealed = await until(() => state('tx') === 'true - - true');
            document.getElementById('tx-group').remove();
            result.released = await until(() => state('tx') === 'true - - false');
            // The same kind of control on the first row is in sight already.
            view.insertAdjacentHTML('afterbegin', '<button data-reveal="expand" style="flex:none;width:20px;height:30px">c</button>');
            await sleep(200);
            result.onFirstRow = state('tx');
            // The bar gets wide enough for one row.
            document.getElementById('tx').style.width = '700px';
            view.style.width = '700px';
            result.wide = await until(() => state('tx') === 'false - - false');
            expand.dispose();
            result.disposed = state('tx');
            host.remove();
            return result;
            """);

        // [overflowing, -, -, a revealed control is clipped]: this mode has no paging to report.
        Assert.Equal("false - - false", r.GetProperty("fits").GetString());
        Assert.True(r.GetProperty("wraps").GetBoolean());
        Assert.True(r.GetProperty("revealed").GetBoolean());
        Assert.True(r.GetProperty("released").GetBoolean());
        Assert.Equal("true - - false", r.GetProperty("onFirstRow").GetString());
        Assert.True(r.GetProperty("wide").GetBoolean());
        Assert.Equal("- - - -", r.GetProperty("disposed").GetString());
    }

    [E2EFact]
    public async Task Token_overflow_hides_the_tokens_that_do_not_fit_and_counts_them()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page,
            """
            const { createTokenOverflow } = await load('tokenOverflow');
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white';
            // Tokens of 60 px on one line, and a counter of 30 px after them.
            const tokens = n => Array.from({ length: n }, (_, i) => '<span data-bz-token style="flex:none;width:60px">t' + i + '</span>').join('');
            host.innerHTML =
                '<div id="tk" style="display:flex;flex-wrap:nowrap;overflow:hidden;width:200px">' + tokens(5) + '<span data-bz-token-more hidden style="flex:none;width:30px"></span></div>' +
                '<div id="tk-bare" style="display:flex;flex-wrap:nowrap;overflow:hidden;width:200px">' + tokens(5) + '</div>';
            document.body.append(host);
            const box = document.getElementById('tk'), more = box.querySelector('[data-bz-token-more]');
            // A + for every token that shows, a - for every hidden one, then what the counter says.
            const state = () => [...box.querySelectorAll('[data-bz-token]')].map(token => token.style.display === 'none' ? '-' : '+').join('') +
                ' ' + (more.hidden ? 'no counter' : more.textContent);
            const until = async (wanted, ms = 3000) => {
                for (const end = performance.now() + ms; performance.now() < end; await sleep(20)) if (state() === wanted) break;
                return state();
            };
            const overflow = createTokenOverflow(box);
            const result = { start: state() };

            box.style.width = '400px';
            result.wide = await until('+++++ no counter');
            box.style.width = '100px';
            result.narrow = await until('+---- +4');
            box.style.width = '20px';
            result.tiny = await until('----- +5');
            box.style.width = '200px';
            await until('++--- +3');

            // The selection changes: the box did not resize, so its owner asks for a new measure.
            box.querySelector('[data-bz-token]').remove();
            box.querySelector('[data-bz-token]').remove();
            result.staleUntilAsked = state();
            overflow.update();
            result.afterRemoving = state();
            box.insertAdjacentHTML('afterbegin', tokens(3));
            overflow.update();
            result.afterAdding = state();

            overflow.dispose();
            box.style.width = '400px';
            await sleep(200);
            result.disposed = state();

            // No counter to show: the tokens are left to be clipped. Nothing to attach to: a handle
            // that does nothing.
            const bare = document.getElementById('tk-bare');
            createTokenOverflow(bare).dispose();
            result.noCounter = [...bare.children].every(token => token.style.display !== 'none');
            const none = createTokenOverflow(null);
            none.update();
            none.dispose();
            host.remove();
            return result;
            """);
        string At(string name) => r.GetProperty(name).GetString()!;

        // 200 px: two tokens and the counter are 150, a third token would make 210.
        Assert.Equal("++--- +3", At("start"));
        // Room for all: no counter. Less room: fewer tokens, down to the counter alone.
        Assert.Equal("+++++ no counter", At("wide"));
        Assert.Equal("+---- +4", At("narrow"));
        Assert.Equal("----- +5", At("tiny"));
        // Each measure starts from every token showing, so one hidden for the old selection does
        // not stay hidden for the new one.
        Assert.Equal("--- +3", At("staleUntilAsked"));
        Assert.Equal("+++ no counter", At("afterRemoving"));
        Assert.Equal("++---- +4", At("afterAdding"));
        Assert.Equal("++---- +4", At("disposed"));
        Assert.True(r.GetProperty("noCounter").GetBoolean());
    }
}
