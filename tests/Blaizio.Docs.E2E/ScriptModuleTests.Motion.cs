using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

// The modules that move things, or wait for something to finish moving: dragging to reorder, the
// toast stack, exit animations (collapse, presence), the reveal of cut-off text, and the small
// ones that move a surface to the body, write the clipboard, hold the page still and turn a
// chosen picture.
public sealed partial class ScriptModuleTests
{
    // The fixture's own contexts ask for reduced motion, under which the docs page animates
    // nothing. These modules are about animations that do run.
    private Task<IBrowserContext> NewContextWithMotionAsync(
        [CallerFilePath] string callerFile = "", [CallerMemberName] string callerMember = "") =>
        fx.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            ReducedMotion = ReducedMotion.NoPreference,
        }, callerFile, callerMember);

    // Presses on an element, moves by the given distance, reads something while the pointer is
    // still down, and lets go.
    private static async Task<string> DragAndReadAsync(IPage page, string selector, float dx, float dy, string read)
    {
        var (x, y) = await CentreAsync(page, selector);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + dx, y + dy, new() { Steps = 3 });
        var value = await page.EvaluateAsync<string>(read);
        await page.Mouse.UpAsync();
        return value;
    }

    // What every sortable test starts from: the options the component sends (a plain vertical
    // list that sorts), one item as it renders it, and a list above the page.
    private const string SortableScript =
        """
        const { createSortable } = await load('sortable');
        const base = { strategy: 'sort', orientation: 'vertical', group: null, animationMs: 150, disabled: false, sort: true,
            transferOut: true, transferIn: true, delayMs: 0, lockAxis: false, swapMode: 'swap', placeholder: false, placeholderClass: null, animation: 'none' };
        const item = (list, id, inner = '', attributes = '', style = 'height:40px') =>
            '<div id="' + list + '-' + id + '" data-sortable-item data-sortable-item-id="' + id + '" tabindex="-1" ' + attributes + ' style="background:#ddd;' + style + '">' + id + inner + '</div>';
        const list = (id, place, style, items) =>
            '<div id="' + id + '" data-slot="sortable" style="position:fixed;z-index:99999;background:white;' + place + ';' + style + '">' + items + '</div>';
        // Items of 40 px, 10 px apart.
        const column = 'width:150px;display:flex;flex-direction:column;gap:10px';
        const make = (id, options = {}) => {
            window['__' + id] = recorder();
            window['__' + id + 'List'] = createSortable(document.getElementById(id), window['__' + id], { ...base, id, ...options });
        };
        // Each item of a list with the transform it carries, the dragged one starred: "a:translate(0px, 60px)* b:-".
        window.__sortState = id => [...document.querySelectorAll('#' + id + ' > [data-sortable-item]')]
            .map(el => el.id.split('-')[1] + ':' + (el.style.transform || '-') + (el.hasAttribute('data-dragging') ? '*' : '')).join(' ');
        const host = document.createElement('div');
        document.body.append(host);
        """;

    private static Task<string> SortStateAsync(IPage page, string list) =>
        page.EvaluateAsync<string>("id => window.__sortState(id)", list);

    // An item is measured, and pressed, where it is drawn, and the docs page draws a transform the
    // script has let go of a moment later (under reduced motion it gives every property a
    // transition of a hundredth of a millisecond). This waits until every item is drawn in its place.
    private static Task SortableAtRestAsync(IPage page, string list) =>
        page.WaitForFunctionAsync(
            "id => [...document.querySelectorAll('#' + id + ' > [data-sortable-item]')].every(el => getComputedStyle(el).transform === 'none')",
            list, new() { Timeout = 5_000 });

    // What the component does once it has re-rendered after a move.
    private static async Task SettleSortableAsync(IPage page, string list)
    {
        await page.EvaluateAsync("id => window['__' + id + 'List'].settle()", list);
        await SortableAtRestAsync(page, list);
    }

    [E2EFact]
    public async Task Sortable_passes_on_only_the_keys_pressed_on_an_item_or_its_handle()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, SortableScript +
            """
            host.innerHTML = list('sk', 'left:20px;top:100px', 'width:260px;display:flex;flex-direction:column;gap:10px',
                item('sk', 'a') +
                item('sk', 'b', '<button id="sk-b-handle" data-sortable-handle>grip</button><input id="sk-b-field" style="width:50px" /><button id="sk-b-button">edit</button>') +
                '<div id="sk-unnamed" data-sortable-item tabindex="-1" style="height:40px">no id</div>' +
                // A list inside an item: its items are its own.
                item('sk', 'c', '<div id="sn" data-slot="sortable">' + item('sn', 'x') + '</div>', '', 'height:auto'));
            make('sk');
            make('sn');
            window.__sortKeys = [];
            window.addEventListener('keydown', e => window.__sortKeys.push(e.key + ':' + e.defaultPrevented));
            return true;
            """);

        // On an item: every key the keyboard contract uses goes to the component, which decides
        // what it means (grab, move, drop, cancel). The ones that would scroll the page lose that
        // default; Enter and Escape have none worth taking. Other keys are not the sortable's.
        await page.FocusAsync("#sk-a");
        foreach (var key in new[] { "Space", "Enter", "Escape", "ArrowDown", "ArrowUp", "ArrowLeft", "ArrowRight", "Home", "End", "x", "Tab" })
            await page.Keyboard.PressAsync(key);
        Assert.Equal(
            """[["NotifyItemKeyDown","a"," "],["NotifyItemKeyDown","a","Enter"],["NotifyItemKeyDown","a","Escape"],["NotifyItemKeyDown","a","ArrowDown"],""" +
            """["NotifyItemKeyDown","a","ArrowUp"],["NotifyItemKeyDown","a","ArrowLeft"],["NotifyItemKeyDown","a","ArrowRight"],["NotifyItemKeyDown","a","Home"],["NotifyItemKeyDown","a","End"]]""",
            await TakeCallsAsync(page, "__sk"));
        Same([" :true", "Enter:false", "Escape:false", "ArrowDown:true", "ArrowUp:true", "ArrowLeft:true", "ArrowRight:true", "Home:true", "End:true", "x:false", "Tab:false"],
            await DrainAsync(page, "__sortKeys"));

        // On the item's handle: the same, for the item it belongs to.
        await page.FocusAsync("#sk-b-handle");
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("""[["NotifyItemKeyDown","b","ArrowUp"]]""", await TakeCallsAsync(page, "__sk"));
        Same(["ArrowUp:true"], await DrainAsync(page, "__sortKeys"));

        // In a control inside the item the keys are the control's: Space types a space, the arrows
        // move the caret, and nothing is grabbed.
        await page.FocusAsync("#sk-b-field");
        foreach (var key in new[] { "Space", "ArrowLeft", "Home" }) await page.Keyboard.PressAsync(key);
        await page.FocusAsync("#sk-b-button");
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("[]", await TakeCallsAsync(page, "__sk"));
        Same([" :false", "ArrowLeft:false", "Home:false", "ArrowDown:false"], await DrainAsync(page, "__sortKeys"));
        Assert.Equal(" ", await page.InputValueAsync("#sk-b-field"));

        // An item the component gave no id cannot be named in a report, so it is left alone.
        await page.FocusAsync("#sk-unnamed");
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("[]", await TakeCallsAsync(page, "__sk"));

        // A key on an item of the inner list is reported once, by that list.
        await page.FocusAsync("#sn-x");
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("[]", await TakeCallsAsync(page, "__sk"));
        Assert.Equal("""[["NotifyItemKeyDown","x","ArrowDown"]]""", await TakeCallsAsync(page, "__sn"));

        // Disposed: the keys are the page's again.
        await page.EvaluateAsync("() => window.__skList.dispose()");
        await page.FocusAsync("#sk-a");
        await DrainAsync(page, "__sortKeys");
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("[]", await TakeCallsAsync(page, "__sk"));
        Same(["ArrowDown:false"], await DrainAsync(page, "__sortKeys"));
    }

    [E2EFact]
    public async Task Sortable_drag_in_one_list_moves_the_others_aside_and_reports_the_move_on_release()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, SortableScript +
            """
            // Four items from y = 100: a 100..140, b 150..190, c 200..240 (it cannot be dragged), d 250..290.
            host.innerHTML = list('sp', 'left:20px;top:100px', 'width:200px;display:flex;flex-direction:column;gap:10px',
                item('sp', 'a') + item('sp', 'b') + item('sp', 'c', '', 'data-sortable-disabled') + item('sp', 'd'));
            make('sp');
            return true;
            """);
        Task<string> State() => SortStateAsync(page, "sp");
        Task<string> Selection() => page.EvaluateAsync<string>("() => document.body.style.userSelect");
        Task StateIs(string state) =>
            page.WaitForFunctionAsync("state => window.__sortState('sp') === state", state, new() { Timeout = 5_000 });
        const string Still = "a:- b:- c:- d:-";

        // A press that barely moves is a click, not a drag.
        await page.Mouse.MoveAsync(120, 120);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(120, 123);
        Assert.Equal("[]", await TakeCallsAsync(page, "__sp"));
        Assert.Equal(Still, await State());

        // Past the middle of the next item: the drag is announced, the dragged item follows the
        // pointer, and the item it passed moves up into the space it left (its 40 px and the gap).
        // The page's text cannot be selected by the movement.
        await page.Mouse.MoveAsync(120, 180);
        Assert.Equal("""[["NotifyDragStart",0]]""", await TakeCallsAsync(page, "__sp"));
        Assert.Equal("a:translate(0px, 60px)* b:translate(0px, -50px) c:- d:-", await State());
        Assert.Equal("none", await Selection());
        // An item that cannot be dragged still makes way for one that can.
        await page.Mouse.MoveAsync(120, 230);
        Assert.Equal("a:translate(0px, 110px)* b:translate(0px, -50px) c:translate(0px, -50px) d:-", await State());

        // Released one place down: the drag ends and the move is reported, from index 0 to 1 of
        // this list, not a swap. Nothing in the page is reordered by the script: the items stay
        // where they were shown until the component has rendered the new order.
        await page.Mouse.MoveAsync(120, 180);
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragEnd",0],["NotifyReorder",0,1,"sp","sp",false]]""", await TakeCallsAsync(page, "__sp"));
        Assert.Equal("a:translate(0px, 60px) b:translate(0px, -50px) c:- d:-", await State());
        // The component renders the new order: the script sees the change and lets go of everything.
        await page.EvaluateAsync("() => document.getElementById('sp').insertBefore(document.getElementById('sp-b'), document.getElementById('sp-a'))");
        await StateIs("b:- a:- c:- d:-");
        Assert.Equal("", await Selection());
        await SortableAtRestAsync(page, "sp");

        // The last item to the very top, three places up.
        await page.Mouse.MoveAsync(120, 270);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(120, 100, new() { Steps = 4 });
        Assert.Equal("b:translate(0px, 50px) a:translate(0px, 50px) c:translate(0px, 50px) d:translate(0px, -170px)*", await State());
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragStart",3],["NotifyDragEnd",3],["NotifyReorder",3,0,"sp","sp",false]]""", await TakeCallsAsync(page, "__sp"));
        // A consumer that never applies the move: the items go back on their own after a moment.
        await StateIs("b:- a:- c:- d:-");
        await SortableAtRestAsync(page, "sp");

        // Dropped where it was picked up: a drag that started and ended, and no move.
        await page.Mouse.MoveAsync(120, 120);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(120, 130);
        Assert.Equal("b:translate(0px, 10px)* a:- c:- d:-", await State());
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragStart",0],["NotifyDragEnd",0]]""", await TakeCallsAsync(page, "__sp"));
        Assert.Equal("b:- a:- c:- d:-", await State());
        await SortableAtRestAsync(page, "sp");

        // The component re-rendered without changing the page (it refused the move): it says so,
        // and everything is let go at once.
        await page.Mouse.MoveAsync(120, 120);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(120, 180);
        await page.Mouse.UpAsync();
        Assert.Equal("b:translate(0px, 60px) a:translate(0px, -50px) c:- d:-", await State());
        await page.EvaluateAsync("() => window.__spList.settle()");
        Assert.Equal("b:- a:- c:- d:-", await State());
        await TakeCallsAsync(page, "__sp");
        await SortableAtRestAsync(page, "sp");

        // The browser takes the pointer away in the middle (a touch that turned into a scroll):
        // that is not a drop. The drag ends, the order stays.
        await page.Mouse.MoveAsync(120, 120);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(120, 180);
        await page.EvaluateAsync("() => window.dispatchEvent(new PointerEvent('pointercancel', { pointerId: 1 }))");
        Assert.Equal("""[["NotifyDragStart",0],["NotifyDragEnd",0]]""", await TakeCallsAsync(page, "__sp"));
        Assert.Equal("b:- a:- c:- d:-", await State());
        await page.Mouse.UpAsync();
        await SortableAtRestAsync(page, "sp");

        // Removed in the middle of a drag: nothing stays displaced, text can be selected again,
        // and the list no longer answers the pointer.
        await page.Mouse.MoveAsync(120, 120);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(120, 180);
        Assert.Equal("none", await Selection());
        await page.EvaluateAsync("() => window.__spList.dispose()");
        Assert.Equal("b:- a:- c:- d:-", await State());
        Assert.Equal("", await Selection());
        await page.Mouse.UpAsync();
        await TakeCallsAsync(page, "__sp");
        await SortableAtRestAsync(page, "sp");
        await DragAsync(page, "#sp-b", 0, 60, steps: 3);
        Assert.Equal("[]", await TakeCallsAsync(page, "__sp"));
    }

    [E2EFact]
    public async Task Sortable_starts_a_drag_only_from_a_place_that_is_meant_to_be_dragged()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, SortableScript +
            """
            host.innerHTML =
                // a has a handle; b has none, but holds a button and a part marked as not for dragging;
                // c is disabled.
                list('sh', 'left:20px;top:100px', column,
                    item('sh', 'a', '<span id="sh-a-handle" data-sortable-handle style="float:right;width:30px;height:30px;background:#888"></span>') +
                    item('sh', 'b', '<button id="sh-b-button" style="float:right;width:40px">go</button>' +
                        '<span id="sh-b-zone" data-no-drag style="float:right;width:30px;height:30px;background:#bbb"></span>') +
                    item('sh', 'c', '', 'data-sortable-disabled') + item('sh', 'd')) +
                // A list that wants the pointer held for 300 ms first.
                list('sw', 'left:220px;top:100px', column, ['a', 'b', 'c'].map(id => item('sw', id)).join(''));
            make('sh');
            make('sw', { delayMs: 300 });
            return true;
            """);
        const string Moved = """[["NotifyDragStart",{0}],["NotifyDragEnd",{0}],["NotifyReorder",{0},{1},"sh","sh",false]]""";

        // An item with a handle is dragged by its handle and by nothing else.
        await DragAsync(page, "#sh-a", 0, 60, steps: 3);
        Assert.Equal("[]", await TakeCallsAsync(page, "__sh"));
        await DragAsync(page, "#sh-a-handle", 0, 60, steps: 3);
        Assert.Equal(string.Format(Moved, 0, 1), await TakeCallsAsync(page, "__sh"));
        await SettleSortableAsync(page, "sh");

        // Without a handle the whole item drags, except from a control that needs the press itself
        // or a part marked as not for dragging.
        await DragAsync(page, "#sh-b-button", 0, 60, steps: 3);
        await DragAsync(page, "#sh-b-zone", 0, 60, steps: 3);
        Assert.Equal("[]", await TakeCallsAsync(page, "__sh"));
        await DragAsync(page, "#sh-b", 0, 60, steps: 3);
        Assert.Equal(string.Format(Moved, 1, 2), await TakeCallsAsync(page, "__sh"));
        await SettleSortableAsync(page, "sh");

        // A disabled item does not move. Neither does anything for the secondary button.
        await DragAsync(page, "#sh-c", 0, -60, steps: 3);
        var (x, y) = await CentreAsync(page, "#sh-d");
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync(new() { Button = MouseButton.Right });
        await page.Mouse.MoveAsync(x, y - 60, new() { Steps = 3 });
        await page.Mouse.UpAsync(new() { Button = MouseButton.Right });
        Assert.Equal("[]", await TakeCallsAsync(page, "__sh"));

        // The whole list disabled after it was wired, and enabled again.
        await page.EvaluateAsync("() => window.__shList.setDisabled(true)");
        await DragAsync(page, "#sh-d", 0, -60, steps: 3);
        Assert.Equal("[]", await TakeCallsAsync(page, "__sh"));
        await page.EvaluateAsync("() => window.__shList.setDisabled(false)");
        await DragAsync(page, "#sh-d", 0, -60, steps: 3);
        Assert.Equal(string.Format(Moved, 3, 2), await TakeCallsAsync(page, "__sh"));

        // With a hold delay, moving at once is a scroll or a tap: the press is given back, and
        // nothing is displaced however far the pointer goes.
        Assert.Equal("a:- b:- c:-", await DragAndReadAsync(page, "#sw-a", 0, 60, "() => window.__sortState('sw')"));
        Assert.Equal("[]", await TakeCallsAsync(page, "__sw"));
        // Held first, the same movement is a drag.
        (x, y) = await CentreAsync(page, "#sw-a");
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.WaitForTimeoutAsync(400);
        await page.Mouse.MoveAsync(x, y + 60, new() { Steps = 3 });
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragStart",0],["NotifyDragEnd",0],["NotifyReorder",0,1,"sw","sw",false]]""", await TakeCallsAsync(page, "__sw"));
    }

    [E2EFact]
    public async Task Sortable_works_out_the_drop_place_for_each_layout_and_strategy()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, SortableScript +
            """
            const three = id => ['a', 'b', 'c'].map(name => item(id, name)).join('');
            host.innerHTML =
                // A row read right to left: a is the rightmost.
                list('sr', 'left:20px;top:100px', 'width:320px;display:flex;gap:10px;direction:rtl', ['a', 'b', 'c'].map(name => item('sr', name, '', '', 'width:100px;height:40px')).join('')) +
                list('ss', 'left:20px;top:200px', column, three('ss')) +
                list('sl', 'left:220px;top:200px', column, three('sl')) +
                list('sd', 'left:420px;top:200px', column, three('sd')) +
                list('sx', 'left:620px;top:200px', column, three('sx')) +
                // Two columns of 100 px, two rows.
                list('sg', 'left:820px;top:200px', 'width:210px;display:grid;grid-template-columns:100px 100px;gap:10px', ['a', 'b', 'c', 'd'].map(name => item('sg', name)).join(''));
            make('sr', { orientation: 'horizontal' });
            make('ss', { strategy: 'swap' });
            make('sl', { lockAxis: true, placeholder: true, placeholderClass: 'my-marker' });
            make('sd', { swapMode: 'drop', placeholder: true });
            make('sx', { sort: false });
            make('sg', { orientation: 'grid' });
            // The marker that shows where the item will land: its kind, its class, and where it is put.
            window.__marker = () => {
                const marker = document.querySelector('[data-slot=sortable-placeholder]');
                return marker ? [marker.getAttribute('data-variant'), marker.className || '-', marker.style.left, marker.style.top, marker.style.width, marker.style.height].join(' ') : 'none';
            };
            return true;
            """);
        Task<string> DragAndReadState(string list, float dx, float dy) =>
            DragAndReadAsync(page, $"#{list}-a", dx, dy, $"() => window.__sortState('{list}')");
        static string Reorder(string list, int to, bool swap = false)
        {
            var kind = swap ? "true" : "false";
            return $$"""[["NotifyDragStart",0],["NotifyDragEnd",0],["NotifyReorder",0,{{to}},"{{list}}","{{list}}",{{kind}}]]""";
        }

        // Right to left the second place is to the left: dragged that way past its neighbour, the
        // first item lands there and the neighbour moves right into its place.
        Assert.Equal("a:translate(-120px, 0px)* b:translate(110px, 0px) c:-", await DragAndReadState("sr", -120, 0));
        Assert.Equal(Reorder("sr", 1), await TakeCallsAsync(page, "__sr"));

        // Swap: only the item under the dragged one moves, straight into the place that was left,
        // and the move is reported as a swap with it.
        Assert.Equal("a:translate(0px, 100px)* b:- c:translate(0px, -100px)", await DragAndReadState("ss", 0, 100));
        Assert.Equal(Reorder("ss", 2, swap: true), await TakeCallsAsync(page, "__ss"));
        await SettleSortableAsync(page, "ss");
        // Still nearer its own place than any other: nothing to swap with yet.
        Assert.Equal("a:translate(0px, 20px)* b:- c:-", await DragAndReadState("ss", 0, 20));
        Assert.Equal("""[["NotifyDragStart",0],["NotifyDragEnd",0]]""", await TakeCallsAsync(page, "__ss"));

        // Locked to the list's axis the item ignores the sideways part of the movement. A marker
        // with the consumer's class sits where the item will land (one place down), and goes away
        // with the drop.
        Assert.Equal("a:translate(0px, 60px)* b:translate(0px, -50px) c:- | gap my-marker 220px 250px 150px 40px",
            await DragAndReadAsync(page, "#sl-a", 80, 60, "() => window.__sortState('sl') + ' | ' + window.__marker()"));
        Assert.Equal(Reorder("sl", 1), await TakeCallsAsync(page, "__sl"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => window.__marker()"));

        // "Drop" mode holds everything still until release: only a line shows the place, here
        // after the last item (its bottom edge at 340, half the gap further down, 4 px thick).
        Assert.Equal("a:translate(0px, 110px)* b:- c:- | line - 420px 343px 150px 4px",
            await DragAndReadAsync(page, "#sd-a", 0, 110, "() => window.__sortState('sd') + ' | ' + window.__marker()"));
        Assert.Equal(Reorder("sd", 2), await TakeCallsAsync(page, "__sd"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => window.__marker()"));

        // A list that may not be sorted (items only leave it for another list): the item can be
        // picked up, nothing makes way, and no move is reported.
        Assert.Equal("a:translate(0px, 60px)* b:- c:-", await DragAndReadState("sx", 0, 60));
        Assert.Equal("""[["NotifyDragStart",0],["NotifyDragEnd",0]]""", await TakeCallsAsync(page, "__sx"));

        // A grid: the first cell dragged onto the last one lands before it, and the two cells in
        // between flow back by one, the second of them up to the end of the first row.
        Assert.Equal("a:translate(110px, 50px)* b:translate(-110px, 0px) c:translate(110px, -50px) d:-", await DragAndReadState("sg", 110, 50));
        Assert.Equal(Reorder("sg", 2), await TakeCallsAsync(page, "__sg"));
    }

    [E2EFact]
    public async Task Sortable_held_near_the_edge_of_a_scrolling_box_scrolls_it_to_reach_the_rest_of_the_list()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, SortableScript + ScrollingListScript);
        Task<double> Scrolled() => page.EvaluateAsync<double>("() => document.getElementById('sc-box').scrollTop");
        Task StateIs(string state) =>
            page.WaitForFunctionAsync("state => window.__sortState('sc') === state", state, new() { Timeout = 5_000 });

        // In the middle of the box a drag scrolls nothing.
        await page.Mouse.MoveAsync(200, 120);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(200, 160);
        Assert.Equal("a:translate(0px, 40px)* b:- c:- d:- e:- f:- g:- h:-", await SortStateAsync(page, "sc"));
        Assert.Equal(0, await Scrolled());

        // Held within 40 px of the bottom edge, with the pointer not moving, the box scrolls on by
        // itself to its end (230 px). The dragged item stays under the pointer, so it has
        // travelled the pointer's 120 px and the 230 px of list that went by: past six items, to
        // just before the last. (Pressed at its middle and held 20 px from the edge, the item is
        // flush with the edge: see the test below for one that reaches past it.)
        await page.Mouse.MoveAsync(200, 240);
        await StateIs("a:translate(0px, 350px)* b:translate(0px, -50px) c:translate(0px, -50px) d:translate(0px, -50px) " +
            "e:translate(0px, -50px) f:translate(0px, -50px) g:translate(0px, -50px) h:-");
        Assert.Equal(230, await Scrolled());
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragStart",0],["NotifyDragEnd",0],["NotifyReorder",0,6,"sc","sc",false]]""", await TakeCallsAsync(page, "__sc"));
        await SettleSortableAsync(page, "sc");

        // And the other way: the last item, now in view at the bottom, held near the top edge.
        var (x, y) = await CentreAsync(page, "#sc-h");
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, 120, new() { Steps = 3 });
        await StateIs("a:translate(0px, 50px) b:translate(0px, 50px) c:translate(0px, 50px) d:translate(0px, 50px) " +
            "e:translate(0px, 50px) f:translate(0px, 50px) g:translate(0px, 50px) h:translate(0px, -350px)*");
        Assert.Equal(0, await Scrolled());
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragStart",7],["NotifyDragEnd",7],["NotifyReorder",7,0,"sc","sc",false]]""", await TakeCallsAsync(page, "__sc"));
    }

    // Eight items (390 px of list) in a box that shows 160 px of them, from y = 100 to 260.
    private const string ScrollingListScript =
        """
        host.innerHTML = '<div id="sc-box" style="position:fixed;z-index:99999;left:100px;top:100px;width:200px;height:160px;overflow:auto;background:white">' +
            '<div id="sc" data-slot="sortable" style="display:flex;flex-direction:column;gap:10px">' +
            ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'].map(id => item('sc', id, '', '', 'height:40px;flex:none')).join('') + '</div></div>';
        make('sc');
        return true;
        """;

    [E2EFact]
    public async Task Sortable_edge_scrolling_stops_at_the_end_of_the_list()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, SortableScript + ScrollingListScript);

        // The first item, pressed at its middle and held 10 px from the bottom edge of the box:
        // its lower half reaches past the edge, as it does whenever an item is dragged to the end
        // of a list.
        await page.Mouse.MoveAsync(200, 120);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(200, 160);
        await page.Mouse.MoveAsync(200, 250);
        await page.WaitForTimeoutAsync(1_500);

        // The list ends 230 px down. The box used to be some 900 px down by now and still going, at
        // 600 px a second, through empty space: the dragged item is moved along with the scroll,
        // and an item moved past the bottom of the box makes the box that much taller to scroll.
        var scrolled = await page.EvaluateAsync<double>("() => document.getElementById('sc-box').scrollTop");
        await page.Mouse.UpAsync();
        Assert.Equal(230, scrolled);
        Assert.Equal("""[["NotifyDragStart",0],["NotifyDragEnd",0],["NotifyReorder",0,7,"sc","sc",false]]""", await TakeCallsAsync(page, "__sc"));
    }

    // A toaster root with one stack in the bottom right corner, as the component renders it. The
    // docs stylesheet lays the stack out from what the script writes, exactly as for a real toast.
    private const string ToastScript =
        """
        const { createToaster } = await load('toast');
        const root = document.createElement('section');
        document.body.append(root);
        // Content of a set height, so that each toast's own height is known. Its text cannot be
        // selected: a drag that selects text is left to the selection, and is not a swipe.
        const toast = (id, height, duration = 'infinity', inner = '', attributes = 'data-dismissible="true"') =>
            '<li data-bz-toast data-bz-toast-id="' + id + '" data-bz-toast-duration="' + duration + '" data-y-position="bottom" data-x-position="right" ' + attributes +
            ' tabindex="0" style="background:#eee;user-select:none"><div style="height:' + height + 'px;flex:1">' + id + inner + '</div></li>';
        const stack = (id, toasts, attributes = 'data-y-position="bottom" data-x-position="right"') =>
            '<ol id="' + id + '" data-bz-toaster ' + attributes + ' data-bz-visible-toasts="3">' + toasts + '</ol>';
        window.__toastHtml = toast;
        // Every toast of a stack: "id index z-index [front] shown|behind stacked|expanded offset height [removed]".
        window.__stack = id => [...document.querySelectorAll('#' + id + ' [data-bz-toast]')].map(li =>
            [li.getAttribute('data-bz-toast-id'), li.style.getPropertyValue('--index'), li.style.getPropertyValue('--z-index'),
                li.getAttribute('data-front') === 'true' ? 'front' : '', li.getAttribute('data-visible') === 'true' ? 'shown' : 'behind',
                li.getAttribute('data-expanded') === 'true' ? 'expanded' : 'stacked', li.style.getPropertyValue('--offset'),
                li.style.getPropertyValue('--initial-height'), li.hasAttribute('data-removed') ? 'removed' : ''].filter(part => part !== '').join(' ')).join(' | ');
        // The stack itself: the height it collapses to, and whether it is lifted.
        window.__stackList = id => document.getElementById(id).style.getPropertyValue('--front-toast-height') + ' ' + document.getElementById(id).getAttribute('data-lifted');
        """;

    // A toast is measured, and pressed, where it is drawn: this waits until it has arrived (it
    // enters from below the edge, and the page draws the change a moment after the script made it).
    private static Task ToastArrivedAsync(IPage page, string selector) =>
        page.WaitForFunctionAsync(
            """
            selector => {
                const toast = document.querySelector(selector);
                if (toast?.getAttribute('data-mounted') !== 'true') return false;
                const style = getComputedStyle(toast);
                return style.opacity === '1' && new DOMMatrix(style.transform).isIdentity;
            }
            """, selector, new() { Timeout = 5_000 });

    [E2EFact]
    public async Task Toast_stack_orders_its_toasts_shows_the_first_few_and_opens_up_under_the_pointer()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var start = await RunAsync<string[]>(page, ToastScript +
            """
            // Newest first, as the host renders them; the second one is taller than the rest.
            root.innerHTML = stack('tt', toast('e', 40) + toast('d', 60) + toast('c', 40) + toast('b', 40) + toast('a', 40));
            window.__tt = recorder();
            window.__ttToaster = createToaster(root, window.__tt);
            window.__ttToaster.sync();
            return [window.__stack('tt'), window.__stackList('tt'), String(root.querySelectorAll('[data-mounted]').length)];
            """);
        Task<string> Stack() => page.EvaluateAsync<string>("() => window.__stack('tt')");
        Task<string> List() => page.EvaluateAsync<string>("() => window.__stackList('tt')");

        // The first toast is the front one and on top; three show, the rest wait behind. Each one's
        // offset is the heights before it plus a 14 px gap each: what the open stack is laid out by.
        // The stack collapses to the height of its front toast.
        Assert.Equal("e 0 5 front shown stacked 0px 40px | d 1 4 shown stacked 54px 60px | c 2 3 shown stacked 128px 40px | " +
            "b 3 2 behind stacked 182px 40px | a 4 1 behind stacked 236px 40px", start[0]);
        Assert.Equal("40px false", start[1]);
        // A new toast is marked as arrived a moment after it is measured, so that it has a place
        // to animate in from.
        Assert.Equal("0", start[2]);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#tt [data-mounted=true]').length === 5", null, new() { Timeout = 5_000 });
        await ToastArrivedAsync(page, "#tt [data-front=true]");

        // The pointer over the stack opens it: every toast shows, at its own height, and the
        // stack is lifted. Drawn, each one sits above the one before it by its offset.
        var (x, y) = await CentreAsync(page, "#tt [data-front=true]");
        await page.Mouse.MoveAsync(x, y);
        Assert.Equal("e 0 5 front shown expanded 0px 40px | d 1 4 shown expanded 54px 60px | c 2 3 shown expanded 128px 40px | " +
            "b 3 2 shown expanded 182px 40px | a 4 1 shown expanded 236px 40px", await Stack());
        Assert.Equal("40px true", await List());
        await page.WaitForFunctionAsync(
            """
            () => {
                const bottoms = [...document.querySelectorAll('#tt [data-bz-toast]')].map(li => Math.round(li.getBoundingClientRect().bottom));
                return bottoms.map(bottom => bottoms[0] - bottom).join(' ') === '0 54 128 182 236';
            }
            """, null, new() { Timeout = 5_000 });

        // The host renders again: the front toast has grown, one is gone, and fewer may show. The
        // stack is measured and numbered afresh.
        await page.EvaluateAsync(
            """
            () => {
                document.querySelector('#tt [data-bz-toast-id=e] div').style.height = '100px';
                document.querySelector('#tt [data-bz-toast-id=d]').remove();
                document.getElementById('tt').setAttribute('data-bz-visible-toasts', '2');
                window.__ttToaster.sync();
            }
            """);
        Assert.Equal("e 0 4 front shown expanded 0px 100px | c 1 3 shown expanded 114px 40px | b 2 2 shown expanded 168px 40px | a 3 1 shown expanded 222px 40px", await Stack());
        Assert.Equal("100px true", await List());

        // The host dismisses a toast (its close button, or code): it is marked as leaving at once
        // and keeps its place while it does, the ones behind close up, and nothing is reported yet.
        await page.EvaluateAsync(
            "() => { document.querySelector('#tt [data-bz-toast-id=c]').setAttribute('data-bz-toast-dismiss', 'true'); window.__ttToaster.sync(); window.__ttToaster.sync(); }");
        Assert.Equal("e 0 4 front shown expanded 0px 100px | c 1 3 shown expanded 114px 40px removed | b 2 2 shown expanded 114px 40px | a 3 1 shown expanded 168px 40px", await Stack());
        Assert.Equal("[]", await TakeCallsAsync(page, "__tt"));
        // Once its exit has had time to play it is reported, once, as not closed by its timer.
        // Only then does the host take it out of the list.
        await page.WaitForFunctionAsync("() => window.__tt.calls.length > 0", null, new() { Timeout = 5_000 });
        await page.WaitForTimeoutAsync(300);
        Assert.Equal("""[["Remove","c",false]]""", await TakeCallsAsync(page, "__tt"));
        await page.EvaluateAsync("() => { document.querySelector('#tt [data-bz-toast-id=c]').remove(); window.__ttToaster.sync(); }");
        Assert.Equal("e 0 3 front shown expanded 0px 100px | b 1 2 shown expanded 114px 40px | a 2 1 shown expanded 168px 40px", await Stack());

        // The pointer leaves: the stack closes to its front toast, and the limit applies again.
        await page.Mouse.MoveAsync(5, 5);
        Assert.Equal("e 0 3 front shown stacked 0px 100px | b 1 2 shown stacked 114px 40px | a 2 1 behind stacked 168px 40px", await Stack());
        Assert.Equal("100px false", await List());

        // A lone toast has nothing behind it to lift.
        await page.EvaluateAsync(
            "() => { for (const id of ['b', 'a']) document.querySelector('#tt [data-bz-toast-id=' + id + ']').remove(); window.__ttToaster.sync(); }");
        await ToastArrivedAsync(page, "#tt [data-front=true]");
        (x, y) = await CentreAsync(page, "#tt [data-front=true]");
        await page.Mouse.MoveAsync(x, y);
        Assert.Equal("e 0 1 front shown expanded 0px 100px", await Stack());
        Assert.Equal("100px false", await List());
    }

    [E2EFact]
    public async Task Toast_behind_the_front_one_keeps_its_own_height_when_the_host_renders_again()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, ToastScript +
            """
            root.innerHTML = stack('th', toast('c', 40) + toast('b', 60) + toast('a', 40));
            window.__thToaster = createToaster(root, recorder());
            window.__thToaster.sync();
            return true;
            """);
        Task<string> Stack() => page.EvaluateAsync<string>("() => window.__stack('th')");
        const string Measured = "c 0 3 front shown stacked 0px 40px | b 1 2 shown stacked 54px 60px | a 2 1 shown stacked 128px 40px";

        Assert.Equal(Measured, await Stack());
        // The stack has settled as it is drawn while closed: the toasts behind the front one are
        // 5 percent smaller per place.
        await page.WaitForFunctionAsync(
            """
            () => {
                const second = document.querySelector('#th [data-bz-toast-id=b]');
                return second.getAttribute('data-mounted') === 'true' && Math.abs(new DOMMatrix(getComputedStyle(second).transform).a - 0.95) < 0.001;
            }
            """, null, new() { Timeout = 5_000 });

        // The host renders again with nothing changed (it does after every toast shown, updated
        // or dismissed). What gets measured is the toast as drawn, scale and all: the second toast
        // used to come out at 57 px and the third at 36, so an opened stack gave them less room
        // than their content takes.
        await page.EvaluateAsync("() => window.__thToaster.sync()");
        Assert.Equal(Measured, await Stack());
    }

    [E2EFact]
    public async Task Toast_closes_when_its_time_is_up_and_the_time_stops_while_it_is_hidden_or_hovered()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, ToastScript +
            """
            root.innerHTML = stack('tm', toast('slow', 40, '2000') + toast('quick', 40, '300') + toast('keep', 40, 'persistent'));
            const list = document.getElementById('tm');
            window.__tm = recorder();
            window.__tmToaster = createToaster(root, window.__tm);
            // Everything below is timed from the moment the toasts are first seen.
            const started = performance.now();
            window.__tmNow = () => performance.now() - started;
            window.__tmAt = {};
            const stamp = name => { window.__tmAt[name] ??= window.__tmNow(); };
            new MutationObserver(records => { for (const record of records) stamp('removed-' + record.target.getAttribute('data-bz-toast-id')); })
                .observe(list, { subtree: true, attributes: true, attributeFilter: ['data-removed'] });
            list.addEventListener('mouseenter', () => stamp('hovered'));
            list.addEventListener('mouseleave', () => stamp('left'));
            // The tab goes to the background and comes back.
            window.__tmHide = hidden => {
                if (hidden) Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
                else delete document.hidden;
                document.dispatchEvent(new Event('visibilitychange'));
                stamp(hidden ? 'hidden' : 'shown');
            };
            window.__tmToaster.sync();
            return true;
            """);
        Task<double> At(string name) => page.EvaluateAsync<double>("name => window.__tmAt[name] ?? -1", name);
        Task Until(string condition) => page.WaitForFunctionAsync(condition, null, new() { Timeout = 10_000 });

        // 300 ms: marked as leaving when the time is up, never before, and reported as closed by
        // its timer once the exit has played. The host then takes it out and renders again, which
        // does not start the others' time over.
        await Until("() => window.__tmAt['removed-quick'] !== undefined");
        Assert.True(await At("removed-quick") >= 280, $"the 300 ms toast left after {await At("removed-quick"):0} ms");
        await Until("() => window.__tm.calls.length > 0");
        Assert.Equal("""[["Remove","quick",true]]""", await TakeCallsAsync(page, "__tm"));
        await page.EvaluateAsync("() => { document.querySelector('#tm [data-bz-toast-id=quick]').remove(); window.__tmToaster.sync(); }");

        // The tab is hidden some 600 ms before the 2 s toast is due and stays hidden until well
        // after: a toast nobody could see has not used up its time.
        await Until("() => window.__tmNow() > 1400");
        await page.EvaluateAsync("() => window.__tmHide(true)");
        await Until("() => window.__tmNow() > 2600");
        Assert.Equal(-1d, await At("removed-slow"));
        // Back in view, and the pointer goes onto the stack for longer than the toast has left:
        // a toast that is being read is not counted down either.
        await ToastArrivedAsync(page, "#tm [data-front=true]");
        var (x, y) = await CentreAsync(page, "#tm [data-front=true]");
        await page.EvaluateAsync("() => window.__tmHide(false)");
        await page.Mouse.MoveAsync(x, y);
        await Until("() => window.__tmAt.hovered !== undefined");
        await page.WaitForTimeoutAsync(900);
        Assert.Equal(-1d, await At("removed-slow"));

        // The pointer leaves: the toast gets what was left of its 2 s (the time before the tab was
        // hidden and the instant between coming back and the hover are spent), not a fresh 2 s.
        await page.Mouse.MoveAsync(5, 5);
        await Until("() => window.__tmAt['removed-slow'] !== undefined");
        var left = 2000 - await At("hidden") - (await At("hovered") - await At("shown"));
        var lived = await At("removed-slow") - await At("left");
        Assert.InRange(lived, left - 40, left + 700);
        await Until("() => window.__tm.calls.length > 0");
        Assert.Equal("""[["Remove","slow",true]]""", await TakeCallsAsync(page, "__tm"));

        // A toast with no time limit is still there.
        Assert.False(await page.EvaluateAsync<bool>("() => document.querySelector('#tm [data-bz-toast-id=keep]').hasAttribute('data-removed')"));

        // Disposed with a toast still counting down: it is never closed by this engine.
        await page.EvaluateAsync(
            """
            () => {
                document.getElementById('tm').insertAdjacentHTML('afterbegin', window.__toastHtml('late', 40, '300'));
                window.__tmToaster.sync();
                window.__tmToaster.dispose();
            }
            """);
        await page.WaitForTimeoutAsync(900);
        Assert.False(await page.EvaluateAsync<bool>("() => document.querySelector('#tm [data-bz-toast-id=late]').hasAttribute('data-removed')"));
        Assert.Equal("[]", await TakeCallsAsync(page, "__tm"));
    }

    [E2EFact]
    public async Task Toast_swiped_towards_its_edge_is_dismissed_and_any_other_drag_springs_back()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, ToastScript +
            """
            root.id = 'ts-root';
            root.innerHTML =
                // Bottom right: it leaves to the right or downwards.
                stack('ts', toast('one', 60, 'infinity', '<button id="ts-action" style="float:right">Undo</button>')) +
                // Bottom left, and not to be dismissed by the user.
                stack('tl', toast('fixed', 60, 'infinity', '', 'data-dismissible="false"').replace('data-x-position="right"', 'data-x-position="left"'),
                    'data-y-position="bottom" data-x-position="left"') +
                // Top "right" in a page read right to left: that corner is on the left there.
                stack('tr', toast('mirrored', 60).replace('data-y-position="bottom"', 'data-y-position="top"'), 'dir="rtl" data-y-position="top" data-x-position="right"');
            window.__ts = recorder();
            window.__tsToaster = createToaster(root, window.__ts);
            window.__tsToaster.sync();
            // One toast: "x amount, y amount, [swiping] [swiped] [out:direction] [removed]".
            window.__swipe = id => {
                const li = document.querySelector('#ts-root [data-bz-toast-id=' + id + ']');
                const amount = name => Math.round(parseFloat(li.style.getPropertyValue(name) || '0') * 100) / 100;
                return [amount('--swipe-amount-x') + ',' + amount('--swipe-amount-y'), li.getAttribute('data-swiping') === 'true' ? 'swiping' : '',
                    li.getAttribute('data-swiped') === 'true' ? 'swiped' : '', li.hasAttribute('data-swipe-out') ? 'out:' + li.getAttribute('data-swipe-direction') : '',
                    li.hasAttribute('data-removed') ? 'removed' : ''].filter(part => part !== '').join(' ');
            };
            // The host puts a new toast in the bottom right stack in place of the one that left.
            window.__tsNext = id => {
                document.getElementById('ts').innerHTML = window.__toastHtml(id, 60);
                window.__tsToaster.sync();
            };
            return true;
            """);
        Task<string> Swipe(string id) => page.EvaluateAsync<string>("id => window.__swipe(id)", id);
        Task Arrived(string id) => ToastArrivedAsync(page, $"#ts-root [data-bz-toast-id={id}]");
        // Presses 60 px in from the toast's left edge, moves, and holds: [state while held, state once released].
        async Task<string[]> Drag(string id, float dx, float dy, int holdMs)
        {
            await Arrived(id);
            var toast = (await page.Locator($"#ts-root [data-bz-toast-id={id}]").BoundingBoxAsync())!;
            var (x, y) = (toast.X + 60, toast.Y + toast.Height / 2);
            await page.Mouse.MoveAsync(x, y);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(x + dx, y + dy, new() { Steps = 2 });
            if (holdMs > 0) await page.WaitForTimeoutAsync(holdMs);
            var held = await Swipe(id);
            await page.Mouse.UpAsync();
            return [held, await Swipe(id)];
        }

        // Away from its edge the toast resists: it follows a fraction of the movement (100 px
        // become 15.38), and goes back when it is let go.
        Same(["-15.38,0 swiping swiped", "0,0"], await Drag("one", -100, 0, 300));
        Same(["0,-15.38 swiping swiped", "0,0"], await Drag("one", 0, -100, 300));
        // Towards its edge it follows the pointer, but a short drag that was not a flick is not
        // a dismissal either.
        Same(["30,0 swiping swiped", "0,0"], await Drag("one", 30, 0, 400));
        Assert.Equal("[]", await TakeCallsAsync(page, "__ts"));

        // Far enough towards the edge: it leaves that way, and is reported (not closed by its
        // timer) when its exit has played.
        Same(["60,0 swiping swiped", "60,0 swiped out:right removed"], await Drag("one", 60, 0, 300));
        await page.WaitForFunctionAsync("() => window.__ts.calls.length > 0", null, new() { Timeout = 5_000 });
        Assert.Equal("""[["Remove","one",false]]""", await TakeCallsAsync(page, "__ts"));

        // A flick: not far, but fast.
        await page.EvaluateAsync("() => window.__tsNext('two')");
        Assert.Equal("40,0 swiped out:right removed", (await Drag("two", 40, 0, 0))[1]);
        // Downwards, for a toast at the bottom.
        await page.EvaluateAsync("() => window.__tsNext('three')");
        Assert.Equal("0,60 swiped out:down removed", (await Drag("three", 0, 60, 300))[1]);

        // A press on a button in the toast is the button's: the toast does not follow the pointer.
        await page.EvaluateAsync("() => { document.getElementById('ts').innerHTML = window.__toastHtml('four', 60, 'infinity', '<button id=\"ts-action\" style=\"float:right\">Undo</button>'); window.__tsToaster.sync(); }");
        await Arrived("four");
        Assert.Equal("0,0", await DragAndReadAsync(page, "#ts-action", 100, 0, "() => window.__swipe('four')"));
        Assert.Equal("0,0", await Swipe("four"));

        // The browser takes the pointer away in the middle of a swipe: the toast goes back, and
        // the release that follows dismisses nothing.
        var box = (await page.Locator("#ts-root [data-bz-toast-id=four]").BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(box.X + 60, box.Y + 30);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(box.X + 120, box.Y + 30, new() { Steps = 2 });
        Assert.Equal("60,0 swiping swiped", await Swipe("four"));
        await page.EvaluateAsync(
            "() => document.querySelector('#ts-root [data-bz-toast-id=four]').dispatchEvent(new PointerEvent('pointercancel', { bubbles: true, pointerId: 1 }))");
        Assert.Equal("0,0", await Swipe("four"));
        await page.Mouse.UpAsync();
        Assert.Equal("0,0", await Swipe("four"));

        // A toast the user may not dismiss does not move at all.
        Same(["0,0", "0,0"], await Drag("fixed", -100, 0, 300));
        // Right to left the "right" corner is on the left, so that is the way out.
        Same(["-60,0 swiping swiped", "-60,0 swiped out:left removed"], await Drag("mirrored", -60, 0, 300));
        await page.WaitForFunctionAsync("() => window.__ts.calls.length >= 3", null, new() { Timeout = 5_000 });
        Assert.Equal("""[["Remove","two",false],["Remove","three",false],["Remove","mirrored",false]]""", await TakeCallsAsync(page, "__ts"));
    }

    [E2EFact]
    public async Task Collapse_keeps_the_height_of_an_open_panel_measured_and_reports_when_its_closing_has_played()
    {
        await using var context = await NewContextWithMotionAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page, UntilScript +
            """
            const { createCollapse } = await load('collapse');
            const style = document.createElement('style');
            // What a style's accordion does: the height runs between 0 and the measured height.
            style.textContent =
                '@keyframes cl-open { from { height: 0 } to { height: var(--bz-collapsible-content-height) } }' +
                '@keyframes cl-close { from { height: var(--bz-collapsible-content-height) } to { height: 0 } }' +
                '.cl-animated[data-state=open] { animation: cl-open 300ms linear }' +
                '.cl-animated[data-state=closed] { animation: cl-close 300ms linear }';
            document.head.append(style);
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;width:200px;background:white';
            host.innerHTML =
                '<div id="cl" data-state="open" style="overflow:hidden"><div id="cl-inner"><div id="cl-body" style="height:80.4px">body</div></div></div>' +
                '<div id="ca" class="cl-animated" data-state="open" style="overflow:hidden"><div><div style="height:60px">animated</div></div></div>' +
                '<div id="cw-column" style="width:200px"><div id="cw" data-state="open"><div><p style="margin:0;font:16px/20px monospace">words that wrap onto more lines in a narrow column</p></div></div></div>';
            document.body.append(host);
            const result = {};

            const panel = document.getElementById('cl'), inner = document.getElementById('cl-inner'), body = document.getElementById('cl-body');
            const ref = recorder();
            const heights = () => panel.style.getPropertyValue('--bz-collapsible-content-height') + ' ' + panel.style.getPropertyValue('--bz-accordion-content-height');
            const collapse = createCollapse(panel, ref);
            result.measured = heights();
            body.style.height = '120px';
            result.grown = await until(() => heights() === '120px 120px');
            // As the styled panel is: its inner wrapper held at the height measured last.
            inner.style.height = '120px';
            body.style.height = '150px';
            result.throughThePin = await until(() => heights() === '150px 150px');
            result.pinKept = inner.style.height;
            inner.style.height = '';
            inner.insertAdjacentHTML('beforeend', '<div style="height:30px">loaded later</div>');
            result.added = await until(() => heights() === '180px 180px');

            // Closed, with no closing animation to wait for.
            panel.dataset.state = 'closed';
            collapse.onClosing();
            result.closedAtOnce = JSON.stringify(ref.calls.splice(0));
            body.style.height = '10px';
            await sleep(200);
            result.whileClosed = heights();
            panel.dataset.state = 'open';
            result.openAgain = await until(() => heights() === '40px 40px');

            // Find-in-page matched text inside a closed panel: the browser says so on the panel.
            panel.dispatchEvent(new Event('beforematch', { bubbles: true }));
            body.dispatchEvent(new Event('beforematch', { bubbles: true }));
            result.found = JSON.stringify(ref.calls.splice(0));

            collapse.dispose();
            body.style.height = '300px';
            panel.dispatchEvent(new Event('beforematch', { bubbles: true }));
            await sleep(200);
            result.disposed = heights() + ' ' + JSON.stringify(ref.calls);

            // The window gets narrower: the text wraps onto more lines, and nothing inside the
            // panel changed to say so. (The column is narrowed by hand, then the window says it resized.)
            const wrapping = document.getElementById('cw');
            const wrapped = createCollapse(wrapping, recorder());
            const height = () => parseFloat(wrapping.style.getPropertyValue('--bz-collapsible-content-height'));
            const wide = height();
            document.getElementById('cw-column').style.width = '100px';
            await sleep(200);
            result.staleBeforeTheResize = height() === wide;
            window.dispatchEvent(new Event('resize'));
            result.afterTheResize = await until(() => height() > wide);
            wrapped.dispose();

            // With a closing animation of 300 ms.
            const animated = document.getElementById('ca'), animatedRef = recorder();
            const closing = createCollapse(animated, animatedRef);
            await sleep(450);
            result.afterOpening = JSON.stringify(animatedRef.calls);
            animated.dataset.state = 'closed';
            let from = performance.now();
            closing.onClosing();
            closing.onClosing(); // the component says so on every render while it closes
            result.rightAfterClosing = JSON.stringify(animatedRef.calls);
            await until(() => animatedRef.calls.length > 0);
            result.closedAfter = performance.now() - from;
            await sleep(500);
            result.closedCalls = JSON.stringify(animatedRef.calls.splice(0));

            // Closed while it was still opening: the opening animation is cut off, and that is not
            // the end of the closing one.
            animated.dataset.state = 'open';
            await sleep(100);
            animated.dataset.state = 'closed';
            from = performance.now();
            closing.onClosing();
            await until(() => animatedRef.calls.length > 0);
            result.interruptedAfter = performance.now() - from;
            animatedRef.calls.length = 0;

            // Opened again in the middle of closing: there is no close to report any more.
            animated.dataset.state = 'open';
            await sleep(450);
            animated.dataset.state = 'closed';
            closing.onClosing();
            await sleep(100);
            animated.dataset.state = 'open';
            await sleep(700);
            result.reopened = JSON.stringify(animatedRef.calls);
            closing.dispose();
            host.remove();
            return result;
            """);
        string At(string name) => r.GetProperty(name).GetString()!;
        bool Reached(string name) => r.GetProperty(name).GetBoolean();

        // Both names of the variable (accordion and collapsible styles read one each) get the
        // content's height, rounded up so the last pixel is not clipped.
        Assert.Equal("81px 81px", At("measured"));
        // Content that changes while the panel is open is measured again, also through a wrapper
        // that is held at the old height (which is left as it was), and when nodes are added.
        Assert.True(Reached("grown"));
        Assert.True(Reached("throughThePin"));
        Assert.Equal("120px", At("pinKept"));
        Assert.True(Reached("added"));
        // Nothing to animate: closed is reported in the same breath.
        Assert.Equal("""[["OnCloseFinished"]]""", At("closedAtOnce"));
        // A closed panel is not measured (the closing animation owns the height), an opened one is.
        Assert.Equal("180px 180px", At("whileClosed"));
        Assert.True(Reached("openAgain"));
        // Only a match on the panel itself is the panel's to report.
        Assert.Equal("""[["OnBeforeMatch"]]""", At("found"));
        Assert.Equal("40px 40px []", At("disposed"));
        // A resized window is a reason to measure too.
        Assert.True(Reached("staleBeforeTheResize"));
        Assert.True(Reached("afterTheResize"));

        // The opening animation ending is nothing to report. The closing one is, once, when it
        // has played: not before its 300 ms, and not again from the timer that backs it up.
        Assert.Equal("[]", At("afterOpening"));
        Assert.Equal("[]", At("rightAfterClosing"));
        Assert.True(r.GetProperty("closedAfter").GetDouble() >= 280, $"closed after {r.GetProperty("closedAfter").GetDouble():0} ms");
        Assert.Equal("""[["OnCloseFinished"]]""", At("closedCalls"));
        Assert.True(r.GetProperty("interruptedAfter").GetDouble() >= 280, $"closed after {r.GetProperty("interruptedAfter").GetDouble():0} ms");
        Assert.Equal("[]", At("reopened"));
    }

    // A wait for something an observer or an animation does in its own time.
    private const string UntilScript =
        """
        const until = async (condition, ms = 3000) => {
            for (const end = performance.now() + ms; performance.now() < end; await sleep(20)) if (condition()) return true;
            return condition();
        };
        """;

    [E2EFact]
    public async Task Presence_reports_a_closed_surface_when_its_exit_has_played_and_hides_it_meanwhile()
    {
        await using var context = await NewContextWithMotionAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page, UntilScript +
            """
            const { createPresence } = await load('presence');
            const style = document.createElement('style');
            style.textContent =
                '@keyframes pr-in { from { opacity: 0 } to { opacity: 1 } }' +
                '@keyframes pr-out { from { opacity: 1 } to { opacity: 0 } }' +
                '.pr-animated[data-state=open] { animation: pr-in 300ms linear }' +
                '.pr-animated[data-state=closed] { animation: pr-out 300ms linear }';
            document.head.append(style);
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;width:200px;background:white';
            host.innerHTML =
                '<div id="pp" data-state="open">plain <span id="pp-part" class="pr-animated" data-state="open">a part with an animation of its own</span></div>' +
                '<div id="pa" class="pr-animated" data-state="open">animated</div>' +
                // Animated by its style, but not drawn: no animation event will ever come from it.
                '<div id="pn" class="pr-animated" data-state="open" style="display:none">not drawn</div>';
            document.body.append(host);
            const result = {};

            // A surface whose style has no exit animation.
            const plain = document.getElementById('pp'), plainRef = recorder();
            const presence = createPresence(plain, plainRef);
            plain.dataset.state = 'closed';
            presence.onClosing();
            result.plain = JSON.stringify(plainRef.calls.splice(0)) + ' ' + plain.style.visibility;
            // Opened again before the component took it away: the same element has to show.
            plain.dataset.state = 'open';
            await sleep(50);
            result.plainOpenAgain = plain.style.visibility;
            // An animation that ends inside the surface is not the surface's.
            plain.dataset.state = 'closed';
            document.getElementById('pp-part').dataset.state = 'closed';
            await sleep(500);
            result.partAnimation = JSON.stringify(plainRef.calls);
            presence.dispose();

            // With an exit animation of 300 ms.
            const animated = document.getElementById('pa'), animatedRef = recorder();
            const closing = createPresence(animated, animatedRef);
            await sleep(450);
            result.afterOpening = JSON.stringify(animatedRef.calls);
            animated.dataset.state = 'closed';
            let from = performance.now();
            closing.onClosing();
            closing.onClosing(); // the component says so on every render while it closes
            result.rightAfterClosing = JSON.stringify(animatedRef.calls) + ' ' + animated.style.visibility;
            await until(() => animatedRef.calls.length > 0);
            result.closedAfter = performance.now() - from;
            result.hiddenWhenReported = animated.style.visibility;
            await sleep(600);
            result.closedCalls = JSON.stringify(animatedRef.calls.splice(0));
            animated.dataset.state = 'open';
            await sleep(450);
            result.openAgain = animated.style.visibility;

            // Opened again in the middle of closing: nothing to report, nothing hidden.
            animated.dataset.state = 'closed';
            closing.onClosing();
            await sleep(100);
            animated.dataset.state = 'open';
            await sleep(700);
            result.reopened = JSON.stringify(animatedRef.calls) + ' ' + animated.style.visibility;

            // Disposed in the middle of closing: the report is the component's to do without.
            animated.dataset.state = 'closed';
            closing.onClosing();
            closing.dispose();
            await sleep(700);
            result.disposed = JSON.stringify(animatedRef.calls);

            const undrawn = document.getElementById('pn'), undrawnRef = recorder();
            const backedUp = createPresence(undrawn, undrawnRef);
            undrawn.dataset.state = 'closed';
            from = performance.now();
            backedUp.onClosing();
            result.undrawnAtOnce = JSON.stringify(undrawnRef.calls);
            await until(() => undrawnRef.calls.length > 0);
            result.undrawnAfter = performance.now() - from;
            backedUp.dispose();
            host.remove();
            return result;
            """);
        string At(string name) => r.GetProperty(name).GetString()!;

        // Nothing to wait for: reported at once, and hidden at once too, so that the surface does
        // not show at rest for the moment the component takes to remove it.
        Assert.Equal("""[["OnCloseFinished"]] hidden""", At("plain"));
        Assert.Equal("", At("plainOpenAgain"));
        Assert.Equal("[]", At("partAnimation"));

        // The entry animation ending is nothing to report. The exit is: once, when it has played,
        // with the surface hidden from that moment on.
        Assert.Equal("[]", At("afterOpening"));
        Assert.Equal("[] ", At("rightAfterClosing"));
        Assert.True(r.GetProperty("closedAfter").GetDouble() >= 280, $"closed after {r.GetProperty("closedAfter").GetDouble():0} ms");
        Assert.Equal("hidden", At("hiddenWhenReported"));
        Assert.Equal("""[["OnCloseFinished"]]""", At("closedCalls"));
        Assert.Equal("", At("openAgain"));
        Assert.Equal("[] ", At("reopened"));
        Assert.Equal("[]", At("disposed"));

        // No animation event can come from an element that is not drawn (or from a page in the
        // background): a timer set for the animation's length reports in its place.
        Assert.Equal("[]", At("undrawnAtOnce"));
        Assert.InRange(r.GetProperty("undrawnAfter").GetDouble(), 380, 2_000);
    }

    [E2EFact]
    public async Task Marquee_slides_cut_off_text_into_view_under_the_pointer_and_back()
    {
        await using var context = await NewContextWithMotionAsync();
        var page = await OpenAsync(context);

        var overflow = await RunAsync<double>(page,
            """
            const { createMarquee } = await load('marquee');
            const host = document.createElement('div');
            host.id = 'mq';
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;width:300px;background:white;font:16px/20px monospace';
            // Labels of one line, 20 px tall, from y = 120.
            const label = (id, text, width) =>
                '<div id="' + id + '" data-bz-marquee style="width:' + width + 'px;height:20px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + text + '</div>';
            window.__mqLabel = label;
            host.innerHTML = label('mq-long', 'a label that is much longer than its box', 100) + label('mq-short', 'short', 100) + label('mq-later', 'fits for now', 200);
            document.body.append(host);
            window.__marquee = createMarquee(host);
            // Per label: whether it is marked as cut off, and how far its text is pulled left.
            window.__mq = () => [...host.querySelectorAll('[data-bz-marquee]')]
                .map(el => (el.hasAttribute('data-truncated') ? 'cut' : 'fits') + ':' + (el.style.textIndent ? Math.round(parseFloat(el.style.textIndent)) : '-')).join(' ');
            await sleep(300);
            const long = document.getElementById('mq-long');
            return long.scrollWidth - long.clientWidth;
            """);
        Task<string> State() => page.EvaluateAsync<string>("() => window.__mq()");
        Task StateIs(string state) => page.WaitForFunctionAsync("state => window.__mq() === state", state, new() { Timeout = 6_000 });
        Task<double> Indent() => page.EvaluateAsync<double>("() => parseFloat(document.getElementById('mq-long').style.textIndent || '0')");

        // Only the label whose text does not fit is marked.
        Assert.Equal("cut:- fits:- fits:-", await State());
        Assert.True(overflow > 100, $"the long label overflows by {overflow} px");

        // Under the pointer its text slides left, a step per frame, until the end of it shows:
        // by exactly what was cut off.
        await page.Mouse.MoveAsync(90, 130);
        await page.WaitForFunctionAsync("() => parseFloat(document.getElementById('mq-long').style.textIndent || '0') < -5", null, new() { Timeout = 5_000 });
        var early = await Indent();
        Assert.InRange(early, -overflow + 1, -5);
        await StateIs($"cut:-{overflow} fits:- fits:-");
        // Without the pointer it slides back, and at rest nothing of the slide is left on it.
        await page.Mouse.MoveAsync(600, 600);
        await page.WaitForFunctionAsync("([low, high]) => { const indent = parseFloat(document.getElementById('mq-long').style.textIndent || '0'); return indent > low && indent < high; }",
            new[] { -overflow + 5, -5 }, new() { Timeout = 5_000 });
        await StateIs("cut:- fits:- fits:-");

        // A label that fits does not move.
        await page.Mouse.MoveAsync(90, 150);
        await page.WaitForTimeoutAsync(300);
        Assert.Equal("cut:- fits:- fits:-", await State());
        await page.Mouse.MoveAsync(600, 600);

        // Things change after the first look: one label's text grows too long, another gets the
        // room it needs, and a new label arrives. Nobody has to ask for a new measure.
        await page.EvaluateAsync(
            """
            () => {
                document.getElementById('mq-later').textContent = 'now this label is far too long for its two hundred pixels';
                document.getElementById('mq-long').style.width = '600px';
                document.getElementById('mq').insertAdjacentHTML('beforeend', window.__mqLabel('mq-new', 'a new label that is too long as well', 80));
            }
            """);
        await StateIs("fits:- fits:- cut:- cut:-");
        // The label that fits now is no longer armed.
        await page.Mouse.MoveAsync(90, 130);
        await page.WaitForTimeoutAsync(300);
        Assert.Equal("fits:- fits:- cut:- cut:-", await State());

        // Someone who asked for less motion gets the end of the text at once.
        await page.Mouse.MoveAsync(600, 600);
        await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        var cut = await page.EvaluateAsync<double>("() => { const el = document.getElementById('mq-new'); return el.scrollWidth - el.clientWidth; }");
        await page.Mouse.MoveAsync(90, 190);
        Assert.Equal($"fits:- fits:- cut:- cut:-{cut}", await State());
        await page.Mouse.MoveAsync(600, 600);
        Assert.Equal("fits:- fits:- cut:- cut:-", await State());
        await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.NoPreference });

        // Disposed in the middle of a slide: the text is put back, and the pointer starts nothing.
        await page.Mouse.MoveAsync(90, 170);
        await page.WaitForFunctionAsync("() => parseFloat(document.getElementById('mq-later').style.textIndent || '0') < -5", null, new() { Timeout = 5_000 });
        await page.EvaluateAsync("() => window.__marquee.dispose()");
        Assert.Equal("fits:- fits:- cut:- cut:-", await State());
        await page.Mouse.MoveAsync(600, 600);
        await page.Mouse.MoveAsync(90, 170);
        await page.WaitForTimeoutAsync(300);
        Assert.Equal("fits:- fits:- cut:- cut:-", await State());
    }

    [E2EFact]
    public async Task Portal_moves_a_surface_to_the_body_and_puts_it_back_where_it_came_from()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page,
            """
            const { createPortal } = await load('portal');
            const host = document.createElement('div');
            host.innerHTML =
                // Declared inside something that clips: a dialog's overlay and then its content.
                '<div id="po-clip" style="overflow:hidden;width:10px;height:10px"><span id="po-before">before</span><div id="po-overlay">overlay</div><div id="po-content">content</div><span id="po-after">after</span></div>' +
                // Declared inside a part of the page that pins its own theme.
                '<div id="po-themed" data-bz-portal-frame="po-theme po-dark"><div id="po-framed">framed</div><div id="po-framed-too">framed too</div></div>' +
                '<div id="po-leaving"><div id="po-orphan">orphan</div></div><div id="po-gone-home"><div id="po-gone">gone</div></div>';
            document.body.append(host);
            const el = id => document.getElementById(id);
            // The clipping parent's children, a marker standing in for each surface that is away.
            const home = () => [...el('po-clip').childNodes].map(node => node.nodeType === Node.COMMENT_NODE ? '(marker)' : node.id.slice(3)).join(' ');
            const inBody = () => [...document.body.children].filter(child => child.id.startsWith('po-') && child !== host).map(child => child.id.slice(3)).join(' ');
            const frames = () => [...document.body.children].filter(child => child.hasAttribute('data-bz-portal-frame'))
                .map(frame => frame.className + ' (' + frame.style.display + '): ' + [...frame.children].map(child => child.id.slice(3)).join(' ')).join(' | ');
            const result = {};

            // The scripts of the two parts land in either order: here the content's first.
            const content = createPortal(el('po-content'));
            const overlay = createPortal(el('po-overlay'));
            result.inBody = inBody();
            result.homeWhileAway = home();
            content.dispose();
            result.contentBack = home();
            overlay.dispose();
            result.allBack = home() + ' / ' + inBody();

            const framed = createPortal(el('po-framed'));
            result.frame = frames();
            const framedToo = createPortal(el('po-framed-too'));
            result.frameShared = frames();
            framed.dispose();
            result.frameWithOneLeft = frames();
            framedToo.dispose();
            result.frameEmptied = frames() + ' / ' + [...el('po-themed').children].map(child => child.id.slice(3)).join(' ');

            // The page the surface was declared in goes away first (a navigation).
            const orphan = createPortal(el('po-orphan'));
            el('po-leaving').remove();
            orphan.dispose();
            result.orphanLeft = el('po-orphan') !== null;

            // The component went away while its surface sat in the body, and took the element with it.
            const gone = el('po-gone');
            const portal = createPortal(gone);
            gone.remove();
            portal.dispose();
            result.goneBack = gone.isConnected || el('po-gone-home').childNodes.length > 0;

            // Nothing to move: a handle that does nothing.
            createPortal(null).dispose();
            host.remove();
            return result;
            """);
        string At(string name) => r.GetProperty(name).GetString()!;

        // Both sit in the body, out of reach of the parent that clips, and in the order they were
        // declared in: the overlay stays under the content it belongs to.
        Assert.Equal("overlay content", At("inBody"));
        // A marker holds each one's place; each goes back to exactly its own.
        Assert.Equal("before (marker) (marker) after", At("homeWhileAway"));
        Assert.Equal("before (marker) content after", At("contentBack"));
        Assert.Equal("before overlay content after / ", At("allBack"));

        // Under a part of the page that pins a theme the surface goes into a frame in the body
        // that carries the same classes and has no box of its own. Surfaces of one theme share a
        // frame, and the frame goes when the last of them has left.
        Assert.Equal("po-theme po-dark (contents): framed", At("frame"));
        Assert.Equal("po-theme po-dark (contents): framed framed-too", At("frameShared"));
        Assert.Equal("po-theme po-dark (contents): framed-too", At("frameWithOneLeft"));
        Assert.Equal(" / framed framed-too", At("frameEmptied"));

        // With no home left to go back to the surface is removed, not left behind in the body.
        Assert.False(r.GetProperty("orphanLeft").GetBoolean());
        // An element that is already gone is not put back into the page.
        Assert.False(r.GetProperty("goneBack").GetBoolean());
    }

    [E2EFact]
    public async Task Copy_control_writes_during_the_click_and_tells_its_owner_how_it_went()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { register, unregister, writeText } = await load('clipboard');
            window.__clip = { unregister, writeText };
            window.__written = [];
            window.__refuse = false;
            Object.defineProperty(navigator.clipboard, 'writeText', { configurable: true,
                value: text => { window.__written.push(text); return window.__refuse ? Promise.reject(new Error('refused')) : Promise.resolve(); } });
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white';
            host.innerHTML =
                // A code block: three lines, the second one empty, and a gutter that is not code.
                '<pre id="cp-lines"><span data-bz-copy-line>first</span><span>gutter</span><span data-bz-copy-line>\n</span><span data-bz-copy-line>third</span></pre>' +
                '<button id="cp-text" data-bz-copy="cp-one" data-bz-copy-text="plain text"><span id="cp-text-icon">icon</span></button>' +
                '<button id="cp-empty" data-bz-copy="cp-one" data-bz-copy-text="">empty</button>' +
                '<button id="cp-from" data-bz-copy="cp-two" data-bz-copy-from="#cp-lines">from</button>' +
                '<button id="cp-missing" data-bz-copy="cp-two" data-bz-copy-from="#cp-no-such-source">missing</button>' +
                '<button id="cp-invalid" data-bz-copy="cp-two" data-bz-copy-from="##">invalid</button>' +
                '<button id="cp-nothing" data-bz-copy="cp-two">nothing</button>' +
                '<button id="cp-disabled" data-bz-copy="cp-one" data-bz-copy-text="x" disabled>disabled</button>' +
                '<a id="cp-aria" data-bz-copy="cp-one" data-bz-copy-text="x" aria-disabled="true">off</a>' +
                '<div id="cp-unowned" data-bz-copy="cp-nobody" data-bz-copy-text="nobody listens" style="width:60px;height:20px">div</div>' +
                '<button id="cp-plain">not a copy control</button><input id="cp-field" />';
            document.body.append(host);
            window.__one = recorder();
            window.__two = recorder();
            register('cp-one', window.__one);
            register('cp-two', window.__two);
            return true;
            """);
        // After a click: [what was written, what the first owner heard, what the second owner heard].
        async Task<string> After(Func<Task> act)
        {
            await act();
            await page.WaitForTimeoutAsync(60); // the outcome arrives once the write has settled
            return await page.EvaluateAsync<string>(
                "() => JSON.stringify([window.__written.splice(0), window.__one.calls.splice(0), window.__two.calls.splice(0)])");
        }
        Task<string> Click(string selector) => After(() => page.ClickAsync(selector));

        // The text on the control itself, from a click anywhere inside it. Empty text is a text.
        Assert.Equal("""[["plain text"],[["OnCopyResult",true]],[]]""", await Click("#cp-text-icon"));
        Assert.Equal("""[[""],[["OnCopyResult",true]],[]]""", await Click("#cp-empty"));
        // The text of another element: its marked lines, one per line, an empty one where a line
        // holds only its line break, and nothing of what is not marked. The outcome goes to the
        // owner the control names, and to nobody else.
        Assert.Equal("""[["first\n\nthird"],[],[["OnCopyResult",true]]]""", await Click("#cp-from"));
        // Enter on the control is a click like any other.
        Assert.Equal("""[["first\n\nthird"],[],[["OnCopyResult",true]]]""", await After(async () =>
        {
            await page.FocusAsync("#cp-from");
            await page.Keyboard.PressAsync("Enter");
        }));

        // Nothing to copy (no such source, a selector that is not one, no source named at all):
        // nothing is written and the owner hears that it failed.
        foreach (var control in new[] { "#cp-missing", "#cp-invalid", "#cp-nothing" })
            Assert.Equal("""[[],[],[["OnCopyResult",false]]]""", await Click(control));
        // A write the browser refuses is a failure too.
        await page.EvaluateAsync("() => { window.__refuse = true; }");
        Assert.Equal("""[["plain text"],[["OnCopyResult",false]],[]]""", await Click("#cp-text"));
        await page.EvaluateAsync("() => { window.__refuse = false; }");

        // A disabled control copies nothing, natively disabled or said to be.
        Assert.Equal("[[],[],[]]", await After(() => page.ClickAsync("#cp-disabled", new() { Force = true })));
        Assert.Equal("[[],[],[]]", await Click("#cp-aria"));
        // No owner (or one that has gone): the copy still happens, there is just nobody to tell.
        Assert.Equal("""[["nobody listens"],[],[]]""", await Click("#cp-unowned"));
        await page.EvaluateAsync("() => window.__clip.unregister('cp-one')");
        Assert.Equal("""[["plain text"],[],[]]""", await Click("#cp-text"));
        Assert.Equal("[[],[],[]]", await Click("#cp-plain"));

        // A page that is not a secure context (plain http on a network address) has no clipboard
        // API. The copy then goes through a selected, read-only text box that is gone again
        // afterwards, with focus back where it was.
        await page.EvaluateAsync(
            """
            () => {
                Object.defineProperty(window, 'isSecureContext', { configurable: true, get: () => false });
                window.__commands = [];
                window.__commandWorks = true;
                document.execCommand = command => {
                    const box = document.activeElement;
                    window.__commands.push([command, box.tagName, box.value, box.selectionStart, box.selectionEnd, box.readOnly].join('|'));
                    return window.__commandWorks;
                };
            }
            """);
        Assert.Equal("""[[],[],[["OnCopyResult",true]]]""", await Click("#cp-from"));
        Same(["copy|TEXTAREA|first\n\nthird|0|12|true"], await DrainAsync(page, "__commands"));
        Assert.Equal("cp-from", await ActiveIdAsync(page));
        Assert.Equal(0, await page.Locator("body > textarea").CountAsync());
        await page.EvaluateAsync("() => { window.__commandWorks = false; }");
        Assert.Equal("""[[],[],[["OnCopyResult",false]]]""", await Click("#cp-from"));

        // The same write for code that copies by itself (a component's own Copy method).
        await page.FocusAsync("#cp-field");
        Assert.False(await page.EvaluateAsync<bool>("() => window.__clip.writeText('direct')"));
        Assert.Equal("cp-field", await ActiveIdAsync(page));
    }

    [E2EFact]
    public async Task Scroll_lock_holds_the_page_until_the_last_surface_that_asked_lets_go()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page, UntilScript +
            """
            const { lock, unlock } = await load('scrollLock');
            const html = document.documentElement, body = document.body;
            // [overflow, padding on the scrollbar's side, marked as locked], as set on the body itself.
            const state = () => [body.style.overflow || '-', body.style.paddingRight || '-', body.hasAttribute('data-scroll-locked') ? 'locked' : 'free'].join(' ');
            const result = { before: state() };

            // A dialog opens, and a second one from it.
            lock();
            result.locked = state();
            lock();
            unlock();
            result.oneStillOpen = state();
            unlock();
            result.released = state();
            // Letting go more often than was asked for does not leave the count below zero: the
            // next lock still has to be undone by one unlock.
            unlock();
            lock();
            unlock();
            result.afterOneTooMany = state();

            // What the body had set on it before is what it gets back.
            body.style.overflow = 'clip';
            lock();
            result.lockedOverOwnValue = state();
            unlock();
            result.ownValueBack = state();
            body.style.overflow = '';

            // A scrollbar that takes room (15 px) would let the page jump sideways when it goes.
            // This browser draws none, so the window is made to report itself that much wider
            // than the document, which is how the script measures one.
            const inner = Object.getOwnPropertyDescriptor(window, 'innerWidth');
            Object.defineProperty(window, 'innerWidth', { configurable: true, get: () => html.clientWidth + 15 });
            result.gutter = getComputedStyle(html).scrollbarGutter;
            // The docs page keeps the scrollbar's room reserved at all times: nothing to make up for.
            lock();
            result.withReservedGutter = state();
            unlock();
            // A page that does not: the body is padded by the scrollbar's width, on top of what it has.
            html.style.setProperty('scrollbar-gutter', 'auto', 'important');
            body.style.paddingRight = '7px';
            await until(() => getComputedStyle(body).paddingRight === '7px' && getComputedStyle(html).scrollbarGutter === 'auto');
            lock();
            result.padded = state();
            unlock();
            result.paddingBack = state();

            body.style.paddingRight = '';
            html.style.removeProperty('scrollbar-gutter');
            Object.defineProperty(window, 'innerWidth', inner);
            return result;
            """);
        string At(string name) => r.GetProperty(name).GetString()!;

        Assert.Equal("- - free", At("before"));
        Assert.Equal("hidden - locked", At("locked"));
        Assert.Equal("hidden - locked", At("oneStillOpen"));
        Assert.Equal("- - free", At("released"));
        Assert.Equal("- - free", At("afterOneTooMany"));
        Assert.Equal("hidden - locked", At("lockedOverOwnValue"));
        Assert.Equal("clip - free", At("ownValueBack"));
        Assert.Contains("stable", At("gutter"));
        Assert.Equal("hidden - locked", At("withReservedGutter"));
        Assert.Equal("hidden 22px locked", At("padded"));
        Assert.Equal("- 7px free", At("paddingBack"));
    }

    [E2EFact]
    public async Task Image_fill_loads_a_chosen_picture_turns_it_and_hands_it_back_as_data()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page,
            """
            const { loadImage, showImage, rotateImage, exportImage, releaseImage } = await load('imageFill');
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:40px;top:120px;background:white';
            host.innerHTML = '<div id="if-zone"><input id="if-input" type="file" /></div><img id="if-preview" hidden /><img id="if-other" hidden />';
            document.body.append(host);
            const input = document.getElementById('if-input'), preview = document.getElementById('if-preview'), other = document.getElementById('if-other');

            // A picture two pixels wide and one high: red on the left, blue on the right.
            const canvas = document.createElement('canvas');
            canvas.width = 2;
            canvas.height = 1;
            const paint = canvas.getContext('2d');
            paint.fillStyle = '#ff0000'; paint.fillRect(0, 0, 1, 1);
            paint.fillStyle = '#0000ff'; paint.fillRect(1, 0, 1, 1);
            const png = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
            const asDataUrl = blob => new Promise(resolve => { const reader = new FileReader(); reader.onload = () => resolve(reader.result); reader.readAsDataURL(blob); });
            // What a user's choice leaves on the file input, with no file dialog involved.
            const choose = file => { const transfer = new DataTransfer(); transfer.items.add(file); input.files = transfer.files; };
            // An image as "width x height: colour of each pixel".
            const describe = image => {
                const copy = document.createElement('canvas');
                copy.width = image.naturalWidth;
                copy.height = image.naturalHeight;
                const context = copy.getContext('2d');
                context.drawImage(image, 0, 0);
                const data = context.getImageData(0, 0, copy.width, copy.height).data, names = [];
                for (let i = 0; i < data.length; i += 4) {
                    const [red, green, blue] = [data[i], data[i + 1], data[i + 2]];
                    names.push(red > 200 && green < 50 && blue < 50 ? 'red' : blue > 200 && red < 50 && green < 50 ? 'blue' : red === green && green === blue ? 'grey' : 'other');
                }
                return copy.width + 'x' + copy.height + ': ' + names.join(' ');
            };
            const decoded = url => new Promise(resolve => { const image = new Image(); image.onload = () => resolve(image); image.src = url; });
            const alive = url => fetch(url).then(() => true, () => false);
            const result = {};

            result.nothingChosen = await loadImage(input, preview);
            choose(new File(['not a picture'], 'notes.txt', { type: 'text/plain' }));
            result.notAPicture = await loadImage(input, preview);
            choose(new File(['a picture in name only'], 'broken.png', { type: 'image/png' }));
            result.cannotBeDecoded = await loadImage(input, preview);

            // Given the dropzone around the input, as the picker gives it.
            choose(new File([png], 'two.png', { type: 'image/png' }));
            const loaded = await loadImage(document.getElementById('if-zone'), preview);
            result.loaded = loaded.startsWith('blob:') && preview.src === loaded ? describe(preview) : 'not shown: ' + loaded;
            // Untouched, it is handed back as the file's own bytes.
            result.exportedAsItWas = (await exportImage(preview, '')) === (await asDataUrl(png)) && (await exportImage(preview, 'none')) === (await asDataUrl(png));

            const turned = await rotateImage(preview);
            result.turned = turned.startsWith('blob:') && turned !== loaded && preview.src === turned ? describe(preview) : 'not shown: ' + turned;
            result.firstUrlAlive = await alive(loaded);
            // With tone adjustments the export is the picture as it looks: turned, and graded.
            const graded = await exportImage(preview, 'grayscale(1)');
            result.graded = graded.startsWith('data:image/png;base64,') ? describe(await decoded(graded)) : 'not a png: ' + graded.slice(0, 30);

            // A fresh preview element (the surface was left and entered again) knows nothing yet,
            // then takes the picture over by its address.
            result.unknownPreview = await exportImage(other, '');
            result.shown = (await showImage(other, turned)) && (await showImage(other, turned)) ? describe(other) : 'not shown';
            result.shownExports = ((await exportImage(other, '')) ?? '').startsWith('data:image/png;base64,');
            result.badAddress = await showImage(other, 'blob:' + location.origin + '/00000000-0000-0000-0000-000000000000');
            result.nothingToTurn = await rotateImage(document.createElement('img'));

            // Released (the component is gone): the address is given up, and there is nothing to export.
            releaseImage(preview);
            releaseImage(preview);
            result.releasedUrlAlive = await alive(turned);
            result.exportAfterRelease = await exportImage(preview, '');
            host.remove();
            return result;
            """);
        static bool IsNull(JsonElement value) => value.ValueKind == JsonValueKind.Null;

        // No file, a file that is not a picture, a picture that cannot be decoded: no address.
        Assert.True(IsNull(r.GetProperty("nothingChosen")));
        Assert.True(IsNull(r.GetProperty("notAPicture")));
        Assert.True(IsNull(r.GetProperty("cannotBeDecoded")));
        Assert.Equal("2x1: red blue", r.GetProperty("loaded").GetString());
        Assert.True(r.GetProperty("exportedAsItWas").GetBoolean());
        // A quarter turn clockwise: what was on the left is on top. The turn is in the picture
        // itself, and the address of the picture before it is given up.
        Assert.Equal("1x2: red blue", r.GetProperty("turned").GetString());
        Assert.False(r.GetProperty("firstUrlAlive").GetBoolean());
        Assert.Equal("1x2: grey grey", r.GetProperty("graded").GetString());
        Assert.True(IsNull(r.GetProperty("unknownPreview")));
        Assert.Equal("1x2: red blue", r.GetProperty("shown").GetString());
        Assert.True(r.GetProperty("shownExports").GetBoolean());
        Assert.False(r.GetProperty("badAddress").GetBoolean());
        Assert.True(IsNull(r.GetProperty("nothingToTurn")));
        Assert.False(r.GetProperty("releasedUrlAlive").GetBoolean());
        Assert.True(IsNull(r.GetProperty("exportAfterRelease")));
    }
}
