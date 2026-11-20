using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

// The modules behind single fields: the one-time code, the color surface, and the guards that
// decide in the page what a key may do (the tags box, the number box, the calendar grid).
public sealed partial class ScriptModuleTests
{
    // The one-time code as the component renders it: one text field laid over the slots, as long
    // as the code (four here). "otp" takes digits only, "otp-rtl" too and reads right to left,
    // "otp-any" takes anything.
    private const string OtpMarkup =
        """
        const { createInputOtp } = await load('inputOtp');
        window.__createInputOtp = createInputOtp;
        window.__otpRecorder = recorder;
        const field = (id, top, value, more = '') =>
            '<div id="' + id + '-box" ' + more + ' style="position:fixed;z-index:99999;left:50px;top:' + top + 'px;width:160px;height:40px;background:white">' +
            '<input id="' + id + '" data-input-otp maxlength="4" value="' + value + '" style="position:absolute;inset:0;width:100%;height:100%;border:0;padding:0;font:16px monospace" /></div>';
        const host = document.createElement('div');
        host.id = 'otp-host';
        host.innerHTML = field('otp', 50, '') + field('otp-rtl', 100, '12', 'dir="rtl"') + field('otp-any', 150, '') +
            '<button id="otp-elsewhere" style="position:fixed;z-index:99999;left:300px;top:50px">elsewhere</button>';
        document.body.append(host);
        window.__otp = recorder(); window.__otpRtl = recorder(); window.__otpAny = recorder();
        window.__otpHandle = createInputOtp(document.getElementById('otp'), window.__otp, 4, '^[0-9]+$');
        createInputOtp(document.getElementById('otp-rtl'), window.__otpRtl, 4, '^[0-9]+$');
        createInputOtp(document.getElementById('otp-any'), window.__otpAny, 4, null);
        // "what the field holds|the part of it that is selected": the selected part is the slot
        // the component lights.
        window.__otpField = id => { const f = document.getElementById(id); return f.value + '|' + f.selectionStart + ',' + f.selectionEnd; };
        // What was reported since the last look: everything but the selection, then the selection
        // reported last (the browser may announce one selection more than once).
        window.__otpTook = name => {
            const calls = window[name].calls.splice(0);
            const selection = calls.filter(c => c[0] === 'OnSelectionChange').at(-1);
            return JSON.stringify(calls.filter(c => c[0] !== 'OnSelectionChange')) + (selection ? ' ' + selection.slice(1).join(',') : '');
        };
        // The browser announces a selection change a task after the key that made it.
        window.__otpSettle = () => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(() => setTimeout(resolve, 0))));
        // A paste as the browser delivers it. Says whether the field's own handling was called off.
        window.__otpPaste = (id, text) => {
            const data = new DataTransfer();
            data.setData('text/plain', text);
            const paste = new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true });
            document.getElementById(id).dispatchEvent(paste);
            return paste.defaultPrevented;
        };
        """;

    private static async Task<string> OtpFieldAsync(IPage page, string id = "otp")
    {
        await page.EvaluateAsync("() => window.__otpSettle()");
        return await page.EvaluateAsync<string>("id => window.__otpField(id)", id);
    }

    private static async Task<string> OtpTookAsync(IPage page, string recorder = "__otp")
    {
        await page.EvaluateAsync("() => window.__otpSettle()");
        return await page.EvaluateAsync<string>("name => window.__otpTook(name)", recorder);
    }

