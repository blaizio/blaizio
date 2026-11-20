using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

// The modules behind lists: the tree's drag and drop and its scroll watcher, the highlight a
// command palette and a combobox keep while focus stays in their text field, and the scroll
// buttons of a select.
public sealed partial class ScriptModuleTests
{
    // Rows as the tree renders them. A leaf is one element, node and row at once. A branch is a
    // node around its row and, while it is open, around its children. Every row is 24 px tall and
    // indented 20 px a level; a tree is 240 px wide and ends in 40 px with no row.
    private const string TreeMarkup =
        """
        const leaf = (value, depth, more = '') =>
            '<div role="treeitem" data-tree-node data-part="item" data-tree-row data-value="' + value + '" data-depth="' + depth + '" tabindex="-1" ' + more +
            ' style="height:24px;margin-left:' + depth * 20 + 'px">' + value + '</div>';
        const branch = (value, depth, open, children, more = '') =>
            '<div role="treeitem" data-tree-node data-part="branch" data-state="' + (open ? 'open' : 'closed') + '" aria-expanded="' + open +
            '" data-value="' + value + '" data-depth="' + depth + '" tabindex="-1">' +
            '<div data-part="branch-control" data-tree-row data-branch data-value="' + value + '" data-depth="' + depth + '" data-state="' + (open ? 'open' : 'closed') + '" ' + more +
            ' style="height:24px;margin-left:' + depth * 20 + 'px">' + value + '</div>' +
            (open ? '<div role="group" data-part="branch-content">' + children + '</div>' : '') + '</div>';
        const tree = (id, left, top, rows, attributes = '') =>
            '<div id="' + id + '" data-slot="tree" role="tree" ' + attributes + ' style="position:fixed;z-index:99999;left:' + left + 'px;top:' + top +
            'px;width:240px;padding-bottom:40px;background:white;font:12px/24px sans-serif;--bz-tree-indent:30px">' + rows + '</div>';
        // What the component passes when it creates the script side of a tree.
        const options = (id, more = {}) => ({ id, group: null, disabled: false, dragEnabled: true, delayMs: 0, hoverExpandMs: 0,
            transferIn: true, transferOut: true, indicatorClass: null, ...more });
        // The drop marker as [kind, left, top, width, height], or null while there is none.
        window.__treeMarker = () => {
            const marker = document.querySelector('[data-slot=tree-drop-indicator]');
            return JSON.stringify(marker && [marker.dataset.variant, parseFloat(marker.style.left), parseFloat(marker.style.top),
                parseFloat(marker.style.width), parseFloat(marker.style.height)]);
        };
        // What a drag leaves on the page while it lasts: [rows of the ghost as "value:opacity:indent",
        // how far the ghost moved, the nodes marked as dragged, the mark on the page, text selection].
        window.__treeDrag = () => {
            const ghost = document.querySelector('[data-slot=tree-ghost]');
            return JSON.stringify([
                ghost ? [...ghost.children].map(row => row.dataset.value + ':' + row.style.opacity + ':' + getComputedStyle(row).marginLeft) : [],
                ghost ? ghost.style.transform : '',
                [...document.querySelectorAll('[data-dragging]')].map(node => node.dataset.value),
                document.documentElement.hasAttribute('data-bz-tree-drag'), document.body.style.userSelect]);
        };
        """;

    private const string NoTreeDrag = """[[],"",[],false,""]""";

    private static Task<string> TreeMarkerAsync(IPage page) => page.EvaluateAsync<string>("() => window.__treeMarker()");

    private static Task<string> TreeDragStateAsync(IPage page) => page.EvaluateAsync<string>("() => window.__treeDrag()");

    // Press at one point, release at another: one move in between, as a quick drag is.
    private static async Task DragFromToAsync(IPage page, float fromX, float fromY, float toX, float toY)
    {
        await page.Mouse.MoveAsync(fromX, fromY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(toX, toY);
        await page.Mouse.UpAsync();
    }

    [E2EFact]
    public async Task Tree_keeps_its_keys_from_scrolling_the_page_and_leaves_a_rename_field_its_own()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, TreeMarkup +
            """
            const { createTree } = await load('tree');
            const host = document.createElement('div');
            // One tree with a row that is being renamed, one where many rows can be selected.
            host.innerHTML =
                tree('tk', 50, 50, leaf('a', 0) +
                    '<div role="treeitem" data-tree-node data-tree-row data-value="r" data-depth="0" tabindex="-1" style="height:24px"><input id="tk-rename" value="name" /></div>',
                    'tabindex="0"') +
                tree('tk-many', 320, 50, leaf('m', 0), 'aria-multiselectable="true"');
            document.body.append(host);
            window.__tkTree = createTree(document.getElementById('tk'), recorder(), options('k1'));
            createTree(document.getElementById('tk-many'), recorder(), options('k2'));
            window.__treeKeys = [];
            // On the window, so it sees what the tree left of each key.
            window.addEventListener('keydown', e => {
                if (e.key !== 'Control') window.__treeKeys.push((e.ctrlKey ? 'Ctrl+' : '') + e.key + ':' + e.defaultPrevented);
            });
            return true;
            """);
        async Task<string[]> Pressed(string selector, params string[] keys)
        {
            await page.FocusAsync(selector);
            foreach (var key in keys) await page.Keyboard.PressAsync(key);
            return await page.EvaluateAsync<string[]>("() => window.__treeKeys.splice(0)");
        }

        // The keys the component moves and toggles rows with would scroll the page as well; the
        // rest (Enter, letters for type-ahead, the page keys) have nothing to take away.
        Same(["ArrowUp:true", "ArrowDown:true", "ArrowLeft:true", "ArrowRight:true", "Home:true", "End:true", " :true",
                "Enter:false", "a:false", "PageDown:false", "Ctrl+a:false"],
            await Pressed("#tk [data-value=a]", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End", "Space",
                "Enter", "a", "PageDown", "Control+a"));

        // Where many rows can be selected, Ctrl+A selects the rows, not the text of the page.
        Same(["Ctrl+a:true", "Ctrl+A:true", "a:false"], await Pressed("#tk-many [data-value=m]", "Control+a", "Control+A", "a"));

        // A row's rename field keeps every key: the caret moves, the space is typed.
        Same(["ArrowLeft:false", "Home:false", " :false", "Ctrl+a:false"],
            await Pressed("#tk-rename", "ArrowLeft", "Home", "Space", "Control+a"));
        Assert.Equal(" name", await page.InputValueAsync("#tk-rename"));

        // With focus on the tree itself, on no row, the keys are the page's.
        Same(["ArrowDown:false", " :false"], await Pressed("#tk", "ArrowDown", "Space"));

