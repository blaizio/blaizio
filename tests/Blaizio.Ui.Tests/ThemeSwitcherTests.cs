using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// The three theme controls (segmented switcher, one-button toggle, menu group) over a theme
/// service: what they show for the stored preference, what they ask the service for, and that they
/// follow a change made anywhere else.
/// </summary>
public class ThemeSwitcherTests : BunitContext
{
    private readonly FakeThemes _themes = new();

    public ThemeSwitcherTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
        Services.AddScoped<IThemeService>(_ => _themes);
    }

    // A theme service with a stored preference and an operating system that is light or dark.
    private sealed class FakeThemes : IThemeService
    {
        public string Theme = "system";
        public bool SystemDark;
        public bool Disconnected;
        public List<string> Asked { get; } = [];

        public event Action<ThemeChangedEventArgs>? ThemeChanged;

        public int Listeners => ThemeChanged?.GetInvocationList().Length ?? 0;

        private string Resolved => Theme == "system" ? (SystemDark ? "dark" : "light") : Theme;

        private T Connected<T>(T value) => Disconnected ? throw new JSDisconnectedException("gone") : value;

        public ValueTask<string> GetThemeAsync() => new(Connected(Theme));
        public ValueTask<string> GetResolvedThemeAsync() => new(Connected(Resolved));

        public ValueTask SetThemeAsync(string theme)
        {
            Connected(0);
            Asked.Add(theme);
            Theme = theme;
            ThemeChanged?.Invoke(new ThemeChangedEventArgs(Theme, Resolved));
            return ValueTask.CompletedTask;
        }

        public ValueTask<string> GetStyleAsync() => new("ember");
        public ValueTask SetStyleAsync(string style) => ValueTask.CompletedTask;
        public ValueTask<string> GetPresetAsync() => new("default");
        public ValueTask SetPresetAsync(string preset) => ValueTask.CompletedTask;
        public ValueTask<Direction> GetDirectionAsync() => new(Direction.Ltr);
        public ValueTask SetDirectionAsync(Direction direction) => ValueTask.CompletedTask;
        public ValueTask<string> GetChartAsync() => new("default");
        public ValueTask SetChartAsync(string chart) => ValueTask.CompletedTask;
        public ValueTask<string> GetRadiusAsync() => new("default");
        public ValueTask SetRadiusAsync(string radius) => ValueTask.CompletedTask;
        public ValueTask<string> GetFontAsync() => new("default");
        public ValueTask SetFontAsync(string font) => ValueTask.CompletedTask;
        public ValueTask<string> GetHeadingAsync() => new("default");
        public ValueTask SetHeadingAsync(string heading) => ValueTask.CompletedTask;
    }

    // The drawing of an icon, to tell which one a control shows.
    private string Drawing(Icon icon) => Render<BzIcon>(p => p.Add(x => x.Icon, icon)).Find("svg").InnerHtml;

    // ---- the segmented switcher ---------------------------------------------------------------

    private static string[] Checked(IRenderedComponent<IComponent> cut) =>
        [.. cut.FindAll("[role=radio]").Select(option => option.GetAttribute("aria-checked") ?? "")];

    [Fact]
    public void The_switcher_is_a_named_radio_group_of_three_named_options()
    {
        var cut = Render<BzThemeSwitcher>(p => p.Add(x => x.Class, "ms-auto").AddUnmatched("id", "theme"));

        var group = cut.Find("[data-slot=theme-switcher]");
        Assert.Equal("radiogroup", group.GetAttribute("role"));
        Assert.Equal("Theme", group.GetAttribute("aria-label"));
        Assert.Equal("theme", group.Id);
        Assert.Contains("ms-auto", group.ClassList);
        Assert.Equal(
            ["System theme", "Light theme", "Dark theme"],
            cut.FindAll("[role=radio]").Select(option => option.GetAttribute("aria-label")));
    }

    [Theory]
    [InlineData("system", new[] { "true", "false", "false" })]
    [InlineData("light", new[] { "false", "true", "false" })]
    [InlineData("dark", new[] { "false", "false", "true" })]
    public void The_switcher_marks_the_stored_preference(string stored, string[] expected)
    {
        _themes.Theme = stored;
        // Following the system stays "system", whatever the system resolves to.
        _themes.SystemDark = true;

        var cut = Render<BzThemeSwitcher>();

        Assert.Equal(expected, Checked(cut));
        Assert.Equal(
            expected.Select(state => state == "true" ? "on" : "off"),
            cut.FindAll("[role=radio]").Select(option => option.GetAttribute("data-state")));
    }

    [Fact]
    public void Picking_an_option_asks_the_service_and_moves_the_mark()
    {
        _themes.Theme = "light";
        var cut = Render<BzThemeSwitcher>();

        cut.Find("[aria-label='Dark theme']").Click();

        Assert.Equal(["dark"], _themes.Asked);
        Assert.Equal(["false", "false", "true"], Checked(cut));
    }

    [Fact]
    public void The_system_option_can_be_left_out()
    {
        var cut = Render<BzThemeSwitcher>(p => p.Add(x => x.ShowSystem, false));

        Assert.Equal(["Light theme", "Dark theme"], cut.FindAll("[role=radio]").Select(option => option.GetAttribute("aria-label")));
    }

    [Fact]
    public async Task Every_control_follows_a_change_made_somewhere_else()
    {
        _themes.Theme = "light";
        var switcher = Render<BzThemeSwitcher>();
        var toggle = Render<BzThemeToggle>();
        var sun = Drawing(BzGlyphs.Sun);
        var moon = Drawing(BzGlyphs.Moon);
        Assert.Equal(sun, toggle.Find("svg").InnerHtml);

        await switcher.InvokeAsync(() => _themes.SetThemeAsync("dark").AsTask());

        Assert.Equal(["false", "false", "true"], Checked(switcher));
        toggle.WaitForAssertion(() => Assert.Equal(moon, toggle.Find("svg").InnerHtml));
    }

    [Fact]
    public void A_control_that_is_gone_stops_listening()
    {
        var cut = Render<BzThemeSwitcher>();
        Assert.Equal(1, _themes.Listeners);

        cut.Instance.Dispose();

        Assert.Equal(0, _themes.Listeners);
    }

    [Fact]
    public void A_circuit_that_is_gone_is_not_an_error()
    {
        _themes.Disconnected = true;

        var cut = Render<BzThemeSwitcher>();
        cut.Find("[aria-label='Dark theme']").Click();

        // Nothing could be read or applied: the control keeps its starting state.
        Assert.Equal(["true", "false", "false"], Checked(cut));
        Assert.Empty(_themes.Asked);
    }

    // ---- the toggle ---------------------------------------------------------------------------

    [Theory]
    [InlineData("light", false, "dark")]
    [InlineData("dark", false, "light")]
    // From "system" the flip lands on an explicit choice: the opposite of what is on screen.
    [InlineData("system", false, "dark")]
    [InlineData("system", true, "light")]
    public void The_toggle_flips_what_is_on_screen(string stored, bool systemDark, string asked)
    {
        (_themes.Theme, _themes.SystemDark) = (stored, systemDark);
        var cut = Render<BzThemeToggle>();
        var onScreenDark = stored == "dark" || (stored == "system" && systemDark);
        Assert.Equal(Drawing(onScreenDark ? BzGlyphs.Moon : BzGlyphs.Sun), cut.Find("svg").InnerHtml);

        cut.Find("button").Click();

        Assert.Equal([asked], _themes.Asked);
        Assert.Equal(Drawing(asked == "dark" ? BzGlyphs.Moon : BzGlyphs.Sun), cut.Find("svg").InnerHtml);
    }

    [Fact]
    public void With_the_system_stop_the_toggle_cycles_through_all_three_and_shows_the_preference()
    {
        (_themes.Theme, _themes.SystemDark) = ("light", true);
        var cut = Render<BzThemeToggle>(p => p.Add(x => x.ShowSystem, true));
        Assert.Equal(Drawing(BzGlyphs.Sun), cut.Find("svg").InnerHtml);

        cut.Find("button").Click();
        Assert.Equal(Drawing(BzGlyphs.Moon), cut.Find("svg").InnerHtml);

        cut.Find("button").Click();
        // A monitor while following the system, even though the system is dark.
        Assert.Equal(Drawing(BzGlyphs.DeviceDesktop), cut.Find("svg").InnerHtml);

        cut.Find("button").Click();
        Assert.Equal(["dark", "system", "light"], _themes.Asked);
    }

    [Fact]
    public void The_toggle_is_named_for_screen_readers_and_takes_class_and_attributes()
    {
        var plain = Render<BzThemeToggle>().Find("button");
        Assert.Equal("Toggle theme", plain.TextContent.Trim());
        Assert.Contains("sr-only", plain.QuerySelector("span")!.ClassList);

        var custom = Render<BzThemeToggle>(p => p
            .Add(x => x.Label, "Switch appearance")
            .Add(x => x.Class, "rounded-full")
            .AddUnmatched("id", "appearance")).Find("button");
        Assert.Equal("Switch appearance", custom.TextContent.Trim());
        Assert.Equal("appearance", custom.Id);
        Assert.Contains("rounded-full", custom.ClassList);
        Assert.Contains("bz-theme-toggle", custom.ClassList);
    }

    // ---- the menu group -----------------------------------------------------------------------

    private IRenderedComponent<BzDropdownMenu> Menu(bool showSystem = true) =>
        Render<BzDropdownMenu>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent<BzDropdownMenuContent>(content => content
                .AddChildContent<BzThemeMenu>(menu => menu.Add(x => x.ShowSystem, showSystem))));

    [Fact]
    public void The_menu_group_lists_the_three_choices_and_checks_the_stored_one()
    {
        _themes.Theme = "dark";

        var cut = Menu();

        var items = cut.FindAll("[role=menuitemradio]");
        Assert.Equal(["Light", "Dark", "System"], items.Select(item => item.TextContent.Trim()));
        Assert.Equal(["false", "true", "false"], items.Select(item => item.GetAttribute("aria-checked")));
    }

    [Fact]
    public void Picking_a_menu_choice_asks_the_service()
    {
        _themes.Theme = "dark";
        var cut = Menu();

        cut.FindAll("[role=menuitemradio]")[0].Click();

        Assert.Equal(["light"], _themes.Asked);
    }

    [Fact]
    public void The_menu_group_can_leave_the_system_choice_out()
    {
        Assert.Equal(["Light", "Dark"], Menu(showSystem: false).FindAll("[role=menuitemradio]").Select(item => item.TextContent.Trim()));
    }
}