    [E2EFact]
    public async Task Input_otp_lights_one_slot_at_a_time_as_a_code_is_typed_and_walked_through()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, OtpMarkup + "return true;");
        async Task<string[]> Walked(string id, params string[] keys)
        {
            var fields = new List<string>();
            foreach (var key in keys)
            {
                await page.Keyboard.PressAsync(key);
                fields.Add((await OtpFieldAsync(page, id)).Split('|')[1]);
            }
            return [.. fields];
        }

        // The field's height is handed to the slots (the caret is sized with it), and the browser's
        // own selection colour is turned off once for all code fields on the page.
        Assert.Equal("40px", await page.EvaluateAsync<string>("() => document.getElementById('otp-box').style.getPropertyValue('--bz-otp-root-height')"));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => document.querySelectorAll('#bz-input-otp-style').length"));

        // Focus on an empty code: the first slot waits for a character.
        await page.FocusAsync("#otp");
        Assert.StartsWith("""[["OnFocus",0,0]]""", await OtpTookAsync(page));
        // Each digit is reported, and the next slot waits.
        await page.Keyboard.TypeAsync("1");
        Assert.Equal("1|1,1", await OtpFieldAsync(page));
        Assert.Equal("""[["OnChange","1"]] 1,1""", await OtpTookAsync(page));
        // A letter is not a digit: it never shows and nothing is reported.
        await page.Keyboard.TypeAsync("a");
        Assert.Equal("1|1,1", await OtpFieldAsync(page));
        Assert.StartsWith("[]", await OtpTookAsync(page));
        await page.Keyboard.TypeAsync("23");
        Assert.Equal("""[["OnChange","12"],["OnChange","123"]] 3,3""", await OtpTookAsync(page));
        // The last digit fills the code: there is no slot after it, so the last one stays lit.
        await page.Keyboard.TypeAsync("4");
        Assert.Equal("1234|3,4", await OtpFieldAsync(page));
        Assert.Equal("""[["OnChange","1234"]] 3,4""", await OtpTookAsync(page));
        // Typing on replaces the lit slot; a letter still does nothing.
        await page.Keyboard.TypeAsync("5");
        Assert.Equal("""[["OnChange","1235"]] 3,4""", await OtpTookAsync(page));
        await page.Keyboard.TypeAsync("x");
        Assert.Equal("1235|3,4", await OtpFieldAsync(page));
        Assert.StartsWith("[]", await OtpTookAsync(page));

        // The arrows walk the slots one at a time and stop at both ends: always one whole slot,
        // never a caret between two.
        Same(["2,3", "1,2", "0,1", "0,1", "1,2", "2,3", "3,4", "3,4"],
            await Walked("otp", "ArrowLeft", "ArrowLeft", "ArrowLeft", "ArrowLeft", "ArrowRight", "ArrowRight", "ArrowRight", "ArrowRight"));
        Assert.Equal("[] 3,4", await OtpTookAsync(page));
        // A slot in the middle is replaced by typing over it, and the next one is lit.
        Same(["2,3", "1,2"], await Walked("otp", "ArrowLeft", "ArrowLeft"));
        await OtpTookAsync(page);
        await page.Keyboard.TypeAsync("9");
        Assert.Equal("1935|2,3", await OtpFieldAsync(page));
        Assert.Equal("""[["OnChange","1935"]] 2,3""", await OtpTookAsync(page));

        // Backspace takes the code apart from its end, down to nothing; on nothing it does nothing.
        Same(["3,4"], await Walked("otp", "End"));
        await OtpTookAsync(page);
        await page.Keyboard.PressAsync("Backspace");
        Assert.Equal("193|3,3", await OtpFieldAsync(page));
        Assert.Equal("""[["OnChange","193"]] 3,3""", await OtpTookAsync(page));
        for (var left = 3; left > 0; left--) await page.Keyboard.PressAsync("Backspace");
        Assert.Equal("""[["OnChange","19"],["OnChange","1"],["OnChange",""]] 0,0""", await OtpTookAsync(page));
        await page.Keyboard.PressAsync("Backspace");
        Assert.StartsWith("[]", await OtpTookAsync(page));

        // Focus leaves: no slot is lit, and the selection moving elsewhere on the page is not
        // this field's to report.
        await page.FocusAsync("#otp-elsewhere");
        Assert.Equal("""[["OnBlur"]]""", await OtpTookAsync(page));
        await page.EvaluateAsync("() => document.dispatchEvent(new Event('selectionchange'))");
        Assert.Equal("[]", await OtpTookAsync(page));

        // With no pattern every character is taken.
        await page.FocusAsync("#otp-any");
        await page.Keyboard.TypeAsync("a-1");
        Assert.Equal("a-1|3,3", await OtpFieldAsync(page, "otp-any"));
        Assert.StartsWith("""[["OnFocus",0,0],["OnChange","a"],["OnChange","a-"],["OnChange","a-1"]]""", await OtpTookAsync(page, "__otpAny"));

        // Right to left the first slot is on the right. Focus on a code of two: the third slot
        // waits. The left arrow goes on towards the end of the code and the right arrow back to
        // its start, the way the slots lie, where the caret of the field itself would go the
        // other way.
        await page.FocusAsync("#otp-rtl");
        Assert.Equal("12|2,2", await OtpFieldAsync(page, "otp-rtl"));
        Assert.StartsWith("""[["OnFocus",2,2]]""", await OtpTookAsync(page, "__otpRtl"));
        Same(["2,2", "1,2", "0,1", "0,1", "1,2", "2,2", "2,2"],
            await Walked("otp-rtl", "ArrowLeft", "ArrowRight", "ArrowRight", "ArrowRight", "ArrowLeft", "ArrowLeft", "ArrowLeft"));
        Assert.Equal("[] 2,2", await OtpTookAsync(page, "__otpRtl"));
        // A full code has no slot waiting after its last.
        await page.Keyboard.TypeAsync("34");
        Assert.Equal("1234|3,4", await OtpFieldAsync(page, "otp-rtl"));
        Same(["3,4", "2,3", "1,2"], await Walked("otp-rtl", "ArrowLeft", "ArrowRight", "ArrowRight"));
        Assert.Equal("""[["OnChange","123"],["OnChange","1234"]] 1,2""", await OtpTookAsync(page, "__otpRtl"));
    }

    [E2EFact]
    public async Task Input_otp_takes_a_pasted_or_filled_code_whole_and_cuts_it_to_its_slots()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page, OtpMarkup + "return true;");
        Task<bool> Paste(string text) => page.EvaluateAsync<bool>("text => window.__otpPaste('otp', text)", text);

        // A code pasted from a message is often longer than the slots: what fits is taken, the
        // last slot is lit, and the browser inserts nothing of its own.
        await page.FocusAsync("#otp");
        await OtpTookAsync(page);
        Assert.True(await Paste("123456"));
        Assert.Equal("1234|3,4", await OtpFieldAsync(page));
        Assert.Equal("""[["OnChange","1234"]] 3,4""", await OtpTookAsync(page));

        // A paste the pattern does not allow is dropped whole.
        await page.EvaluateAsync("() => { const f = document.getElementById('otp'); f.value = ''; f.setSelectionRange(0, 0); }");
        await OtpTookAsync(page);
        Assert.True(await Paste("12 34"));
        Assert.Equal("|0,0", await OtpFieldAsync(page));
        Assert.StartsWith("[]", await OtpTookAsync(page));

        // A short paste leaves the next slot waiting; a paste goes in where the caret is, and
        // over the slot that is lit.
        Assert.True(await Paste("12"));
        Assert.Equal("12|2,2", await OtpFieldAsync(page));
        Assert.Equal("""[["OnChange","12"]] 2,2""", await OtpTookAsync(page));
        await Paste("7");
        Assert.Equal("""[["OnChange","127"]] 3,3""", await OtpTookAsync(page));
        await page.EvaluateAsync("() => document.getElementById('otp').setSelectionRange(0, 1)");
        await OtpTookAsync(page);
        await Paste("88");
        Assert.Equal("8827|3,4", await OtpFieldAsync(page));
        Assert.Equal("""[["OnChange","8827"]] 3,4""", await OtpTookAsync(page));

        // A code set in one go, the way the system offers the one from a text message: cut to the
        // slots. One that the pattern does not allow is undone.
        await page.FillAsync("#otp", "9876543");
        Assert.Equal("9876|3,4", await OtpFieldAsync(page));
        Assert.Equal("""[["OnChange","9876"]] 3,4""", await OtpTookAsync(page));
        await page.FillAsync("#otp", "abcd");
        Assert.Equal("9876|3,4", await OtpFieldAsync(page));
        Assert.StartsWith("[]", await OtpTookAsync(page));

        // The parent clears the code (a bound value set to nothing). A key that is then refused
        // must not bring the old code back into the field.
        await page.EvaluateAsync("() => { document.getElementById('otp').value = ''; }");
        await page.Keyboard.TypeAsync("z");
        Assert.Equal("|0,0", await OtpFieldAsync(page));
        Assert.StartsWith("[]", await OtpTookAsync(page));

        // Made for a field that already has focus (autofocus beat the script): the slot is lit at once.
        Assert.Equal("""[["OnFocus",2,2]]""", await page.EvaluateAsync<string>(
            """
            () => {
                document.getElementById('otp-host').insertAdjacentHTML('beforeend',
                    '<div style="position:fixed;z-index:99999;left:50px;top:200px;width:160px;height:40px"><input id="otp-late" maxlength="4" value="12" /></div>');
                const late = document.getElementById('otp-late');
                late.focus();
                const ref = window.__otpRecorder();
                const handle = window.__createInputOtp(late, ref, 4, null);
                const calls = JSON.stringify(ref.calls);
                handle.dispose();
                return calls;
            }
            """));

        // Disposed: an ordinary field again. Nothing is filtered, a paste is the browser's, and
        // nothing is reported.
        await OtpTookAsync(page);
        await page.EvaluateAsync("() => window.__otpHandle.dispose()");
        await page.FocusAsync("#otp-elsewhere");
        await page.FocusAsync("#otp");
        await page.Keyboard.TypeAsync("5a");
        Assert.False(await Paste("11"));
        Assert.StartsWith("5a|", await OtpFieldAsync(page));
        Assert.Equal("[]", await OtpTookAsync(page));
    }

    [E2EFact]
    public async Task Color_area_turns_presses_drags_and_keys_into_a_proposed_pair()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createColorArea } = await load('colorArea');
            // A surface of 200 by 100 px at 100,100, its thumb in the middle; a disabled one under it.
            const area = (id, top, more = '') =>
                '<div id="' + id + '" data-bz-color-area ' + more + ' style="position:fixed;z-index:99999;left:100px;top:' + top + 'px;width:200px;height:100px;background:silver;touch-action:none">' +
                '<span data-bz-color-area-thumb role="slider" tabindex="0" data-x="0.5" data-y="0.5" style="position:absolute;left:96px;top:46px;width:8px;height:8px;background:black"></span></div>';
            const host = document.createElement('div');
            host.innerHTML = area('ca', 100) + area('ca-off', 250, 'data-disabled');
            document.body.append(host);
            window.__ca = recorder(); window.__caOff = recorder();
            window.__caHandle = createColorArea(document.getElementById('ca'), window.__ca);
            createColorArea(document.getElementById('ca-off'), window.__caOff);
            window.__caKeys = [];
            window.addEventListener('keydown', e => {
                if (!['Shift', 'Control'].includes(e.key)) window.__caKeys.push((e.ctrlKey ? 'Ctrl+' : '') + (e.shiftKey ? 'Shift+' : '') + e.key + ':' + e.defaultPrevented);
            });
            return true;
            """);
        Task<string[]> Keys() => page.EvaluateAsync<string[]>("() => window.__caKeys.splice(0)");

        // A press proposes the point under it, x from the left and y from the bottom (so that up
        // is brighter), and gives the thumb focus so the keys carry on from there.
        await page.Mouse.MoveAsync(150, 125);
        await page.Mouse.DownAsync();
        Assert.Equal("""[["OnInput",0.25,0.75]]""", await TakeCallsAsync(page, "__ca"));
        Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement.matches('#ca [data-bz-color-area-thumb]')"));
        // The drag goes on outside the surface, where the pair stops at the edges. Nothing is
        // settled until the release.
        await page.Mouse.MoveAsync(260, 180);
        await page.Mouse.MoveAsync(500, 20);
        await page.Mouse.MoveAsync(20, 400);
        Assert.Equal("""[["OnInput",0.8,0.2],["OnInput",1,1],["OnInput",0,0]]""", await TakeCallsAsync(page, "__ca"));
        await page.Mouse.UpAsync();
        Assert.Equal("""[["OnCommit"]]""", await TakeCallsAsync(page, "__ca"));
        // After it the pointer is only passing by, and another mouse button is not a press.
        await page.Mouse.MoveAsync(150, 125);
        await page.Mouse.ClickAsync(150, 125, new() { Button = MouseButton.Right });
        Assert.Equal("[]", await TakeCallsAsync(page, "__ca"));

        // Keys, from a thumb at the middle: a hundredth a press, a tenth with Shift or the page
        // keys (up and down only), Home and End to the left and right edge. Each press is a
        // settled value. The component clamps: the script proposes past the edge.
        await page.FocusAsync("#ca [data-bz-color-area-thumb]");
        foreach (var key in new[] { "ArrowRight", "ArrowLeft", "ArrowUp", "ArrowDown", "Shift+ArrowRight", "Shift+ArrowDown", "Home", "End", "PageUp", "PageDown",
                     "a", "Control+ArrowRight", "Enter" })
            await page.Keyboard.PressAsync(key);
        Assert.Equal(
            """[["OnInput",0.51,0.5],["OnCommit"],["OnInput",0.49,0.5],["OnCommit"],["OnInput",0.5,0.51],["OnCommit"],["OnInput",0.5,0.49],["OnCommit"],""" +
            """["OnInput",0.6,0.5],["OnCommit"],["OnInput",0.5,0.4],["OnCommit"],["OnInput",-0.5,0.5],["OnCommit"],["OnInput",1.5,0.5],["OnCommit"],""" +
            """["OnInput",0.5,0.6],["OnCommit"],["OnInput",0.5,0.4],["OnCommit"]]""",
            await TakeCallsAsync(page, "__ca"));
        // Its keys do not scroll the page as well; the others, and shortcuts, are left alone.
        Same(["ArrowRight:true", "ArrowLeft:true", "ArrowUp:true", "ArrowDown:true", "Shift+ArrowRight:true", "Shift+ArrowDown:true", "Home:true", "End:true",
                "PageUp:true", "PageDown:true", "a:false", "Ctrl+ArrowRight:false", "Enter:false"],
            await Keys());

        // Disabled: neither the pointer nor the keys propose anything, and the keys are the page's.
        await page.Mouse.ClickAsync(150, 275);
        await page.FocusAsync("#ca-off [data-bz-color-area-thumb]");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("[]", await TakeCallsAsync(page, "__caOff"));
        Same(["ArrowRight:false"], await Keys());

        // Disposed in the middle of a drag: the rest of the drag, a new press and the keys do nothing.
        await page.Mouse.MoveAsync(150, 125);
        await page.Mouse.DownAsync();
        Assert.Equal("""[["OnInput",0.25,0.75]]""", await TakeCallsAsync(page, "__ca"));
        await page.EvaluateAsync("() => window.__caHandle.dispose()");
        await page.Mouse.MoveAsync(200, 150);
        await page.Mouse.UpAsync();
        await page.Mouse.ClickAsync(150, 125);
        await page.FocusAsync("#ca [data-bz-color-area-thumb]");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("[]", await TakeCallsAsync(page, "__ca"));
        Same(["ArrowRight:false"], await Keys());
    }

    [E2EFact]
    public async Task Eye_dropper_answers_with_the_picked_color_or_with_nothing()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        var r = await RunAsync<JsonElement>(page,
            """
            const { supportsEyeDropper, openEyeDropper } = await load('colorArea');
            const browsers = Object.getOwnPropertyDescriptor(window, 'EyeDropper');
            const result = {};
            // The browser's picker waits for a person: stand-ins that answer the three ways it can.
            window.EyeDropper = class { open() { return Promise.resolve({ sRGBHex: '#12ab34' }); } };
            result.supported = supportsEyeDropper();
            result.picked = await openEyeDropper();
            window.EyeDropper = class { open() { return Promise.reject(new DOMException('The user canceled the selection.', 'AbortError')); } };
            result.dismissed = await openEyeDropper();
            window.EyeDropper = undefined;
            result.without = [supportsEyeDropper(), await openEyeDropper()];
            if (browsers) Object.defineProperty(window, 'EyeDropper', browsers); else delete window.EyeDropper;
            return result;
            """);

        Assert.True(r.GetProperty("supported").GetBoolean());
        Assert.Equal("#12ab34", r.GetProperty("picked").GetString());
        // Escape closes the picker: that is an answer of "nothing", not a failure.
        Assert.Equal(JsonValueKind.Null, r.GetProperty("dismissed").ValueKind);
        // A browser without the picker says so, and asking it anyway is "nothing" too.
        Assert.Equal("[false,null]", r.GetProperty("without").GetRawText());
    }

    [E2EFact]
    public async Task Calendar_takes_the_grid_keys_from_the_page_only_on_a_day()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createCalendar } = await load('calendar');
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:100px;top:100px;background:white';
            // A calendar with its previous button, its month box and one day; and a day of another calendar.
            host.innerHTML =
                '<div id="cal"><button id="cal-prev">previous</button><select id="cal-month"><option>May</option><option>June</option></select>' +
                '<button id="cal-day" data-slot="calendar-day-button"><span id="cal-day-number" tabindex="-1">14</span></button></div>' +
                '<button id="cal-other-day" data-slot="calendar-day-button">15</button>';
            document.body.append(host);
            window.__cal = recorder();
            window.__calHandle = createCalendar(document.getElementById('cal'), window.__cal);
            window.__calKeys = [];
            window.__calPicks = 0;
            document.getElementById('cal-day').addEventListener('click', () => window.__calPicks++);
            window.addEventListener('keydown', e => {
                if (!['Shift', 'Control', 'Alt', 'Meta'].includes(e.key)) window.__calKeys.push(e.key + ':' + e.defaultPrevented);
            });
            return true;
            """);
        async Task<string[]> Pressed(string selector, params string[] keys)
        {
            await page.FocusAsync(selector);
            foreach (var key in keys) await page.Keyboard.PressAsync(key);
            return await page.EvaluateAsync<string[]>("() => window.__calKeys.splice(0)");
        }

        // On a day the keys that move through the grid are handed to the component, with Shift
        // (which turns a month into a year), and do not scroll the page. Shortcuts and other keys
        // are not the grid's. Enter and Space are the day button's own: they press it.
        Same(["ArrowLeft:true", "ArrowRight:true", "ArrowUp:true", "ArrowDown:true", "Home:true", "End:true", "PageUp:true", "PageDown:true",
                "PageUp:true", "ArrowRight:true", "ArrowLeft:false", "ArrowDown:false", "ArrowUp:false", "a:false", "Enter:false", " :false"],
            await Pressed("#cal-day", "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End", "PageUp", "PageDown",
                "Shift+PageUp", "Shift+ArrowRight", "Control+ArrowLeft", "Alt+ArrowDown", "Meta+ArrowUp", "a", "Enter", "Space"));
        Assert.Equal(
            """[["OnNavigate","ArrowLeft",false],["OnNavigate","ArrowRight",false],["OnNavigate","ArrowUp",false],["OnNavigate","ArrowDown",false],""" +
            """["OnNavigate","Home",false],["OnNavigate","End",false],["OnNavigate","PageUp",false],["OnNavigate","PageDown",false],""" +
            """["OnNavigate","PageUp",true],["OnNavigate","ArrowRight",true]]""",
            await TakeCallsAsync(page, "__cal"));
        Assert.Equal(2, await page.EvaluateAsync<int>("() => window.__calPicks"));

        // Something inside the day button counts as the day.
        Same(["ArrowDown:true"], await Pressed("#cal-day-number", "ArrowDown"));
        Assert.Equal("""[["OnNavigate","ArrowDown",false]]""", await TakeCallsAsync(page, "__cal"));

        // The other controls of the calendar keep their keys (the month box changes its month),
        // and a day of another calendar is not this one's.
        Same(["ArrowDown:false"], await Pressed("#cal-prev", "ArrowDown"));
        Same(["ArrowDown:false"], await Pressed("#cal-month", "ArrowDown"));
        Assert.Equal("June", await page.InputValueAsync("#cal-month"));
        Same(["ArrowDown:false"], await Pressed("#cal-other-day", "ArrowDown"));
        Assert.Equal("[]", await TakeCallsAsync(page, "__cal"));

        await page.EvaluateAsync("() => window.__calHandle.dispose()");
        Same(["ArrowDown:false"], await Pressed("#cal-day", "ArrowDown"));
        Assert.Equal("[]", await TakeCallsAsync(page, "__cal"));
    }

    [E2EFact(Skip = "bug: calendar.ts and colorArea.ts accept a missing root when they are created, then throw from dispose()")]
    public async Task Calendar_and_color_area_made_for_an_element_that_is_gone_can_still_be_disposed()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        // The element was removed before the script got to it: the component's reference to it
        // arrives as null. Both constructors allow for that and attach nothing; the handle they
        // return is the one the component disposes later.
        var r = await RunAsync<string[]>(page,
            """
            const { createCalendar } = await load('calendar');
            const { createColorArea } = await load('colorArea');
            return [createCalendar, createColorArea].map(create => {
                const handle = create(null, recorder());
                try { handle.dispose(); return 'disposed'; } catch (error) { return String(error); }
            });
            """);

        // Today both say "TypeError: Cannot read properties of null (reading 'removeEventListener')".
        Assert.Equal(["disposed", "disposed"], r);
    }

    [E2EFact]
    public async Task Input_tags_guard_takes_the_default_from_Enter_and_the_delimiter_keys_only()
    {
        await using var context = await fx.NewContextAsync();
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { createInputTags, focusInput, setInputText } = await load('inputTags');
            window.__tags = { createInputTags, focusInput, setInputText };
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:100px;top:100px;background:white';
            // The text box alone in a form: Enter in such a box submits the form.
            host.innerHTML = '<form id="tg-form"><input id="tg-input" data-bz-input-tags-input /></form><input id="tg-next" /><div id="tg-div" tabindex="0">div</div>';
            document.body.append(host);
            window.__tgSubmits = 0;
            document.getElementById('tg-form').addEventListener('submit', e => { e.preventDefault(); window.__tgSubmits++; });
            // The component's own key handler, which turns the text into a tag: it must still get every key.
            window.__tgKeys = [];
            document.getElementById('tg-input').addEventListener('keydown', e => window.__tgKeys.push(e.key));
            window.__tagsHandle = createInputTags(document.getElementById('tg-input'), { delimiters: [',', ' ', 'Tab'] });
            return true;
            """);
        Task<int> Submits() => page.EvaluateAsync<int>("() => window.__tgSubmits");

        // A delimiter ends a tag, so its character is not typed; any other character is. Enter
        // ends a tag as well and must not submit the form. Every key still reaches the component.
        await page.FocusAsync("#tg-input");
        await page.Keyboard.TypeAsync("a,b c;d");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.PressAsync("Shift+Enter");
        Assert.Equal("abc;d", await page.InputValueAsync("#tg-input"));
        Assert.Equal(0, await Submits());
        Assert.Equal("a,b c;dEnterShiftEnter", await page.EvaluateAsync<string>("() => window.__tgKeys.splice(0).join('')"));
        // Tab as a delimiter ends a tag instead of moving on to the next field.
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("tg-input", await ActiveIdAsync(page));

        // Keys that belong to a text composition in progress are the input method's:
        // [Enter while composing, a delimiter while composing, the same delimiter typed plainly].
        Same([false, false, true], await page.EvaluateAsync<bool[]>(
            """
            () => [['Enter', true], [',', true], [',', false]].map(([key, isComposing]) => {
                const press = new KeyboardEvent('keydown', { key, isComposing, bubbles: true, cancelable: true });
                document.getElementById('tg-input').dispatchEvent(press);
                return press.defaultPrevented;
            })
            """));

        // What the component calls after a click on the box or on a chip's remove button, and after
        // a paste was split into tags: focus back to the text box, and its text set to what is left.
        // Text that is already there is not written again (that would move the caret).
        Same(["tg-input", "tg-input", "rest@2", "", "div"], await page.EvaluateAsync<string[]>(
            """
            () => {
                const input = document.getElementById('tg-input'), seen = [];
                document.getElementById('tg-div').focus();
                window.__tags.focusInput('tg-input');
                seen.push(document.activeElement.id);
                window.__tags.focusInput('tg-no-such-box');
                seen.push(document.activeElement.id);
                input.value = 'rest';
                input.setSelectionRange(2, 2);
                window.__tags.setInputText('tg-input', 'rest');
                seen.push(input.value + '@' + input.selectionStart);
                window.__tags.setInputText('tg-input', '');
                seen.push(input.value);
                // Not a text box, or not there: left alone.
                window.__tags.setInputText('tg-div', 'x');
                window.__tags.setInputText('tg-no-such-box', 'x');
                seen.push(document.getElementById('tg-div').textContent);
                window.__tags.createInputTags(null, { delimiters: [] }).dispose();
                return seen;
            }
            """));

        // Disposed: an ordinary text box. The delimiter is typed, Enter submits, Tab moves on.
        await page.EvaluateAsync("() => window.__tagsHandle.dispose()");
        await page.Keyboard.TypeAsync("a,b");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("a,b", await page.InputValueAsync("#tg-input"));
        Assert.Equal(1, await Submits());
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("tg-next", await ActiveIdAsync(page));
    }

    [E2EFact]
    public async Task Number_field_refuses_what_could_never_become_a_number()
    {
        await using var context = await fx.NewContextAsync();
        // A real paste: the guard has to hold for text that arrives all at once too.
        await context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        var page = await OpenAsync(context);

        await RunAsync<bool>(page,
            """
            const { guardNumeric, releaseNumeric } = await load('inputNumber');
            window.__number = { guardNumeric, releaseNumeric };
            const host = document.createElement('div');
            host.style.cssText = 'position:fixed;z-index:99999;left:100px;top:100px;background:white';
            host.innerHTML = '<input id="nm" role="spinbutton" /><input id="nm-whole" /><input id="nm-comma" /><input id="nm-paste" />';
            document.body.append(host);
            guardNumeric(document.getElementById('nm'), { decimal: true, negative: true, separator: '.' });
            // A whole number that cannot go below zero.
            guardNumeric(document.getElementById('nm-whole'), { decimal: false, negative: false, separator: '.' });
            // A culture that writes one and a half as 1,5.
            guardNumeric(document.getElementById('nm-comma'), { decimal: true, negative: true, separator: ',' });
            guardNumeric(document.getElementById('nm-paste'), { decimal: true, negative: true, separator: '.' });
            return true;
            """);
        async Task<string> Typed(string selector, string text)
        {
            await page.FocusAsync(selector);
            await page.Keyboard.TypeAsync(text);
            return await page.InputValueAsync(selector);
        }
        async Task<string> Pasted(string text, bool overEverything = false)
        {
            await page.EvaluateAsync("text => navigator.clipboard.writeText(text)", text);
            if (overEverything) await page.Keyboard.PressAsync("Control+a");
            await page.Keyboard.PressAsync("Control+v");
            return await page.InputValueAsync("#nm-paste");
        }

        // Letters, a second separator, a sign in the middle and a space never land; what is left
        // is what was typed of a number.
        Assert.Equal("-12.567", await Typed("#nm", "-1a2.5.6e-7 "));
        // Where the caret is counts: in front of the sign neither a second sign nor a digit fits.
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.TypeAsync("-9");
        Assert.Equal("-12.567", await page.InputValueAsync("#nm"));
        // So does what is selected: typed over everything, a lone separator is a number in the making.
        await page.Keyboard.PressAsync("Control+a");
        await page.Keyboard.TypeAsync(".");
        Assert.Equal(".", await page.InputValueAsync("#nm"));
        await page.Keyboard.TypeAsync("5");
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.TypeAsync("-");
        Assert.Equal("-.5", await page.InputValueAsync("#nm"));
        // Deleting is always allowed.
        await page.Keyboard.PressAsync("Backspace");
        await page.Keyboard.PressAsync("Delete");
        Assert.Equal("5", await page.InputValueAsync("#nm"));

        // Whole and not below zero: digits only.
        Assert.Equal("1234", await Typed("#nm-whole", "-12.3,4x"));
        // The culture's separator is taken, once. So is the point (number pads type it whatever
        // the culture), but not both.
        Assert.Equal("1,523", await Typed("#nm-comma", "1,5,2.3"));
        await page.FillAsync("#nm-comma", "");
        Assert.Equal("-.5", await Typed("#nm-comma", "-.5,"));

        // Pasted text is judged as a whole, together with what it joins or replaces.
        await page.FocusAsync("#nm-paste");
        Assert.Equal("", await Pasted("12abc"));
        Assert.Equal("12.5", await Pasted("12.5"));
        Assert.Equal("12.5", await Pasted(".5"));
        Assert.Equal("-3", await Pasted("-3", overEverything: true));
        // Grouped digits are not something the field can grow into a number.
        Assert.Equal("-3", await Pasted("1,234.5", overEverything: true));

        // What counts as a number changed (a minimum below zero, a fractional step): the new guard
        // takes the place of the old one. Were both in force the sign and the point would still be refused.
        await page.EvaluateAsync("() => window.__number.guardNumeric(document.getElementById('nm-whole'), { decimal: true, negative: true, separator: '.' })");
        await page.FillAsync("#nm-whole", "");
        Assert.Equal("-1.5", await Typed("#nm-whole", "-1.5x"));

        // Released: an ordinary text box. Releasing twice, or a field that is gone, is fine.
        await page.EvaluateAsync(
            """
            () => {
                const field = document.getElementById('nm-whole');
                window.__number.releaseNumeric(field);
                window.__number.releaseNumeric(field);
                window.__number.releaseNumeric(null);
                window.__number.guardNumeric(null, { decimal: true, negative: true, separator: '.' });
            }
            """);
        Assert.Equal("-1.5x", await Typed("#nm-whole", "x"));
    }
}