        await page.EvaluateAsync("() => window.__tkTree.dispose()");
        Same(["ArrowDown:false", " :false"], await Pressed("#tk [data-value=a]", "ArrowDown", "Space"));
    }

    [E2EFact]
    public async Task Tree_drag_marks_where_the_node_would_land_and_reports_the_drop()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, TreeMarkup +
            """
            const { createTree } = await load('tree');
            const host = document.createElement('div');
            // a 50..74, b 74..98 (open: b1 98..122, b2 122..146 (open: b2x 146..170)), c 170..194 (closed),
            // d 194..218, then no row down to 258.
            host.innerHTML = tree('tr', 50, 50,
                leaf('a', 0) + branch('b', 0, true, leaf('b1', 1, 'id="tr-b1"') + branch('b2', 1, true, leaf('b2x', 2))) + branch('c', 0, false, '') + leaf('d', 0));
            document.body.append(host);
            window.__tr = recorder();
            createTree(document.getElementById('tr'), window.__tr, options('t1'));
            // Every click the browser makes, and the ones that reach the rows.
            window.__trClicks = [];
            window.addEventListener('click', () => window.__trClicks.push('made'), true);
            document.getElementById('tr').addEventListener('click', e => window.__trClicks.push('row ' + (e.target.dataset.value ?? '')));
            return true;
            """);
        Task<string[]> Clicks() => page.EvaluateAsync<string[]>("() => window.__trClicks.splice(0)");

        // A press that moves less than 5 px is a click in the making, not a drag.
        await page.Mouse.MoveAsync(100, 62);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(103, 62);
        Assert.Equal("[]", await TakeCallsAsync(page, "__tr"));
        Assert.Equal(NoTreeDrag, await TreeDragStateAsync(page));

        // Further, it is one: a copy of the row follows the pointer while the row itself stays
        // where it is, marked. The upper half of a leaf means "before it": a line on its top edge.
        await page.Mouse.MoveAsync(100, 199);
        Assert.Equal("""[["NotifyDragStart","a"]]""", await TakeCallsAsync(page, "__tr"));
        Assert.Equal("""[["a:1:0px"],"translate(0px, 137px)",["a"],true,"none"]""", await TreeDragStateAsync(page));
        Assert.Equal("""["line",50,192.5,240,3]""", await TreeMarkerAsync(page));
        // The lower half means "after it".
        await page.Mouse.MoveAsync(100, 214);
        Assert.Equal("""["line",50,216.5,240,3]""", await TreeMarkerAsync(page));

        // A branch takes the node in from its middle half, and is a sibling from its top and
        // bottom quarters.
        await page.Mouse.MoveAsync(100, 182);
        Assert.Equal("""["inside",50,170,240,24]""", await TreeMarkerAsync(page));
        await page.Mouse.MoveAsync(100, 172);
        Assert.Equal("""["line",50,168.5,240,3]""", await TreeMarkerAsync(page));
        await page.Mouse.MoveAsync(100, 192);
        Assert.Equal("""["line",50,192.5,240,3]""", await TreeMarkerAsync(page));
        // "After" an open branch is after everything in it: the line is under its last child.
        await page.Mouse.MoveAsync(100, 96);
        Assert.Equal("""["line",50,168.5,240,3]""", await TreeMarkerAsync(page));
        // Over the row it started from there is nowhere to land.
        await page.Mouse.MoveAsync(100, 62);
        Assert.Equal("null", await TreeMarkerAsync(page));
        // The line is as wide as the row it is for, so its indent says which level the node joins.
        await page.Mouse.MoveAsync(120, 116);
        Assert.Equal("""["line",70,120.5,220,3]""", await TreeMarkerAsync(page));

        // The release ends the drag and reports the move; nothing of it stays on the page, and
        // the click the browser makes after the release does not reach the rows.
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragEnd","a"],["NotifyMove","a","b1","after","t1","t1"]]""", await TakeCallsAsync(page, "__tr"));
        Assert.Equal(NoTreeDrag, await TreeDragStateAsync(page));
        Assert.Equal("null", await TreeMarkerAsync(page));
        Same(["made"], await Clicks());
        // The next click is an ordinary one.
        await page.Mouse.ClickAsync(100, 62);
        Same(["made", "row a"], await Clicks());

        // A branch is dragged with a hint of what is in it: its row and two more, fading, indented
        // from the dragged row by the tree's own indent. No copy keeps an id.
        await page.Mouse.MoveAsync(100, 86);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(100, 110);
        Assert.Equal("""[["b:1:0px","b1:0.55:30px","b2:0.3:30px"],"translate(0px, 24px)",["b"],true,"none"]""", await TreeDragStateAsync(page));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => document.querySelectorAll('#tr-b1').length"));
        // It cannot land inside itself, however deep.
        Assert.Equal("null", await TreeMarkerAsync(page));
        await page.Mouse.MoveAsync(100, 158);
        Assert.Equal("null", await TreeMarkerAsync(page));
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragStart","b"],["NotifyDragEnd","b"]]""", await TakeCallsAsync(page, "__tr"));

        // Under the last row: after the last node of the top level.
        await page.Mouse.MoveAsync(100, 62);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(100, 240);
        Assert.Equal("""["line",50,216.5,240,3]""", await TreeMarkerAsync(page));
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragStart","a"],["NotifyDragEnd","a"],["NotifyMove","a","d","after","t1","t1"]]""", await TakeCallsAsync(page, "__tr"));

        // That last node itself, dropped there, has no row to come after: it goes to the top level
        // as such, and the marker frames the whole tree.
        await page.Mouse.MoveAsync(100, 206);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(100, 240);
        Assert.Equal("""["inside",50,50,240,208]""", await TreeMarkerAsync(page));
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragStart","d"],["NotifyDragEnd","d"],["NotifyMove","d",null,"inside","t1","t1"]]""", await TakeCallsAsync(page, "__tr"));
    }

    [E2EFact]
    public async Task Tree_drag_does_not_start_from_a_control_a_pinned_row_or_before_the_hold_is_over()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, TreeMarkup +
            """
            const { createTree } = await load('tree');
            const host = document.createElement('div');
            // a 50..74 (with a button and a grip that is not for dragging), off 74..98 (disabled),
            // pin 98..122 (not to be dragged), z 122..146
            host.innerHTML =
                tree('tn', 50, 50,
                    '<div role="treeitem" data-tree-node data-tree-row data-value="a" data-depth="0" tabindex="-1" style="height:24px">a' +
                    '<button style="position:absolute;left:100px;top:2px;height:20px">act</button>' +
                    '<span data-no-drag style="position:absolute;left:180px;top:2px;width:20px;height:20px;background:silver"></span></div>' +
                    leaf('off', 0, 'data-disabled') + leaf('pin', 0, 'data-drag-disabled') + leaf('z', 0)) +
                tree('th', 320, 50, leaf('h1', 0) + leaf('h2', 0) + leaf('h3', 0));
            document.body.append(host);
            window.__tn = recorder(); window.__th = recorder();
            window.__tnTree = createTree(document.getElementById('tn'), window.__tn, options('n1'));
            // A tree made for touch: the row has to be held before it comes loose.
            createTree(document.getElementById('th'), window.__th, options('held', { delayMs: 600 }));
            return true;
            """);

        // From a control inside a row, from a part marked as not for dragging, from a disabled or
        // a pinned row, with another mouse button: the press is not the tree's.
        await DragFromToAsync(page, 160, 62, 160, 134);
        await DragFromToAsync(page, 240, 62, 240, 134);
        await DragFromToAsync(page, 60, 86, 60, 134);
        await DragFromToAsync(page, 60, 110, 60, 134);
        await page.Mouse.MoveAsync(60, 62);
        await page.Mouse.DownAsync(new() { Button = MouseButton.Right });
        await page.Mouse.MoveAsync(60, 134);
        await page.Mouse.UpAsync(new() { Button = MouseButton.Right });
        Assert.Equal("[]", await TakeCallsAsync(page, "__tn"));

        // The component turns dragging off and on again as its parameters change.
        await page.EvaluateAsync("() => window.__tnTree.update({ disabled: true })");
        await DragFromToAsync(page, 60, 62, 60, 134);
        await page.EvaluateAsync("() => window.__tnTree.update({ disabled: false, dragEnabled: false })");
        await DragFromToAsync(page, 60, 62, 60, 134);
        Assert.Equal("[]", await TakeCallsAsync(page, "__tn"));
        await page.EvaluateAsync("() => window.__tnTree.update({ dragEnabled: true })");
        await DragFromToAsync(page, 60, 62, 60, 140);
        Assert.Equal("""[["NotifyDragStart","a"],["NotifyDragEnd","a"],["NotifyMove","a","z","after","n1","n1"]]""", await TakeCallsAsync(page, "__tn"));

        // Hold to drag: a pointer that moves off before the hold is over was scrolling, and does
        // not become a drag later in the same press either.
        await page.Mouse.MoveAsync(380, 62);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(380, 90);
        await page.WaitForTimeoutAsync(700);
        await page.Mouse.MoveAsync(380, 116);
        await page.Mouse.UpAsync();
        Assert.Equal("[]", await TakeCallsAsync(page, "__th"));
        // Held first, it drags.
        await page.Mouse.MoveAsync(380, 62);
        await page.Mouse.DownAsync();
        await page.WaitForTimeoutAsync(700);
        await page.Mouse.MoveAsync(380, 80);
        await page.Mouse.MoveAsync(380, 116);
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragStart","h1"],["NotifyDragEnd","h1"],["NotifyMove","h1","h3","after","held","held"]]""", await TakeCallsAsync(page, "__th"));
    }

    [E2EFact]
    public async Task Tree_drag_called_off_moves_nothing_and_leaves_nothing_behind()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, TreeMarkup +
            """
            const { createTree } = await load('tree');
            const host = document.createElement('div');
            // a 50..74, b 74..98 (with a button), c 98..122
            host.innerHTML = tree('tx', 50, 50,
                leaf('a', 0) + '<div role="treeitem" data-tree-node data-tree-row data-value="b" data-depth="0" tabindex="-1" style="height:24px">b' +
                '<button id="tx-action" style="position:absolute;left:100px;top:26px;height:20px">more</button></div>' + leaf('c', 0));
            document.body.append(host);
            window.__tx = recorder();
            window.__txTree = createTree(document.getElementById('tx'), window.__tx, options('x1'));
            window.__txEscapes = 0;
            window.__txActions = 0;
            window.__txPointer = -1;
            // What a dialog around the tree would close on.
            document.addEventListener('keydown', e => { if (e.key === 'Escape') window.__txEscapes++; });
            document.getElementById('tx-action').addEventListener('click', () => window.__txActions++);
            window.addEventListener('pointerdown', e => { window.__txPointer = e.pointerId; }, true);
            return true;
            """);
        Task<int> Leftovers() => page.EvaluateAsync<int>(
            "() => document.querySelectorAll('[data-slot=tree-ghost], [data-slot=tree-drop-indicator]').length");

        // Escape ends the drag where it is, with no move, and is not passed on: a surface around
        // the tree stays open. The release that follows is nothing any more.
        await page.Mouse.MoveAsync(60, 62);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(60, 116);
        Assert.Equal(2, await Leftovers());
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("""[["NotifyDragStart","a"],["NotifyDragEnd","a"]]""", await TakeCallsAsync(page, "__tx"));
        Assert.Equal(NoTreeDrag, await TreeDragStateAsync(page));
        Assert.Equal(0, await Leftovers());
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__txEscapes"));
        await page.Mouse.UpAsync();
        Assert.Equal("[]", await TakeCallsAsync(page, "__tx"));
        // With no drag going on Escape is everyone's again.
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__txEscapes"));

        // The browser takes the pointer away (a touch that turned into a scroll): no drop. No
        // click follows a cancelled pointer, so the next one, here made with the keyboard on a
        // control in a row, is not held back.
        await page.Mouse.MoveAsync(60, 62);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(60, 116);
        await page.EvaluateAsync("() => window.dispatchEvent(new PointerEvent('pointercancel', { pointerId: window.__txPointer }))");
        Assert.Equal("""[["NotifyDragStart","a"],["NotifyDragEnd","a"]]""", await TakeCallsAsync(page, "__tx"));
        Assert.Equal(NoTreeDrag, await TreeDragStateAsync(page));
        await page.Mouse.UpAsync();
        await page.FocusAsync("#tx-action");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__txActions"));

        // The tree is removed in the middle of a drag: the copy, the marker and the marks on the
        // page go with it, and nothing listens for the release.
        await page.Mouse.MoveAsync(60, 62);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(60, 116);
        Assert.Equal("""[["NotifyDragStart","a"]]""", await TakeCallsAsync(page, "__tx"));
        await page.EvaluateAsync("() => window.__txTree.dispose()");
        Assert.Equal(0, await Leftovers());
        Same([false, true], await page.EvaluateAsync<bool[]>(
            "() => [document.documentElement.hasAttribute('data-bz-tree-drag'), document.body.style.userSelect === '']"));
        await page.Mouse.UpAsync();
        await DragFromToAsync(page, 60, 110, 60, 62);
        Assert.Equal("[]", await TakeCallsAsync(page, "__tx"));
    }

    [E2EFact]
    public async Task Tree_drag_released_outside_the_tree_does_not_eat_a_later_click_made_from_the_keyboard()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, TreeMarkup +
            """
            const { createTree } = await load('tree');
            const host = document.createElement('div');
            // a 50..74, b 74..98 (with a button)
            host.innerHTML = tree('to', 50, 50,
                leaf('a', 0) + '<div role="treeitem" data-tree-node data-tree-row data-value="b" data-depth="0" tabindex="-1" style="height:24px">b' +
                '<button id="to-action" style="position:absolute;left:100px;top:26px;height:20px">more</button></div>');
            document.body.append(host);
            window.__to = recorder();
            createTree(document.getElementById('to'), window.__to, options('o1'));
            window.__toActions = 0;
            document.getElementById('to-action').addEventListener('click', () => window.__toActions++);
            return true;
            """);

        // Released over the page, where there is no tree to drop into. The click the browser makes
        // lands on what the press and the release have in common, which is outside the tree, so
        // the tree never sees a click to hold back.
        await page.Mouse.MoveAsync(60, 62);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(60, 90);
        await page.Mouse.MoveAsync(600, 400);
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragStart","a"],["NotifyDragEnd","a"]]""", await TakeCallsAsync(page, "__to"));

        // The first Enter used to do nothing: the tree still waited for the click of that drag and
        // took this one for it. (The same happened after a drop into another tree.)
        await page.FocusAsync("#to-action");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__toActions"));
    }

    [E2EFact]
    public async Task Tree_drag_opens_a_closed_branch_it_rests_on_and_scrolls_a_long_tree_at_its_edges()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, TreeMarkup +
            """
            const { createTree } = await load('tree');
            const host = document.createElement('div');
            // a 50..74, shut 74..98 (a closed branch), open 98..122 (an open one: kid 122..146), z 146..170;
            // beside it forty rows in a box that shows eight and scrolls.
            host.innerHTML =
                tree('te', 50, 50, leaf('a', 0) + branch('shut', 0, false, '') + branch('open', 0, true, leaf('kid', 1)) + leaf('z', 0)) +
                '<div id="ts-box" style="position:fixed;z-index:99999;left:320px;top:50px;width:240px;height:200px;overflow:auto;background:white">' +
                '<div id="ts" data-slot="tree" role="tree" style="font:12px/24px sans-serif">' +
                Array.from({ length: 40 }, (_, i) => leaf('s' + i, 0)).join('') + '</div></div>';
            document.body.append(host);
            window.__te = recorder(); window.__ts = recorder();
            createTree(document.getElementById('te'), window.__te, options('e1', { hoverExpandMs: 400 }));
            createTree(document.getElementById('ts'), window.__ts, options('s1'));
            return true;
            """);

        // Over the middle of a closed branch and off again before the time is up: nothing.
        await page.Mouse.MoveAsync(100, 62);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(100, 86);
        await page.Mouse.MoveAsync(100, 158);
        await page.WaitForTimeoutAsync(600);
        Assert.Equal("""[["NotifyDragStart","a"]]""", await TakeCallsAsync(page, "__te"));
        // An open branch has nothing to open, and the edge of a closed one is a place between rows.
        await page.Mouse.MoveAsync(100, 110);
        await page.WaitForTimeoutAsync(600);
        await page.Mouse.MoveAsync(100, 76);
        await page.WaitForTimeoutAsync(600);
        Assert.Equal("[]", await TakeCallsAsync(page, "__te"));
        // Resting on its middle asks the component to open it: once, however long the pointer
        // stays and however it moves within the row.
        await page.Mouse.MoveAsync(100, 86);
        await page.Mouse.MoveAsync(102, 87);
        await page.WaitForFunctionAsync("() => window.__te.calls.length === 1", null, new() { Timeout = 5_000 });
        await page.Mouse.MoveAsync(104, 88);
        await page.WaitForTimeoutAsync(600);
        Assert.Equal("""[["NotifyHoverExpand","shut"]]""", await TakeCallsAsync(page, "__te"));
        // Released while the wait on a branch is running: the drop is reported, and no request to
        // open comes after it.
        await page.Mouse.MoveAsync(100, 158);
        await page.Mouse.MoveAsync(100, 86);
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(600);
        Assert.Equal("""[["NotifyDragEnd","a"],["NotifyMove","a","shut","inside","e1","e1"]]""", await TakeCallsAsync(page, "__te"));

        // The long tree: in the middle of its box nothing scrolls.
        Task<double> Scrolled() => page.EvaluateAsync<double>("() => document.getElementById('ts-box').scrollTop");
        await page.Mouse.MoveAsync(400, 62);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(400, 150);
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(0, await Scrolled());
        // Held near the bottom edge the box scrolls on its own, and the marker keeps up with the
        // rows passing under the pointer.
        await page.Mouse.MoveAsync(400, 240);
        await page.WaitForFunctionAsync("() => document.getElementById('ts-box').scrollTop > 100", null, new() { Timeout = 5_000 });
        Assert.NotEqual("null", await TreeMarkerAsync(page));
        // Near the top edge it scrolls back, all the way.
        await page.Mouse.MoveAsync(400, 60);
        await page.WaitForFunctionAsync("() => document.getElementById('ts-box').scrollTop === 0", null, new() { Timeout = 5_000 });
        // The release stops it, and the drop is on the row that scrolled under the pointer.
        await page.Mouse.MoveAsync(400, 240);
        await page.WaitForFunctionAsync("() => document.getElementById('ts-box').scrollTop > 50", null, new() { Timeout = 5_000 });
        await page.Mouse.UpAsync();
        var at = await Scrolled();
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(at, await Scrolled());
        var drop = JsonDocument.Parse(await TakeCallsAsync(page, "__ts")).RootElement;
        Assert.Equal("NotifyMove", drop[2][0].GetString());
        // Row 0 was dragged; the pointer is 190 px into a box scrolled by `at`, on a row 24 px tall.
        Assert.Equal("s" + (int)((at + 190) / 24), drop[2][2].GetString());
    }

    [E2EFact]
    public async Task Tree_drag_between_trees_asks_both_before_it_tells_either()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, TreeMarkup +
            """
            const { createTree } = await load('tree');
            const host = document.createElement('div');
            // from: a 50..74, folder 74..98 (open: inner 98..122 (open: deep 122..146), shut 146..170 (closed))
            host.innerHTML =
                tree('tf', 50, 50, leaf('a', 0) + branch('folder', 0, true, branch('inner', 1, true, leaf('deep', 2)) + branch('shut', 1, false, ''))) +
                tree('tt', 320, 50, leaf('x', 0) + leaf('y', 0)) +
                tree('tempty', 590, 50, '') +
                tree('tkeeps', 860, 50, leaf('k', 0)) +
                tree('tother', 50, 300, leaf('o', 0)) +
                tree('toff', 320, 300, leaf('f', 0));
            document.body.append(host);
            // A stand-in that says yes or no when it is asked whether a drop may happen.
            const answering = () => {
                const ref = recorder();
                const record = ref.invokeMethodAsync;
                ref.answer = true;
                ref.invokeMethodAsync = (name, ...args) => record(name, ...args).then(() => name === 'CanAcceptDrop' ? ref.answer : undefined);
                return ref;
            };
            const trees = [
                ['tf', 'from', { group: 'g' }], ['tt', 'to', { group: 'g' }], ['tempty', 'empty', { group: 'g' }],
                // Takes nothing in and lets nothing out.
                ['tkeeps', 'keeps', { group: 'g', transferIn: false, transferOut: false }],
                ['tother', 'other', { group: 'h' }], ['toff', 'off', { group: 'g', disabled: true }]];
            for (const [name, id, more] of trees) {
                window['__' + name] = answering();
                window['__' + name + 'Tree'] = createTree(document.getElementById(name), window['__' + name], options(id, more));
            }
            return true;
            """);

        // An open folder with an open folder inside, onto the first row of the other tree. The
        // marker is drawn there, both trees are asked, then the source is told to let go and the
        // receiver to take it, along with which folders of what arrives were open: that is kept
        // per tree, so it would arrive closed otherwise.
        await page.Mouse.MoveAsync(100, 86);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(380, 68);
        Assert.Equal("""["line",320,72.5,240,3]""", await TreeMarkerAsync(page));
        await page.Mouse.UpAsync();
        Assert.Equal(
            """[["NotifyDragStart","folder"],["NotifyDragEnd","folder"],["CanAcceptDrop","folder","x","after","from","to"],["NotifyMove","folder","x","after","from","to"]]""",
            await TakeCallsAsync(page, "__tf"));
        Assert.Equal(
            """[["CanAcceptDrop","folder","x","after","from","to"],["NotifyReceiveMove","folder","x","after","from","to",["folder","inner"]]]""",
            await TakeCallsAsync(page, "__tt"));

        // A tree with no rows takes the node as its first: no row to name, the whole tree framed.
        await page.Mouse.MoveAsync(100, 62);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(650, 70);
        Assert.Equal("""["inside",590,50,240,40]""", await TreeMarkerAsync(page));
        await page.Mouse.UpAsync();
        Assert.Equal(
            """[["NotifyDragStart","a"],["NotifyDragEnd","a"],["CanAcceptDrop","a",null,"inside","from","empty"],["NotifyMove","a",null,"inside","from","empty"]]""",
            await TakeCallsAsync(page, "__tf"));
        Assert.Equal(
            """[["CanAcceptDrop","a",null,"inside","from","empty"],["NotifyReceiveMove","a",null,"inside","from","empty",[]]]""",
            await TakeCallsAsync(page, "__tempty"));

        // No marker and no drop over a tree that takes nothing in, one of another group, or a
        // disabled one: the drag ends as if released over the page.
        foreach (var (name, x, y) in new[] { ("__tkeeps", 920f, 62f), ("__tother", 100f, 312f), ("__toff", 380f, 312f) })
        {
            await page.Mouse.MoveAsync(100, 62);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(x, y);
            Assert.Equal("null", await TreeMarkerAsync(page));
            await page.Mouse.UpAsync();
            Assert.Equal("""[["NotifyDragStart","a"],["NotifyDragEnd","a"]]""", await TakeCallsAsync(page, "__tf"));
            Assert.Equal("[]", await TakeCallsAsync(page, name));
        }
        // Nor does a node leave a tree that lets nothing out.
        await DragFromToAsync(page, 920, 62, 380, 68);
        Assert.Equal("""[["NotifyDragStart","k"],["NotifyDragEnd","k"]]""", await TakeCallsAsync(page, "__tkeeps"));
        Assert.Equal("[]", await TakeCallsAsync(page, "__tt"));

        // Either side may refuse. Then neither is told: a node let go by one tree and not taken by
        // the other would be lost.
        await page.EvaluateAsync("() => { window.__tt.answer = false; }");
        await DragFromToAsync(page, 100, 62, 380, 68);
        Assert.Equal("""[["NotifyDragStart","a"],["NotifyDragEnd","a"],["CanAcceptDrop","a","x","after","from","to"]]""", await TakeCallsAsync(page, "__tf"));
        Assert.Equal("""[["CanAcceptDrop","a","x","after","from","to"]]""", await TakeCallsAsync(page, "__tt"));
        await page.EvaluateAsync("() => { window.__tt.answer = true; window.__tf.answer = false; }");
        await DragFromToAsync(page, 100, 62, 380, 68);
        Assert.Equal("""[["NotifyDragStart","a"],["NotifyDragEnd","a"],["CanAcceptDrop","a","x","after","from","to"]]""", await TakeCallsAsync(page, "__tf"));
        Assert.Equal("""[["CanAcceptDrop","a","x","after","from","to"]]""", await TakeCallsAsync(page, "__tt"));

        // A tree that is gone is no place to drop any more, even while its rows are still there.
        await page.EvaluateAsync("() => { window.__tf.answer = true; window.__ttTree.dispose(); }");
        await page.Mouse.MoveAsync(100, 62);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(380, 68);
        Assert.Equal("null", await TreeMarkerAsync(page));
        await page.Mouse.UpAsync();
        Assert.Equal("""[["NotifyDragStart","a"],["NotifyDragEnd","a"]]""", await TakeCallsAsync(page, "__tf"));
        Assert.Equal("[]", await TakeCallsAsync(page, "__tt"));
    }

    [E2EFact]
    public async Task Tree_scroll_watcher_reports_where_the_box_is_scrolled_to_once_a_frame()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page, TreeMarkup +
            """
            const { observeScroll } = await load('tree');
            const host = document.createElement('div');
            // Thirty rows in a box that shows five; and a box with no rows yet.
            host.innerHTML =
                '<div id="tv" data-slot="tree" style="position:fixed;z-index:99999;left:50px;top:50px;width:240px;height:120px;overflow:auto;background:white;font:12px/24px sans-serif;transition:none">' +
                Array.from({ length: 30 }, (_, i) => leaf('v' + i, 0)).join('') + '</div>' +
                '<div id="tv-empty" style="position:fixed;z-index:99999;left:320px;top:50px;width:240px;height:80px;overflow:auto;background:white"></div>';
            document.body.append(host);
            const box = host.querySelector('#tv');
            const ref = recorder(), emptyRef = recorder();
            const take = () => ref.calls.splice(0);
            const watcher = observeScroll(box, ref);
            const result = { start: take() };
            const none = observeScroll(host.querySelector('#tv-empty'), emptyRef);
            result.startEmpty = emptyRef.calls.splice(0);
            await sleep(200);
            take(); // the report a resize observer makes when it starts watching

            // Three scrolls before the next frame: one report, of where it ended up.
            box.scrollTop = 10; box.scrollTop = 20; box.scrollTop = 48;
            await sleep(250);
            result.scrolled = take();

            box.style.height = '168px';
            await sleep(250);
            result.taller = take();

            // A tab in the background gets no frames: the report still comes.
            const frames = window.requestAnimationFrame;
            window.requestAnimationFrame = () => 0;
            box.scrollTop = 96;
            await sleep(250);
            window.requestAnimationFrame = frames;
            result.withoutFrames = take();

            watcher.dispose();
            none.dispose();
            box.scrollTop = 0;
            box.style.height = '100px';
            await sleep(250);
            result.afterDispose = take();
            host.remove();
            return result;
            """);

        // At once: where it is and how much shows, and what one row measures, so the component
        // can check the height it sizes its spacers with.
        Assert.Equal("""[["OnVirtualScroll",0,120],["OnVirtualMeasure",24]]""", r.GetProperty("start").GetRawText());
        // With no row there is nothing to measure.
        Assert.Equal("""[["OnVirtualScroll",0,80]]""", r.GetProperty("startEmpty").GetRawText());
        Assert.Equal("""[["OnVirtualScroll",48,120]]""", r.GetProperty("scrolled").GetRawText());
        Assert.Equal("""[["OnVirtualScroll",48,168]]""", r.GetProperty("taller").GetRawText());
        Assert.Equal("""[["OnVirtualScroll",96,168]]""", r.GetProperty("withoutFrames").GetRawText());
        Assert.Equal("[]", r.GetProperty("afterDispose").GetRawText());
    }

    // A palette as the component renders it: a text field and, under it, a list of options, some in
    // a group, one disabled. The host is at 50,50; the field is 30 px tall and so is every option:
    // one 80..110, two 110..140, off 140..170, three 170..200; four and five are scrolled to.
    private const string CommandMarkup =
        """
        const { createCommand } = await load('command');
        window.__createCommand = createCommand;
        const item = (id, more = '') => '<div id="' + id + '" role="option" data-bz-command-item ' + more + ' style="height:30px">' + id + '</div>';
        const host = document.createElement('div');
        host.style.cssText = 'position:fixed;z-index:99999;left:50px;top:50px;width:240px;background:white;font:12px/30px sans-serif';
        host.innerHTML =
            '<input id="cm-input" style="display:block;box-sizing:border-box;width:240px;height:30px" />' +
            '<div id="cm-list" data-bz-command-list role="listbox" style="box-sizing:border-box;height:120px;overflow:auto;padding-bottom:20px">' +
            item('cm-one') + item('cm-two') + item('cm-off', 'data-disabled aria-disabled="true"') +
            '<div id="cm-group" data-bz-command-group role="presentation"><div role="group">' + item('cm-three') + item('cm-four') + '</div></div>' +
            item('cm-five') + '</div>';
        document.body.append(host);
        window.__cmPicked = [];
        host.addEventListener('click', e => { const option = e.target.closest('[data-bz-command-item]'); if (option) window.__cmPicked.push(option.id); });
        // "the highlighted options|the option the field names as active"; a highlighted option that
        // is not also marked as selected for assistive technology gets a question mark.
        window.__cmLit = (list = 'cm-list', input = 'cm-input') =>
            [...document.querySelectorAll('#' + list + ' [data-selected]')].map(o => o.id + (o.getAttribute('aria-selected') === 'true' ? '' : '?')).join(',') +
            '|' + (document.getElementById(input).getAttribute('aria-activedescendant') ?? '');
        window.__cmKeys = [];
        window.addEventListener('keydown', e => { if (!['Control', 'Alt'].includes(e.key)) window.__cmKeys.push(e.key + ':' + e.defaultPrevented); });
        """;

    [E2EFact]
    public async Task Command_keys_move_a_highlight_through_the_options_while_focus_stays_in_the_field()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, CommandMarkup +
            """
            // A second palette that wraps at its ends, one with no options, and one far down a box
            // that scrolls (as a page does under a palette that is not at its top).
            host.insertAdjacentHTML('beforeend',
                '<input id="cm-loop-input" /><div id="cm-loop-list" data-bz-command-list>' + item('cm-l1') + item('cm-l2') + '</div>' +
                '<input id="cm-none-input" /><div id="cm-none-list" data-bz-command-list></div>' +
                '<div id="cm-far-box" style="height:60px;overflow:auto"><div style="height:300px"></div>' +
                '<input id="cm-far-input" /><div id="cm-far-list" data-bz-command-list>' + item('cm-f1') + item('cm-f2') + '</div></div>');
            window.__cm = createCommand(document.getElementById('cm-list'), { inputId: 'cm-input', loop: false });
            createCommand(document.getElementById('cm-loop-list'), { inputId: 'cm-loop-input', loop: true });
            createCommand(document.getElementById('cm-none-list'), { inputId: 'cm-none-input', loop: true });
            createCommand(document.getElementById('cm-far-list'), { inputId: 'cm-far-input', loop: false });
            return true;
            """);
        Task<string> Lit(string list = "cm-list", string input = "cm-input") =>
            page.EvaluateAsync<string>("([list, input]) => window.__cmLit(list, input)", new[] { list, input });
        async Task<string[]> Pressed(string list, string input, params string[] keys)
        {
            var lit = new List<string>();
            foreach (var key in keys)
            {
                await page.Keyboard.PressAsync(key);
                lit.Add((await Lit(list, input)).Split('|')[0]);
            }
            return [.. lit];
        }

        // A palette always has a current option: the first one, from the start, and the field
        // names it. Starting does not scroll anything to get it into view: every palette on a page
        // would pull the page to itself.
        Assert.Equal("cm-one|cm-one", await Lit());
        Assert.Equal("cm-f1|cm-f1", await Lit("cm-far-list", "cm-far-input"));
        Assert.Equal(0, await page.EvaluateAsync<double>("() => document.getElementById('cm-far-box').scrollTop"));
        Assert.Equal("|", await Lit("cm-none-list", "cm-none-input"));

        // Down the list: the disabled option is passed over, the group is walked through, and the
        // last option is the end. An option out of view is scrolled to.
        await page.FocusAsync("#cm-input");
        Same(["cm-two", "cm-three", "cm-four", "cm-five", "cm-five"],
            await Pressed("cm-list", "cm-input", "ArrowDown", "ArrowDown", "ArrowDown", "ArrowDown", "ArrowDown"));
        Assert.Equal("cm-five|cm-five", await Lit());
        Assert.Equal(60, await page.EvaluateAsync<double>("() => document.getElementById('cm-list').scrollTop"));
        Same(["cm-four", "cm-one", "cm-one", "cm-five"], await Pressed("cm-list", "cm-input", "ArrowUp", "Home", "ArrowUp", "End"));
        // With a modifier the keys are the field's (Ctrl+Home is the start of its text), and so is typing.
        Same(["cm-five", "cm-five", "cm-five"], await Pressed("cm-list", "cm-input", "Control+ArrowUp", "Alt+Home", "a"));
        Same(["ArrowDown:true", "ArrowDown:true", "ArrowDown:true", "ArrowDown:true", "ArrowDown:true",
                "ArrowUp:true", "Home:true", "ArrowUp:true", "End:true", "ArrowUp:false", "Home:false", "a:false"],
            await page.EvaluateAsync<string[]>("() => window.__cmKeys.splice(0)"));
        Assert.Equal("cm-input", await ActiveIdAsync(page));

        // Enter picks the highlighted option the way a click on it does.
        await page.Keyboard.PressAsync("Enter");
        Same(["cm-five"], await page.EvaluateAsync<string[]>("() => window.__cmPicked.splice(0)"));
        Same(["Enter:true"], await page.EvaluateAsync<string[]>("() => window.__cmKeys.splice(0)"));
        // Keys that belong to a text composition in progress are the input method's.
        await page.EvaluateAsync(
            """
            () => {
                const field = document.getElementById('cm-input');
                field.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', isComposing: true, bubbles: true }));
                field.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', isComposing: true, bubbles: true }));
            }
            """);
        Assert.Equal("cm-five|cm-five", await Lit());
        Assert.Empty(await page.EvaluateAsync<string[]>("() => window.__cmPicked.splice(0)"));

        // A list that wraps: up from the first is the last, down from the last the first.
        await page.FocusAsync("#cm-loop-input");
        Same(["cm-l2", "cm-l1", "cm-l2"], await Pressed("cm-loop-list", "cm-loop-input", "ArrowUp", "ArrowDown", "ArrowDown"));
        // With no options the keys have nowhere to go; the first option that arrives is highlighted.
        await page.FocusAsync("#cm-none-input");
        Same(["", ""], await Pressed("cm-none-list", "cm-none-input", "ArrowDown", "End"));
        await page.EvaluateAsync("() => { document.getElementById('cm-none-list').innerHTML = '<div id=\"cm-late\" data-bz-command-item>late</div>'; }");
        Assert.Equal("cm-late|cm-late", await Lit("cm-none-list", "cm-none-input"));

        // Disposed: the keys are the field's alone, the highlight stays where it was.
        await page.EvaluateAsync("() => { window.__cm.dispose(); window.__cmKeys.splice(0); }");
        await page.FocusAsync("#cm-input");
        Same(["cm-five", "cm-five"], await Pressed("cm-list", "cm-input", "ArrowUp", "Home"));
        Same(["ArrowUp:false", "Home:false"], await page.EvaluateAsync<string[]>("() => window.__cmKeys.splice(0)"));
    }

    [E2EFact]
    public async Task Command_highlight_follows_the_filter_and_is_only_lent_to_the_mouse()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, CommandMarkup +
            """
            window.__cm = createCommand(document.getElementById('cm-list'), { inputId: 'cm-input', loop: false });
            // What the component does to options as the search text changes.
            window.__cmHide = (id, hidden = true) => { document.getElementById(id).hidden = hidden; };
            return true;
            """);
        Task<string> Lit() => page.EvaluateAsync<string>("() => window.__cmLit()");
        Task Hide(string id, bool hidden = true) => page.EvaluateAsync("([id, hidden]) => window.__cmHide(id, hidden)", new object[] { id, hidden });

        // The search no longer matches the highlighted option: the first one that still matches
        // takes over, so Enter always has something to pick.
        await page.FocusAsync("#cm-input");
        await page.Keyboard.PressAsync("End");
        Assert.Equal("cm-five|cm-five", await Lit());
        await Hide("cm-five");
        Assert.Equal("cm-one|cm-one", await Lit());
        // Hidden options are not stops for the keys either, and a hidden group hides its options.
        await page.Keyboard.PressAsync("End");
        Assert.Equal("cm-four|cm-four", await Lit());
        await Hide("cm-group");
        Assert.Equal("cm-one|cm-one", await Lit());
        await page.Keyboard.PressAsync("End");
        Assert.Equal("cm-two|cm-two", await Lit());
        // The highlighted option becomes disabled: the same.
        await page.EvaluateAsync("() => document.getElementById('cm-two').setAttribute('data-disabled', '')");
        Assert.Equal("cm-one|cm-one", await Lit());
        // Nothing matches: no highlight, the field names no option, and Enter picks nothing.
        await Hide("cm-one");
        Assert.Equal("|", await Lit());
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("|", await Lit());
        Assert.Empty(await page.EvaluateAsync<string[]>("() => window.__cmPicked.splice(0)"));
        // The search is cleared: back on the first option.
        await page.EvaluateAsync(
            """
            () => {
                for (const id of ['cm-one', 'cm-five', 'cm-group']) window.__cmHide(id, false);
                document.getElementById('cm-two').removeAttribute('data-disabled');
                document.getElementById('cm-list').scrollTop = 0;
            }
            """);
        Assert.Equal("cm-one|cm-one", await Lit());

        // The mouse lights the option it is over, and nothing else: no scrolling, a disabled
        // option stays dark. When it leaves the list the highlight goes back to where the keyboard
        // had it, however many options it crossed.
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("cm-two|cm-two", await Lit());
        await page.Mouse.MoveAsync(100, 95);
        Assert.Equal("cm-one|cm-one", await Lit());
        await page.Mouse.MoveAsync(100, 155);
        Assert.Equal("cm-one|cm-one", await Lit());
        await page.Mouse.MoveAsync(100, 185);
        Assert.Equal("cm-three|cm-three", await Lit());
        Assert.Equal(0, await page.EvaluateAsync<double>("() => document.getElementById('cm-list').scrollTop"));
        await page.Mouse.MoveAsync(600, 500);
        Assert.Equal("cm-two|cm-two", await Lit());

        // A key pressed while the mouse holds the highlight takes it for good: from the hovered
        // option onwards, and it stays when the mouse leaves.
        await page.Mouse.MoveAsync(100, 95);
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("cm-two|cm-two", await Lit());
        await page.Mouse.MoveAsync(600, 500);
        Assert.Equal("cm-two|cm-two", await Lit());

        // A click chooses for real: that option is where the highlight returns to from then on.
        // A click on a disabled option changes nothing.
        await page.Mouse.ClickAsync(100, 95);
        Assert.Equal("cm-one|cm-one", await Lit());
        await page.Mouse.MoveAsync(100, 185);
        await page.Mouse.MoveAsync(600, 500);
        Assert.Equal("cm-one|cm-one", await Lit());
        await page.Mouse.ClickAsync(100, 155);
        Assert.Equal("cm-one|cm-one", await Lit());
        await page.Mouse.MoveAsync(600, 500);

        // The option to return to is filtered away while the mouse is over another one: the
        // hovered one keeps the highlight.
        await page.Mouse.MoveAsync(100, 185);
        await Hide("cm-one");
        await page.Mouse.MoveAsync(600, 500);
        Assert.Equal("cm-three|cm-three", await Lit());
        await Hide("cm-one", false);

        // Disposed: the mouse lights nothing and a filter moves nothing.
        await page.EvaluateAsync("() => window.__cm.dispose()");
        await page.Mouse.MoveAsync(100, 95);
        await Hide("cm-three");
        Assert.Equal("cm-three|cm-three", await Lit());
        // Nothing to attach to (no such field, no list): a handle that does nothing.
        await page.EvaluateAsync(
            """
            () => {
                window.__createCommand(document.getElementById('cm-list'), { inputId: 'cm-no-such-field', loop: false }).dispose();
                window.__createCommand(null, { inputId: 'cm-input', loop: false }).dispose();
            }
            """);
        Assert.Equal("cm-three|cm-three", await Lit());
    }

    // A combobox popup as the component renders it, built anew by every open(): the field stays,
    // the list comes and goes. The host is at 50,50; the field is 30 px tall and so is every
    // option: one 80..110, two 110..140, off (disabled) 140..170, three 170..200, then 20 px of
    // padding. open() takes the module's options, the chosen option, the options a query hides,
    // and a height when the list is to scroll.
    private const string ComboboxMarkup =
        """
        const { createCombobox, focusInput } = await load('combobox');
        window.__createCombobox = createCombobox;
        window.__cbFocusInput = focusInput;
        const host = document.createElement('div');
        host.id = 'cb-host';
        host.style.cssText = 'position:fixed;z-index:99999;left:50px;top:50px;width:240px;background:white;font:12px/30px sans-serif';
        host.innerHTML = '<input id="cb-input" value="typed" style="display:block;box-sizing:border-box;width:240px;height:30px" /><div id="cb-popup"></div>';
        document.body.append(host);
        window.__cbPicked = [];
        host.addEventListener('click', e => { const option = e.target.closest('[data-bz-combobox-item]'); if (option) window.__cbPicked.push(option.id); });
        window.__cbOpen = (options = {}, { selected = '', hidden = [], height = '' } = {}) => {
            window.__cb?.dispose();
            const item = (id, more = '') => '<div id="' + id + '" role="option" data-bz-combobox-item aria-selected="' + (id === selected) + '" ' +
                (id === selected ? 'data-selected ' : '') + (hidden.includes(id) ? 'hidden ' : '') + more + ' style="height:30px">' + id + '</div>';
            document.getElementById('cb-popup').innerHTML =
                '<div id="cb-list" data-bz-combobox-list role="listbox" style="box-sizing:border-box;' +
                (height ? 'height:' + height + ';overflow:auto' : 'padding-bottom:20px') + '">' +
                item('cb-one') + item('cb-two') + item('cb-off', 'data-disabled aria-disabled="true"') + item('cb-three') + '</div>';
            window.__cb = createCombobox(document.getElementById('cb-list'), { inputId: 'cb-input', loop: false, autoHighlight: false, ...options });
        };
        // "the highlighted options|the option the field names as active"
        window.__cbLit = () => [...document.querySelectorAll('#cb-list [data-highlighted]')].map(o => o.id).join(',') +
            '|' + (document.getElementById('cb-input').getAttribute('aria-activedescendant') ?? '');
        window.__cbHide = (id, hidden = true) => { document.getElementById(id).hidden = hidden; };
        window.__cbKeys = [];
        window.addEventListener('keydown', e => { if (!['Control', 'Alt'].includes(e.key)) window.__cbKeys.push(e.key + ':' + e.defaultPrevented); });
        """;

    [E2EFact]
    public async Task Combobox_opens_on_the_chosen_option_and_its_keys_move_the_highlight()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, ComboboxMarkup + "window.__cbOpen(); return true;");
        Task<string> Lit() => page.EvaluateAsync<string>("() => window.__cbLit()");
        async Task<string[]> Pressed(params string[] keys)
        {
            var lit = new List<string>();
            foreach (var key in keys)
            {
                await page.Keyboard.PressAsync(key);
                lit.Add(await Lit());
            }
            return [.. lit];
        }
        Task<int> Caret() => page.EvaluateAsync<int>("() => document.getElementById('cb-input').selectionStart");

        // Nothing is chosen: the popup opens with nothing highlighted, not on its first option.
        Assert.Equal("|", await Lit());
        // From there up is the last option and down the first.
        await page.FocusAsync("#cb-input");
        await page.EvaluateAsync("() => document.getElementById('cb-input').setSelectionRange(5, 5)");
        Same(["cb-three|cb-three"], await Pressed("ArrowUp"));
        await page.EvaluateAsync("() => window.__cbOpen()");
        // Down the list past the disabled option; the ends are ends.
        Same(["cb-one|cb-one", "cb-two|cb-two", "cb-three|cb-three", "cb-three|cb-three", "cb-two|cb-two", "cb-one|cb-one", "cb-one|cb-one", "cb-three|cb-three"],
            await Pressed("ArrowDown", "ArrowDown", "ArrowDown", "ArrowDown", "ArrowUp", "Home", "ArrowUp", "End"));
        // Home and End moved the highlight, not the caret in the field. With a modifier they are
        // the field's again.
        Assert.Equal(5, await Caret());
        Same(["cb-three|cb-three", "cb-three|cb-three"], await Pressed("Control+Home", "Alt+ArrowUp"));
        Assert.Equal(0, await Caret());
        Same(["ArrowUp:true", "ArrowDown:true", "ArrowDown:true", "ArrowDown:true", "ArrowDown:true", "ArrowUp:true", "Home:true", "ArrowUp:true", "End:true",
                "Home:false", "ArrowUp:false"],
            await page.EvaluateAsync<string[]>("() => window.__cbKeys.splice(0)"));

        // An Enter that confirms a text composition is not a pick; a plain one is.
        await page.EvaluateAsync(
            "() => document.getElementById('cb-input').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', isComposing: true, bubbles: true, cancelable: true }))");
        Assert.Empty(await page.EvaluateAsync<string[]>("() => window.__cbPicked.splice(0)"));
        await page.Keyboard.PressAsync("Enter");
        Same(["cb-three"], await page.EvaluateAsync<string[]>("() => window.__cbPicked.splice(0)"));

        // The popup closes: the field, which stays, no longer names an option that is gone, and
        // its keys are its own.
        await page.EvaluateAsync("() => { window.__cb.dispose(); window.__cbKeys.splice(0); }");
        Task<bool> NamesAnOption() => page.EvaluateAsync<bool>("() => document.getElementById('cb-input').hasAttribute('aria-activedescendant')");
        Assert.False(await NamesAnOption());
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.False(await NamesAnOption());
        Same(["ArrowDown:false"], await page.EvaluateAsync<string[]>("() => window.__cbKeys.splice(0)"));

        // Something is chosen: the popup opens on it, scrolled to it when the list is long.
        await page.EvaluateAsync("() => window.__cbOpen({ loop: true }, { selected: 'cb-three', height: '60px' })");
        Assert.Equal("cb-three|cb-three", await Lit());
        Assert.Equal(60, await page.EvaluateAsync<double>("() => document.getElementById('cb-list').scrollTop"));
        // A list that wraps: down from the last is the first, up from the first the last.
        Same(["cb-one|cb-one", "cb-three|cb-three"], await Pressed("ArrowDown", "ArrowUp"));
        // A chosen option that is disabled is no place to open on.
        await page.EvaluateAsync("() => window.__cbOpen({}, { selected: 'cb-off' })");
        Assert.Equal("|", await Lit());

        // Clear and the chips hand focus back to the field with its text selected, so the next
        // key replaces it. Something that is not a text field is just focused.
        var focus = await page.EvaluateAsync<string[]>(
            """
            () => {
                document.getElementById('cb-host').insertAdjacentHTML('beforeend', '<div id="cb-div" tabindex="0">div</div>');
                const field = document.getElementById('cb-input');
                field.blur();
                window.__cbFocusInput('cb-input');
                const first = document.activeElement.id + ' ' + field.selectionStart + '-' + field.selectionEnd;
                window.__cbFocusInput('cb-div');
                const second = document.activeElement.id;
                window.__cbFocusInput('cb-no-such-field');
                return [first, second, document.activeElement.id];
            }
            """);
        Same(["cb-input 0-5", "cb-div", "cb-div"], focus);

        // Nothing to attach to (no such field, no list): a handle that does nothing.
        await page.EvaluateAsync(
            """
            () => {
                window.__cbOpen({}, { selected: 'cb-two' });
                window.__createCombobox(document.getElementById('cb-list'), { inputId: 'cb-no-such-field', loop: false, autoHighlight: true }).dispose();
                window.__createCombobox(null, { inputId: 'cb-input', loop: false, autoHighlight: true }).dispose();
            }
            """);
        Assert.Equal("cb-two|cb-two", await Lit());
    }

    [E2EFact]
    public async Task Combobox_highlight_follows_the_query_and_a_hover_highlight_leaves_with_the_mouse()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, ComboboxMarkup + "window.__cbOpen(); return true;");
        Task<string> Lit() => page.EvaluateAsync<string>("() => window.__cbLit()");
        Task Hide(string id, bool hidden = true) => page.EvaluateAsync("([id, hidden]) => window.__cbHide(id, hidden)", new object[] { id, hidden });

        // The mouse lights the option it is over. Over a disabled option, over the padding of the
        // list or outside it, nothing is lit: a hover highlight does not stay behind.
        await page.Mouse.MoveAsync(100, 125);
        Assert.Equal("cb-two|cb-two", await Lit());
        await page.Mouse.MoveAsync(100, 155);
        Assert.Equal("|", await Lit());
        await page.Mouse.MoveAsync(100, 125);
        await page.Mouse.MoveAsync(100, 210);
        Assert.Equal("|", await Lit());
        await page.Mouse.MoveAsync(100, 95);
        Assert.Equal("cb-one|cb-one", await Lit());
        await page.Mouse.MoveAsync(600, 500);
        Assert.Equal("|", await Lit());

        // A highlight the keyboard made is not the mouse's to drop...
        await page.FocusAsync("#cb-input");
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Mouse.MoveAsync(100, 210);
        await page.Mouse.MoveAsync(600, 500);
        Assert.Equal("cb-one|cb-one", await Lit());
        // ...until the mouse has been over that very option.
        await page.Mouse.MoveAsync(100, 95);
        await page.Mouse.MoveAsync(600, 500);
        Assert.Equal("|", await Lit());

        // A query hides the highlighted option. By default nothing takes its place: Enter would
        // pick an option the user never pointed at.
        await page.Keyboard.PressAsync("End");
        Assert.Equal("cb-three|cb-three", await Lit());
        await Hide("cb-three");
        Assert.Equal("|", await Lit());

        // With auto highlight the first match takes over, every time the query narrows further.
        await page.EvaluateAsync("() => window.__cbOpen({ autoHighlight: true }, { selected: 'cb-three' })");
        Assert.Equal("cb-three|cb-three", await Lit());
        await Hide("cb-three");
        Assert.Equal("cb-one|cb-one", await Lit());
        await Hide("cb-one");
        Assert.Equal("cb-two|cb-two", await Lit());
        // The query is cleared: the highlight is on an option that still shows, so it stays.
        await Hide("cb-one", false);
        await Hide("cb-three", false);
        Assert.Equal("cb-two|cb-two", await Lit());
        // The highlighted option is removed while the whole list shows: back on the chosen one.
        await page.EvaluateAsync("() => document.getElementById('cb-two').remove()");
        Assert.Equal("cb-three|cb-three", await Lit());
        // Nothing matches: no highlight, and the keys find nothing.
        await page.EvaluateAsync("() => { for (const id of ['cb-one', 'cb-three']) window.__cbHide(id); }");
        Assert.Equal("|", await Lit());
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("|", await Lit());

        // Auto highlight is about a query. A plain open with nothing chosen still highlights
        // nothing; a popup that opens on a query already typed starts on the first match.
        await page.EvaluateAsync("() => window.__cbOpen({ autoHighlight: true })");
        Assert.Equal("|", await Lit());
        await page.EvaluateAsync("() => window.__cbOpen({ autoHighlight: true }, { hidden: ['cb-one'] })");
        Assert.Equal("cb-two|cb-two", await Lit());
    }

    [E2EFact]
    public async Task Select_scroll_buttons_show_only_where_there_is_more_and_scroll_while_the_pointer_rests_on_them()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createSelectScroll } = await load('selectScroll');
            window.__createSelectScroll = createSelectScroll;
            // As the styled listbox is: a column of a set height, the buttons taking their room
            // from the list between them. 300..440 on screen: a button is 20 px tall.
            const box = (id, left, content) =>
                '<div id="' + id + '" style="position:fixed;z-index:99999;left:' + left + 'px;top:300px;width:120px;height:140px;background:white;display:flex;flex-direction:column;transition:none">' +
                '<div data-up style="display:none;height:20px;flex:none;background:silver"></div>' +
                '<div data-view style="flex:1 1 0;min-height:0;overflow-y:auto;scrollbar-width:none"><div style="height:' + content + 'px"></div></div>' +
                '<div data-down style="display:none;height:20px;flex:none;background:silver"></div></div>';
            const host = document.createElement('div');
            host.innerHTML = box('ss', 50, 220) + box('ss-fits', 250, 100);
            document.body.append(host);
            const wire = id => {
                const el = document.getElementById(id);
                return createSelectScroll(el.querySelector('[data-view]'), el.querySelector('[data-up]'), el.querySelector('[data-down]'));
            };
            window.__ss = wire('ss');
            wire('ss-fits');
            // "is the up button shown,is the down button shown,where the list is scrolled to"
            window.__ssButtons = id => {
                const el = document.getElementById(id);
                return [el.querySelector('[data-up]').style.display !== 'none', el.querySelector('[data-down]').style.display !== 'none',
                    el.querySelector('[data-view]').scrollTop].join(',');
            };
            return true;
            """);
        Task<string> Buttons(string id = "ss") => page.EvaluateAsync<string>("id => window.__ssButtons(id)", id);
        Task Until(string buttons) => page.WaitForFunctionAsync("buttons => window.__ssButtons('ss') === buttons", buttons, new() { Timeout = 5_000 });
        Task<double> Scrolled() => page.EvaluateAsync<double>("() => document.querySelector('#ss [data-view]').scrollTop");

        // At the top there is only more below; a list that fits has no button at all.
        Assert.Equal("false,true,0", await Buttons());
        Assert.Equal("false,false,0", await Buttons("ss-fits"));
        // In the middle there is more both ways.
        await page.EvaluateAsync("() => { document.querySelector('#ss [data-view]').scrollTop = 50; }");
        await Until("true,true,50");

        // The pointer rests on the down button: the list scrolls by itself to its end, where the
        // button goes away (and gives its 20 px back to the list: 220 px of options in 120).
        await page.Mouse.MoveAsync(110, 430);
        await Until("true,false,100");
        // On the up button it scrolls back; leaving the button stops it where it is.
        await page.Mouse.MoveAsync(600, 600);
        await page.Mouse.MoveAsync(110, 310);
        await page.WaitForFunctionAsync("() => document.querySelector('#ss [data-view]').scrollTop < 80", null, new() { Timeout = 5_000 });
        await page.Mouse.MoveAsync(600, 600);
        var at = await Scrolled();
        Assert.InRange(at, 1, 80);
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(at, await Scrolled());
        Assert.Equal($"true,true,{at}", await Buttons());
        // Back on it: to the top, where it goes away in turn.
        await page.Mouse.MoveAsync(110, 310);
        await Until("false,true,0");

        // The popup gets room for every option: no buttons. Less room again: the down button is back.
        await page.Mouse.MoveAsync(600, 600);
        await page.EvaluateAsync("() => { document.getElementById('ss').style.height = '260px'; }");
        await Until("false,false,0");
        await page.EvaluateAsync("() => { document.getElementById('ss').style.height = '140px'; }");
        await Until("false,true,0");

        // Disposed while it scrolls: it stops, and the buttons are no longer kept up to date.
        await page.Mouse.MoveAsync(110, 430);
        await page.WaitForFunctionAsync("() => document.querySelector('#ss [data-view]').scrollTop > 20", null, new() { Timeout = 5_000 });
        await page.EvaluateAsync("() => window.__ss.dispose()");
        at = await Scrolled();
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(at, await Scrolled());
        await page.EvaluateAsync("() => { document.querySelector('#ss [data-view]').scrollTop = 0; }");
        await page.WaitForTimeoutAsync(100);
        Assert.Equal("true,true,0", await Buttons());

        // A part is missing (the popup closed before the script got to it): a handle that does nothing.
        Assert.Equal("function", await page.EvaluateAsync<string>(
            """
            () => {
                const el = document.getElementById('ss-fits');
                const handle = window.__createSelectScroll(el.querySelector('[data-view]'), null, el.querySelector('[data-down]'));
                handle.dispose();
                return typeof handle.dispose;
            }
            """));
    }
}
