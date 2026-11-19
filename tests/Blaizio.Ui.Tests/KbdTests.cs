using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>BzKbd: the label a key combination gets on each platform, and the hotkey it can bind.</summary>
public class KbdTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _hotkey;

    public KbdTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
        _module = JSInterop.SetupModule("./_content/blaizio.base/dist/hotkeys.js");
        _module.Mode = JSRuntimeMode.Loose;
        _hotkey = _module.SetupModule(invocation => invocation.Identifier == "createHotkey");
        _hotkey.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<BzKbd> On(string? platform, string key)
    {
        if (platform is not null) _module.Setup<string>("getPlatform").SetResult(platform);
        return Render<BzKbd>(p => p.Add(x => x.Key, key));
    }

    [Theory]
    [InlineData("mod+k", "Ctrl+K", "⌘K", "Ctrl+K")]
    [InlineData("mod+shift+p", "Ctrl+Shift+P", "⌘⇧P", "Ctrl+Shift+P")]
    [InlineData("ctrl+alt+delete", "Ctrl+Alt+Delete", "⌃⌥Delete", "Ctrl+Alt+Delete")]
    [InlineData("control+option+t", "Ctrl+Alt+T", "⌃⌥T", "Ctrl+Alt+T")]
    [InlineData("meta+l", "Win+L", "⌘L", "Super+L")]
    [InlineData("cmd+l", "Win+L", "⌘L", "Super+L")]
    [InlineData("win+l", "Win+L", "⌘L", "Super+L")]
    [InlineData("super+l", "Win+L", "⌘L", "Super+L")]
    public void A_combo_is_written_the_way_each_platform_writes_it(string key, string windows, string mac, string linux)
    {
        Assert.Equal(windows, On("windows", key).Find("kbd").TextContent);
        Assert.Equal(mac, On("mac", key).Find("kbd").TextContent);
        Assert.Equal(linux, On("linux", key).Find("kbd").TextContent);
    }

    [Theory]
    [InlineData("enter", "Enter")]
    [InlineData("return", "Enter")]
    [InlineData("esc", "Esc")]
    [InlineData("escape", "Esc")]
    [InlineData("space", "Space")]
    [InlineData("tab", "Tab")]
    [InlineData("backspace", "⌫")]
    [InlineData("up", "↑")]
    [InlineData("arrowdown", "↓")]
    [InlineData("left", "←")]
    [InlineData("ArrowRight", "→")]
    [InlineData("k", "K")]
    [InlineData("7", "7")]
    [InlineData("/", "/")]
    [InlineData("f5", "F5")]
    [InlineData("pageup", "Pageup")]
    [InlineData("HOME", "Home")]
    public void A_key_gets_its_usual_cap(string key, string cap)
    {
        Assert.Equal(cap, On("windows", key).Find("kbd").TextContent);
    }

    [Theory]
    [InlineData("Mod + K")]
    [InlineData(" mod+k ")]
    [InlineData("MOD+K")]
    [InlineData("mod++k")]
    public void Case_and_spacing_in_a_combo_do_not_matter(string key)
    {
        Assert.Equal("Ctrl+K", On("windows", key).Find("kbd").TextContent);
    }

    [Fact]
    public void Until_the_platform_is_known_the_label_uses_the_text_convention()
    {
        var platform = _module.Setup<string>("getPlatform");

        var cut = Render<BzKbd>(p => p.Add(x => x.Key, "mod+k"));
        Assert.Equal("Ctrl+K", cut.Find("kbd").TextContent);

        platform.SetResult("mac");
        cut.WaitForAssertion(() => Assert.Equal("⌘K", cut.Find("kbd").TextContent));
    }

    [Fact]
    public void The_chip_carries_the_combo_as_its_title()
    {
        var chip = On("windows", "mod+k").Find("kbd");

        Assert.Equal("kbd", chip.GetAttribute("data-slot"));
        Assert.Equal("mod+k", chip.GetAttribute("title"));
    }

    [Fact]
    public void Content_of_its_own_is_shown_as_it_is_and_the_platform_is_never_asked()
    {
        var cut = Render<BzKbd>(p => p
            .Add(x => x.Class, "text-xs")
            .AddUnmatched("id", "save-key")
            .AddChildContent("Ctrl S"));

        var chip = cut.Find("kbd");
        Assert.Equal("Ctrl S", chip.TextContent);
        Assert.False(chip.HasAttribute("title"));
        Assert.Equal("save-key", chip.Id);
        Assert.Contains("text-xs", chip.ClassList);
        Assert.Contains("bz-kbd", chip.ClassList);
        Assert.Empty(_module.Invocations["getPlatform"]);
    }

    [Fact]
    public void Content_of_its_own_wins_over_the_generated_label_and_the_key_still_names_the_chip()
    {
        _module.Setup<string>("getPlatform").SetResult("mac");
        var cut = Render<BzKbd>(p => p.Add(x => x.Key, "mod+k").AddChildContent("Search"));

        Assert.Equal("Search", cut.Find("kbd").TextContent);
        Assert.Equal("mod+k", cut.Find("kbd").GetAttribute("title"));
    }

    [Fact]
    public void A_chip_with_neither_key_nor_content_is_empty()
    {
        Assert.Equal("", Render<BzKbd>().Find("kbd").TextContent);
    }

    // ---- the hotkey ---------------------------------------------------------------------------

    [Fact]
    public void A_chip_that_only_shows_a_combo_binds_nothing()
    {
        On("windows", "mod+k");

        Assert.Empty(_module.Invocations["createHotkey"]);
    }

    [Fact]
    public async Task OnTrigger_binds_the_combo_and_runs_when_the_script_reports_it()
    {
        var triggered = 0;
        var cut = Render<BzKbd>(p => p.Add(x => x.Key, "mod+k").Add(x => x.OnTrigger, () => triggered++));

        var create = _module.Invocations["createHotkey"].Single();
        Assert.Equal("mod+k", create.Arguments[0]);
        Assert.Equal(true, create.Arguments[2]); // the browser's own action for the combo is taken

        var hotkey = cut.FindComponent<BaseHotkey>();
        await hotkey.InvokeAsync(() => hotkey.Instance.OnHotkey());

        Assert.Equal(1, triggered);
    }

    [Fact]
    public void PreventDefault_off_leaves_the_browsers_own_action_alone()
    {
        Render<BzKbd>(p => p.Add(x => x.Key, "mod+p").Add(x => x.PreventDefault, false).Add(x => x.OnTrigger, () => { }));

        Assert.Equal(false, _module.Invocations["createHotkey"].Single().Arguments[2]);
    }

    [Fact]
    public void A_handler_with_no_key_has_nothing_to_bind()
    {
        Render<BzKbd>(p => p.Add(x => x.OnTrigger, () => { }).AddChildContent("?"));

        Assert.Empty(_module.Invocations["createHotkey"]);
    }

    [Fact]
    public async Task The_binding_goes_away_with_the_chip()
    {
        Render<BzKbd>(p => p.Add(x => x.Key, "mod+k").Add(x => x.OnTrigger, () => { }));

        await DisposeComponentsAsync();

        Assert.Single(_hotkey.Invocations["dispose"]);
    }

    // ---- the group ----------------------------------------------------------------------------

    [Fact]
    public void A_group_is_a_kbd_around_its_chips()
    {
        var cut = Render<BzKbdGroup>(p => p
            .Add(x => x.Class, "gap-2")
            .AddUnmatched("aria-label", "Copy")
            .AddChildContent<BzKbd>(k => k.AddChildContent("Ctrl"))
            .AddChildContent<BzKbd>(k => k.AddChildContent("C")));

        var group = cut.Find("kbd[data-slot=kbd-group]");
        Assert.Equal("Copy", group.GetAttribute("aria-label"));
        Assert.Contains("gap-2", group.ClassList);
        Assert.Equal(["Ctrl", "C"], group.QuerySelectorAll("kbd[data-slot=kbd]").Select(chip => chip.TextContent));
    }
}
