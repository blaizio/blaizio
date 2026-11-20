using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

// The modules that turn a pointer or a scroll position into numbers: resize handles, the panel
// edge, the scroll area's own scrollbars, the slider and the window of a virtualized list.
public sealed partial class ScriptModuleTests
{
    private static async Task<(float X, float Y)> CentreAsync(IPage page, string selector)
    {
        var box = (await page.Locator(selector).BoundingBoxAsync())!;
        return (box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    private static async Task DragAsync(IPage page, string selector, float dx, float dy, int steps = 1)
    {
        var (x, y) = await CentreAsync(page, selector);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + dx, y + dy, new() { Steps = steps });
        await page.Mouse.UpAsync();
    }

    // An array literal next to an awaited value: Assert.Equal would take the literal as a span,
    // which cannot live across the await.
    private static void Same<T>(T[] expected, T[] actual) => Assert.Equal(expected, actual);

    // Reads and empties what a recorder kept, as JSON.
    private static Task<string> TakeCallsAsync(IPage page, string recorder) =>
        page.EvaluateAsync<string>(
            "name => { const calls = window[name].calls; const text = JSON.stringify(calls); calls.length = 0; return text; }", recorder);

    [E2EFact]
    public async Task Resize_handle_reports_a_drag_as_a_share_of_its_group()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createResizeHandle } = await load('resizable');
            const group = (id, style, attributes, handle) =>
                '<div data-slot="resizable-panel-group" ' + attributes + ' style="position:fixed;z-index:99999;display:flex;background:white;' + style + '">' +
                '<div style="flex:1"></div><div id="' + id + '" tabindex="0" style="background:gray;' + handle + '"></div><div style="flex:1"></div></div>';
            const host = document.createElement('div');
            host.innerHTML =
                group('rz', 'left:100px;top:100px;width:400px;height:100px', '', 'width:10px') +
                group('rz-rtl', 'left:100px;top:250px;width:400px;height:100px', 'dir="rtl"', 'width:10px') +
                group('rz-down', 'left:600px;top:100px;width:100px;height:300px;flex-direction:column', '', 'height:10px');
            document.body.append(host);
            window.__rz = recorder(); window.__rzRtl = recorder(); window.__rzDown = recorder();
            window.__rzHandle = createResizeHandle(document.getElementById('rz'), window.__rz, true, null);
            window.__rzRtlHandle = createResizeHandle(document.getElementById('rz-rtl'), window.__rzRtl, true, 'grabbing');
            window.__rzDownHandle = createResizeHandle(document.getElementById('rz-down'), window.__rzDown, false, null);
            window.__rzKeys = [];
            window.addEventListener('keydown', e => window.__rzKeys.push(e.key + ':' + e.defaultPrevented));
            return true;
            """);

        // 40 px of a 400 px group is 10 percent; the handle is marked, and the cursor held, meanwhile.
        var (x, y) = await CentreAsync(page, "#rz");
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        Same(["pointer", "ew-resize"], await page.EvaluateAsync<string[]>(
            "() => [document.getElementById('rz').getAttribute('data-resize-handle-active'), document.body.style.cursor]"));
        await page.Mouse.MoveAsync(x + 40, y);
        await page.Mouse.UpAsync();
        Assert.Equal("""[["OnDragStart"],["OnDrag",10],["OnDragEnd"]]""", await TakeCallsAsync(page, "__rz"));
        Same(["", ""], await page.EvaluateAsync<string[]>(
            "() => [document.getElementById('rz').getAttribute('data-resize-handle-active') ?? '', document.body.style.cursor]"));

        // Right to left the same movement shrinks the leading panel; a custom drag cursor is on the
        // handle too while it lasts, and gone after.
        (x, y) = await CentreAsync(page, "#rz-rtl");
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        Same(["grabbing", "grabbing"], await page.EvaluateAsync<string[]>(
            "() => [document.body.style.cursor, document.getElementById('rz-rtl').style.cursor]"));
        await page.Mouse.MoveAsync(x + 40, y);
        await page.Mouse.UpAsync();
        Assert.Equal("""[["OnDragStart"],["OnDrag",-10],["OnDragEnd"]]""", await TakeCallsAsync(page, "__rzRtl"));
        Assert.Equal("", await page.EvaluateAsync<string>("() => document.getElementById('rz-rtl').style.cursor"));

        // A vertical group measures along its height.
        await DragAsync(page, "#rz-down", 0, 30);
        Assert.Equal("""[["OnDragStart"],["OnDrag",10],["OnDragEnd"]]""", await TakeCallsAsync(page, "__rzDown"));

        // The keys that resize must not scroll the page as well; the others are left alone.
        await page.FocusAsync("#rz");
        foreach (var key in new[] { "ArrowLeft", "ArrowUp", "Home", "a" }) await page.Keyboard.PressAsync(key);
        Same(["ArrowLeft:true", "ArrowUp:false", "Home:true", "a:false"],
            await page.EvaluateAsync<string[]>("() => window.__rzKeys.splice(0)"));

        // Disabled after it was wired: no drag, and its keys are the page's again.
        await page.EvaluateAsync("() => { document.getElementById('rz').dataset.disabled = 'true'; }");
        await DragAsync(page, "#rz", 20, 0);
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("[]", await TakeCallsAsync(page, "__rz"));
        Same(["ArrowLeft:false"], await page.EvaluateAsync<string[]>("() => window.__rzKeys.splice(0)"));

        // Removed in the middle of a drag: the cursor does not stay on the whole page.
        await page.EvaluateAsync("() => { delete document.getElementById('rz').dataset.disabled; }");
        (x, y) = await CentreAsync(page, "#rz");
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.EvaluateAsync("() => window.__rzHandle.dispose()");
        Same(["", ""], await page.EvaluateAsync<string[]>(
            "() => [document.getElementById('rz').getAttribute('data-resize-handle-active') ?? '', document.body.style.cursor]"));
        await page.Mouse.UpAsync();
    }

    [E2EFact]
    public async Task Panel_edge_resizes_within_its_limits_and_reports_only_the_settled_size()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createPanelResize } = await load('panel');
            const host = document.createElement('div');
            // The styled panel animates its size; here the size is to be read the moment it is set.
            const animated = false;
            const panel = (id, place, size, inner, handle) =>
                '<div id="' + id + '" data-state="open" style="position:fixed;z-index:99999;--panel-size:' + size + ';' + place + '">' +
                '<div data-slot="panel-inner" style="background:white;transition:' + (animated ? 'width 0.3s, height 0.3s' : 'none') + ';' + inner + '"></div>' +
                '<div id="' + id + '-edge" tabindex="0" role="separator" style="position:absolute;background:gray;' + handle + '"></div></div>';
            host.innerHTML =
                panel('pn', 'left:0;top:100px;height:300px;width:var(--panel-size)', '240px', 'width:var(--panel-size);height:100%', 'top:0;right:-4px;width:8px;height:100%') +
                panel('pn-end', 'right:0;top:100px;height:300px;width:var(--panel-size)', '240px', 'width:var(--panel-size);height:100%', 'top:0;left:-4px;width:8px;height:100%') +
                panel('pn-low', 'left:400px;bottom:0;width:300px;height:var(--panel-size)', '200px', 'height:var(--panel-size);width:100%', 'left:0;top:-4px;height:8px;width:100%');
            document.body.append(host);
            window.__pn = recorder(); window.__pnEnd = recorder(); window.__pnLow = recorder();
            // Limits come as CSS lengths of any unit: 20rem is 320 px here.
            createPanelResize(document.getElementById('pn'), document.getElementById('pn-edge'), window.__pn, 'start', '160px', '20rem', null);
            createPanelResize(document.getElementById('pn-end'), document.getElementById('pn-end-edge'), window.__pnEnd, 'end', '160px', '320px', null);
            createPanelResize(document.getElementById('pn-low'), document.getElementById('pn-low-edge'), window.__pnLow, 'bottom', '100px', '300px', null);
            return true;
            """);
        Task<string> Size(string id) =>
            page.EvaluateAsync<string>("id => document.getElementById(id).style.getPropertyValue('--panel-size')", id);
        Task<string?> Now(string id) => page.GetAttributeAsync($"#{id}-edge", "aria-valuenow");

        // The separator says where the size sits between the limits: 240 of 160..320, 200 of 100..300.
        Assert.Equal("50", await Now("pn"));
        Assert.Equal("50", await Now("pn-low"));

        // While the pointer is down the size follows it and nothing is reported.
        var (x, y) = await CentreAsync(page, "#pn-edge");
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 30, y);
        Assert.Equal("270px", await Size("pn"));
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('pn').hasAttribute('data-dragging')"));
        Assert.Equal("[]", await TakeCallsAsync(page, "__pn"));
        // Past the limit it stops at the limit; the release reports that size, once.
        await page.Mouse.MoveAsync(x + 300, y);
        await page.Mouse.UpAsync();
        Assert.Equal("320px", await Size("pn"));
        Assert.Equal("""[["OnResizeEnd",320]]""", await TakeCallsAsync(page, "__pn"));
        Assert.Equal("100", await Now("pn"));
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('pn').hasAttribute('data-dragging')"));

        // A panel on the far edge grows as the pointer moves towards the content.
        await DragAsync(page, "#pn-end-edge", -40, 0);
        Assert.Equal("""[["OnResizeEnd",280]]""", await TakeCallsAsync(page, "__pnEnd"));

        // Keys move the edge the way the arrow points: up grows a panel docked at the bottom. Each
        // press is a settled size. Home and End go to the limits; the other axis is not this edge's.
        await page.FocusAsync("#pn-low-edge");
        foreach (var key in new[] { "ArrowUp", "ArrowDown", "ArrowDown", "ArrowLeft", "End", "Home" })
            await page.Keyboard.PressAsync(key);
        Assert.Equal("""[["OnResizeEnd",216],["OnResizeEnd",200],["OnResizeEnd",184],["OnResizeEnd",300],["OnResizeEnd",100]]""",
            await TakeCallsAsync(page, "__pnLow"));
        Assert.Equal("0", await Now("pn-low"));

        // A closed panel has no edge to move.
        await page.EvaluateAsync("() => document.getElementById('pn').setAttribute('data-state', 'closed')");
        await DragAsync(page, "#pn-edge", -50, 0);
        await page.FocusAsync("#pn-edge");
        await page.Keyboard.PressAsync("Home");
        Assert.Equal("[]", await TakeCallsAsync(page, "__pn"));
        Assert.Equal("320px", await Size("pn"));
    }

    [E2EFact]
    public async Task Panel_edge_key_presses_add_up_while_the_size_is_still_animating()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createPanelResize } = await load('panel');
            const host = document.createElement('div');
            // As the styled panel is: its inner surface follows --panel-size over 0.3 s.
            host.innerHTML =
                '<div id="pk" data-state="open" style="position:fixed;z-index:99999;left:400px;bottom:0;width:300px;--panel-size:200px;height:var(--panel-size)">' +
                '<div data-slot="panel-inner" style="background:white;width:100%;height:var(--panel-size);transition:height 0.3s linear !important"></div>' +
                '<div id="pk-edge" tabindex="0" role="separator" style="position:absolute;left:0;top:-4px;height:8px;width:100%;background:gray"></div></div>';
            document.body.append(host);
            window.__pk = recorder();
            createPanelResize(document.getElementById('pk'), document.getElementById('pk-edge'), window.__pk, 'bottom', '100px', '300px', null);
            return true;
            """);

        // Up twice and down twice, as fast as a held key repeats: back where it started.
        await page.FocusAsync("#pk-edge");
        foreach (var key in new[] { "ArrowUp", "ArrowUp", "ArrowDown", "ArrowDown" }) await page.Keyboard.PressAsync(key);

        // Each press used to start from wherever the animation had got to, so the steps did not add
        // up: on the docs Panel page four presses one way and four back left a 320 px panel at 336.
        Assert.Equal("""[["OnResizeEnd",216],["OnResizeEnd",232],["OnResizeEnd",216],["OnResizeEnd",200]]""",
            await TakeCallsAsync(page, "__pk"));
    }

    [E2EFact]
    public async Task Scroll_area_sizes_moves_and_shows_its_own_scrollbars()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createScrollArea } = await load('scrollArea');
            // transition:none on the thumbs: under reduced motion the page gives every property a
            // 0.01 ms transition, so a thumb the script has just moved is drawn there a frame later,
            // and a press aimed at where it is drawn would miss it.
            const area = (id, type, left, width, height) =>
                '<div id="' + id + '" data-slot="scroll-area" data-type="' + type + '" style="position:fixed;z-index:99999;top:100px;left:' + left + 'px;width:200px;height:200px;background:white">' +
                '<div data-slot="scroll-area-viewport" style="width:200px;height:200px;overflow:scroll;scrollbar-width:none">' +
                '<div data-scroll-area-content style="width:' + width + 'px;height:' + height + 'px"></div></div>' +
                '<div data-slot="scroll-area-scrollbar" data-orientation="vertical" style="position:absolute;top:0;right:0;width:10px;height:200px"><div data-slot="scroll-area-thumb" style="width:10px;background:gray;transition:none"></div></div>' +
                '<div data-slot="scroll-area-scrollbar" data-orientation="horizontal" style="position:absolute;left:0;bottom:0;height:10px;width:200px"><div data-slot="scroll-area-thumb" style="height:10px;background:gray;transition:none"></div></div>' +
                '</div>';
            const host = document.createElement('div');
            host.innerHTML = area('sa-auto', 'auto', 50, 200, 800) + area('sa-hover', 'hover', 300, 800, 800) + area('sa-fits', 'always', 550, 200, 200);
            document.body.append(host);
            for (const id of ['sa-auto', 'sa-hover', 'sa-fits']) createScrollArea(document.getElementById(id));
            await sleep(150);
            // [state, thumb length, thumb offset] of one scrollbar
            window.__bar = (id, orientation) => {
                const bar = document.querySelector('#' + id + ' [data-orientation=' + orientation + ']');
                const thumb = bar.firstElementChild;
                const length = parseFloat(orientation === 'vertical' ? thumb.style.height : thumb.style.width);
                const offset = /translate3d\(([-\d.]+)px, ([-\d.]+)px/.exec(thumb.style.transform);
                return [bar.dataset.state, length, offset ? parseFloat(offset[orientation === 'vertical' ? 2 : 1]) : 0];
            };
            return true;
            """);
        Task<JsonElement> Bar(string id, string orientation) =>
            page.EvaluateAsync<JsonElement>("([id, orientation]) => window.__bar(id, orientation)", new[] { id, orientation });
        static string State(JsonElement bar) => bar[0].GetString()!;

        // A quarter of the content shows, so the thumb is a quarter of its track; no bar for an
        // axis that does not overflow.
        var vertical = await Bar("sa-auto", "vertical");
        Assert.Equal("visible", State(vertical));
        Assert.Equal(50, vertical[1].GetDouble());
        Assert.Equal("hidden", State(await Bar("sa-auto", "horizontal")));
        Assert.Equal("hidden", State(await Bar("sa-fits", "vertical")));

        // Half way down the content, half way down the track.
        await page.EvaluateAsync("() => { document.querySelector('#sa-auto [data-slot=scroll-area-viewport]').scrollTop = 300; }");
        await page.WaitForFunctionAsync("() => window.__bar('sa-auto', 'vertical')[2] === 75");

        // Dragging the thumb scrolls by the same share: 30 px of the 150 px it can travel, of 600.
        await DragAsync(page, "#sa-auto [data-orientation=vertical] [data-slot=scroll-area-thumb]", 0, 30, steps: 3);
        Assert.Equal(420, await page.EvaluateAsync<double>(
            "() => document.querySelector('#sa-auto [data-slot=scroll-area-viewport]').scrollTop"));

        // "hover": hidden until the pointer is over the area, or it scrolls; then hidden again.
        Assert.Equal("hidden", State(await Bar("sa-hover", "vertical")));
        await page.Mouse.MoveAsync(350, 150);
        Assert.Equal("visible", State(await Bar("sa-hover", "vertical")));
        Assert.Equal("visible", State(await Bar("sa-hover", "horizontal")));
        await page.Mouse.MoveAsync(5, 700);
        Assert.Equal("hidden", State(await Bar("sa-hover", "vertical")));
        await page.EvaluateAsync("() => { document.querySelector('#sa-hover [data-slot=scroll-area-viewport]').scrollLeft = 100; }");
        await page.WaitForFunctionAsync("() => window.__bar('sa-hover', 'horizontal')[0] === 'visible'");
        await page.WaitForFunctionAsync("() => window.__bar('sa-hover', 'horizontal')[0] === 'hidden'", null, new() { Timeout = 5_000 });

        // Content that grows after the start gets its scrollbar without anyone asking.
        await page.EvaluateAsync("() => { document.querySelector('#sa-fits [data-scroll-area-content]').style.height = '600px'; }");
        await page.WaitForFunctionAsync("() => window.__bar('sa-fits', 'vertical')[0] === 'visible'");
        Assert.Equal(66.67, (await Bar("sa-fits", "vertical"))[1].GetDouble(), 2);
    }

    [E2EFact]
    public async Task Slider_turns_presses_drags_and_keys_into_proposed_values()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createSlider } = await load('slider');
            const thumb = (index, value, style) =>
                '<span data-bz-slider-thumb data-bz-slider-index="' + index + '" role="slider" tabindex="0" aria-valuenow="' + value +
                '" style="position:absolute;background:black;' + style + '"></span>';
            const slider = (id, top, attributes, values) =>
                '<div id="' + id + '" data-min="0" data-max="100" data-step="5" ' + attributes + ' style="position:fixed;z-index:99999;left:100px;top:' + top + 'px;width:200px;height:20px;background:white">' +
                '<div data-bz-slider-track style="position:absolute;inset:0;background:silver"></div>' +
                values.map((value, index) => thumb(index, value, 'top:0;left:calc(' + value + '% - 8px);width:16px;height:20px')).join('') + '</div>';
            const host = document.createElement('div');
            host.innerHTML =
                slider('sl', 100, '', [20, 80]) + slider('sl-rtl', 150, 'dir="rtl"', [50]) +
                slider('sl-inverted', 200, 'data-inverted', [50]) + slider('sl-off', 250, 'data-disabled', [50]) +
                '<div id="sl-up" data-min="0" data-max="10" data-step="1" data-orientation="vertical" style="position:fixed;z-index:99999;left:400px;top:100px;width:20px;height:200px;background:white">' +
                '<div data-bz-slider-track style="position:absolute;inset:0;background:silver"></div>' + thumb(0, 5, 'left:0;top:92px;width:20px;height:16px') + '</div>';
            document.body.append(host);
            for (const id of ['sl', 'sl-rtl', 'sl-inverted', 'sl-off', 'sl-up']) {
                window['__' + id] = recorder();
                createSlider(document.getElementById(id), window['__' + id]);
            }
            return true;
            """);
        // The C# side clamps, snaps to the step and renders the new value; here the markup stays
        // as it is, so every key starts from the same value.
        async Task<string> Settled(string id)
        {
            await page.WaitForTimeoutAsync(50); // a key reports its value, then the commit, in two steps
            return await TakeCallsAsync(page, "__" + id);
        }

        // A press on the rail moves the nearest thumb there and gives it focus.
        await page.Mouse.ClickAsync(160, 110);
        Assert.Equal("""[["OnInput",0,30],["OnCommit"]]""", await Settled("sl"));
        Assert.Equal("0", await page.EvaluateAsync<string>("() => document.activeElement.getAttribute('data-bz-slider-index')"));
        await page.Mouse.ClickAsync(240, 110);
        Assert.Equal("""[["OnInput",1,70],["OnCommit"]]""", await Settled("sl"));

        // A press on a thumb only starts the drag; the pointer may leave the rail, the value may not
        // leave the range.
        await page.Mouse.MoveAsync(260, 110);
        await page.Mouse.DownAsync();
        Assert.Equal("[]", await Settled("sl"));
        await page.Mouse.MoveAsync(280, 110);
        await page.Mouse.MoveAsync(500, 300);
        await page.Mouse.UpAsync();
        Assert.Equal("""[["OnInput",1,90],["OnInput",1,100],["OnCommit"]]""", await Settled("sl"));

        // Keys, from a thumb at 20 with a step of 5: one step, ten steps with Shift or Page keys,
        // the ends with Home and End. Other keys and shortcuts are not the slider's.
        await page.FocusAsync("#sl [data-bz-slider-index='0']");
        foreach (var key in new[] { "ArrowRight", "ArrowLeft", "ArrowUp", "ArrowDown", "Shift+ArrowRight", "PageUp", "PageDown", "Home", "End", "a", "Control+ArrowRight" })
            await page.Keyboard.PressAsync(key);
        Assert.Equal(
            """[["OnInput",0,25],["OnCommit"],["OnInput",0,15],["OnCommit"],["OnInput",0,25],["OnCommit"],["OnInput",0,15],["OnCommit"],""" +
            """["OnInput",0,70],["OnCommit"],["OnInput",0,70],["OnCommit"],["OnInput",0,-30],["OnCommit"],["OnInput",0,0],["OnCommit"],["OnInput",0,100],["OnCommit"]]""",
            await Settled("sl"));

        // Right to left: the low end is on the right, and the arrows follow the reading direction.
        await page.Mouse.ClickAsync(160, 160);
        await page.FocusAsync("#sl-rtl [data-bz-slider-thumb]");
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("""[["OnInput",0,70],["OnCommit"],["OnInput",0,45],["OnCommit"],["OnInput",0,55],["OnCommit"]]""", await Settled("sl-rtl"));

        // Inverted does the same to a left-to-right slider.
        await page.Mouse.ClickAsync(160, 210);
        await page.FocusAsync("#sl-inverted [data-bz-slider-thumb]");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("""[["OnInput",0,70],["OnCommit"],["OnInput",0,45],["OnCommit"]]""", await Settled("sl-inverted"));

        // Vertical: the low end is at the bottom.
        await page.Mouse.ClickAsync(410, 150);
        await page.FocusAsync("#sl-up [data-bz-slider-thumb]");
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("""[["OnInput",0,7.5],["OnCommit"],["OnInput",0,6],["OnCommit"]]""", await Settled("sl-up"));

        // Disabled: neither the pointer nor the keys propose anything.
        await page.Mouse.ClickAsync(160, 260);
        await page.FocusAsync("#sl-off [data-bz-slider-thumb]");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("[]", await Settled("sl-off"));
    }

    [E2EFact]
    public async Task Virtualizer_reports_the_rows_in_view_for_rows_of_one_height()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page,
            """
            const { createVirtualizer, virtualizerFor } = await load('virtualizer');
            const host = document.createElement('div');
            // 1000 rows of 20 px under a 50 px header, in a 200 px box that scrolls.
            host.innerHTML =
                '<div id="vz-box" style="height:200px;overflow:auto;position:relative"><div style="height:50px">header</div>' +
                '<div id="vz-rows" data-bz-virtual-viewport="#vz-box" data-bz-virtual-count="1000" data-bz-virtual-row-height="20" data-bz-virtual-overscan="2" style="height:20000px"></div></div>';
            document.body.append(host);
            const rows = host.querySelector('#vz-rows'), box = host.querySelector('#vz-box');
            const ref = recorder();
            const last = () => ref.calls.at(-1).slice(1);
            const virtualizer = createVirtualizer(rows, ref);
            const result = { first: last() };

            box.scrollTop = 1050;
            await sleep(150);
            result.scrolled = last();

            virtualizer.scrollToIndex(500);
            result.jumped = last();
            result.jumpedTo = box.scrollTop;
            result.found = virtualizerFor(rows) === virtualizer;

            virtualizer.scrollToIndex(99999);
            result.pastTheEnd = last();
            result.pastTheEndTo = box.scrollTop;

            rows.dataset.bzVirtualCount = '0';
            virtualizer.refresh();
            result.empty = last();

            const before = ref.calls.length;
            virtualizer.dispose();
            box.scrollTop = 0;
            await sleep(150);
            result.afterDispose = ref.calls.length - before;
            result.forgotten = virtualizerFor(rows) === undefined;
            host.remove();
            return result;
            """);
        static string Range(JsonElement e) => e.GetRawText();

        // [start, end, top spacer, bottom spacer]; -1 leaves the spacers to the component, which
        // can work them out from the row height. The header takes 50 px of the box, so 150 px of
        // rows show: 8 rows, plus 2 kept ready past the edge.
        Assert.Equal("[0,10,-1,-1]", Range(r.GetProperty("first")));
        // 1000 px into the rows: row 50 is at the top; 2 more kept ready on each side.
        Assert.Equal("[48,62,-1,-1]", Range(r.GetProperty("scrolled")));
        // A jump puts the row at the top of the box and reports the new rows at once.
        Assert.Equal(10050, r.GetProperty("jumpedTo").GetDouble());
        Assert.Equal("[498,512,-1,-1]", Range(r.GetProperty("jumped")));
        Assert.True(r.GetProperty("found").GetBoolean());
        // Past the last row means the last row; the box cannot scroll further than its end.
        Assert.Equal(19850, r.GetProperty("pastTheEndTo").GetDouble());
        Assert.Equal("[988,1000,-1,-1]", Range(r.GetProperty("pastTheEnd")));
        Assert.Equal("[0,0,0,0]", Range(r.GetProperty("empty")));
        Assert.Equal(0, r.GetProperty("afterDispose").GetInt32());
        Assert.True(r.GetProperty("forgotten").GetBoolean());
    }

    [E2EFact]
    public async Task Virtualizer_measures_rows_of_their_own_height_and_keeps_the_sums()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page,
            """
            const { createVirtualizer } = await load('virtualizer');
            const host = document.createElement('div');
            host.innerHTML =
                '<div id="vd-box" style="height:200px;overflow:auto;position:relative">' +
                '<div id="vd-rows" data-bz-virtual-viewport="#vd-box" data-bz-virtual-count="100" data-bz-virtual-row-height="20" data-bz-virtual-overscan="0" data-bz-virtual-dynamic="true"></div></div>';
            document.body.append(host);
            const rows = host.querySelector('#vd-rows'), box = host.querySelector('#vd-box');
            const ref = recorder();
            const last = () => ref.calls.at(-1).slice(1);
            // What the component does with a report: the rows of the range between two spacers.
            // Here every other row is 60 px tall, not the 20 px that was estimated.
            const render = ([start, end, top, bottom]) => {
                rows.innerHTML = '<div style="height:' + top + 'px"></div>' +
                    Array.from({ length: end - start }, (_, k) => '<div data-bz-virtual-index="' + (start + k) +
                        '" style="height:' + ((start + k) % 2 ? 60 : 20) + 'px">row</div>').join('') +
                    '<div style="height:' + bottom + 'px"></div>';
            };
            const virtualizer = createVirtualizer(rows, ref);
            const estimated = last();
            render(estimated);
            virtualizer.grow();
            const measured = last();
            render(measured);
            virtualizer.scrollToIndex(4);
            const jumped = last();
            const jumpedTo = box.scrollTop;
            virtualizer.dispose();
            host.remove();
            return { estimated, measured, jumped, jumpedTo };
            """);

        // Before anything is measured every row is the estimate: 10 fit, 90 x 20 px wait below.
        Assert.Equal("[0,10,0,1800]", r.GetProperty("estimated").GetRawText());
        // Measured, the ten rows take 400 px, so six fill the box; what is below is the rest of
        // those ten (160 px) and the 90 still estimated.
        Assert.Equal("[0,6,0,1960]", r.GetProperty("measured").GetRawText());
        // Row 4 starts after 20 + 60 + 20 + 60 px.
        Assert.Equal(160, r.GetProperty("jumpedTo").GetDouble());
        Assert.Equal("[4,10,160,1800]", r.GetProperty("jumped").GetRawText());
    }
}
