using System.Reflection;
using Microsoft.JSInterop;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// ThemeService against a recording stand-in for theme.js: which function each member calls and
/// with what, the one import and one watcher every caller shares, the change event, a failed
/// import that is tried again, and what teardown releases. What the functions do to the page is
/// the script's and is exercised in a browser.
/// </summary>
public class ThemeServiceTests
{
    private const string ModulePath = "./_content/blaizio.base/dist/theme.js";

    private static (ThemeService Service, RecordingJsModule Module, RecordingJsRuntime Runtime) Create()
    {
        var module = new RecordingJsModule { Results = { ["watchTheme"] = 7 } };
        var runtime = new RecordingJsRuntime(module);
        return (new ThemeService(runtime), module, runtime);
    }

    private static ValueTask<string> Get(ThemeService service, string member) => member switch
    {
        "Theme" => service.GetThemeAsync(),
        "ResolvedTheme" => service.GetResolvedThemeAsync(),
        "Style" => service.GetStyleAsync(),
        "Preset" => service.GetPresetAsync(),
        "Chart" => service.GetChartAsync(),
        "Radius" => service.GetRadiusAsync(),
        "Font" => service.GetFontAsync(),
        _ => service.GetHeadingAsync(),
    };

    private static ValueTask Set(ThemeService service, string member, string value) => member switch
    {
        "Theme" => service.SetThemeAsync(value),
        "Style" => service.SetStyleAsync(value),
        "Preset" => service.SetPresetAsync(value),
        "Chart" => service.SetChartAsync(value),
        "Radius" => service.SetRadiusAsync(value),
        "Font" => service.SetFontAsync(value),
        _ => service.SetHeadingAsync(value),
    };

    [Theory]
    [InlineData("Theme", "getTheme")]
    [InlineData("ResolvedTheme", "getResolvedTheme")]
    [InlineData("Style", "getStyle")]
    [InlineData("Preset", "getPreset")]
    [InlineData("Chart", "getChart")]
    [InlineData("Radius", "getRadius")]
    [InlineData("Font", "getFont")]
    [InlineData("Heading", "getHeading")]
    public async Task A_getter_returns_what_the_script_reports(string member, string function)
    {
        var (service, module, _) = Create();
        module.Results[function] = "from-the-page";

        var value = await Get(service, member);

        Assert.Equal("from-the-page", value);
        Assert.Empty(module.ArgumentsOf(function));
    }

    [Theory]
    [InlineData("Theme", "setTheme")]
    [InlineData("Style", "setStyle")]
    [InlineData("Preset", "setPreset")]
    [InlineData("Chart", "setChart")]
    [InlineData("Radius", "setRadius")]
    [InlineData("Font", "setFont")]
    [InlineData("Heading", "setHeading")]
    public async Task A_setter_hands_the_name_to_the_script(string member, string function)
    {
        var (service, module, _) = Create();

        await Set(service, member, "chosen");

        Assert.Equal(["chosen"], module.ArgumentsOf(function));
    }

    [Theory]
    [InlineData("Style")]
    [InlineData("Preset")]
    [InlineData("Chart")]
    [InlineData("Radius")]
    [InlineData("Font")]
    [InlineData("Heading")]
    public async Task A_named_knob_refuses_an_empty_or_missing_name_before_touching_the_page(string member)
    {
        var (service, module, runtime) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() => Set(service, member, "").AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => Set(service, member, null!).AsTask());

        Assert.Empty(runtime.Imports);
        Assert.Empty(module.Calls);
    }

    [Fact]
    public async Task Any_theme_name_is_passed_on_since_the_script_normalizes_it()
    {
        var (service, module, _) = Create();

        await service.SetThemeAsync("sepia");

        Assert.Equal(["sepia"], module.ArgumentsOf("setTheme"));
    }

    [Theory]
    [InlineData("rtl", Direction.Rtl)]
    [InlineData("ltr", Direction.Ltr)]
    [InlineData("", Direction.Ltr)]
    [InlineData("auto", Direction.Ltr)]
    public async Task The_direction_is_right_to_left_only_when_the_page_says_rtl(string reported, Direction expected)
    {
        var (service, module, _) = Create();
        module.Results["getDir"] = reported;

        Assert.Equal(expected, await service.GetDirectionAsync());
    }

    [Theory]
    [InlineData(Direction.Rtl, "rtl")]
    [InlineData(Direction.Ltr, "ltr")]
    public async Task The_direction_is_set_by_its_attribute_value(Direction direction, string expected)
    {
        var (service, module, _) = Create();

        await service.SetDirectionAsync(direction);

        Assert.Equal([expected], module.ArgumentsOf("setDir"));
    }

    [Fact]
    public async Task Every_call_shares_one_import_and_one_watcher()
    {
        var (service, module, runtime) = Create();

        await service.GetThemeAsync();
        await service.SetStyleAsync("forge");
        await service.GetDirectionAsync();

        Assert.Equal([ModulePath], runtime.Imports);
        Assert.Single(module.Identifiers, i => i == "watchTheme");
        // The watcher is in place before the first answer, so no change goes unreported.
        Assert.Equal("watchTheme", module.Calls[0].Identifier);
    }

    [Fact]
    public async Task Calls_made_while_the_import_is_still_running_wait_for_the_same_one()
    {
        var module = new RecordingJsModule { Results = { ["watchTheme"] = 7, ["getTheme"] = "dark", ["getStyle"] = "ember" } };
        var import = new TaskCompletionSource<IJSObjectReference>();
        var runtime = new RecordingJsRuntime(_ => import.Task);
        var service = new ThemeService(runtime);

        var theme = service.GetThemeAsync();
        var style = service.GetStyleAsync();
        Assert.False(theme.IsCompleted);
        Assert.Single(runtime.Imports);

        import.SetResult(module);

        Assert.Equal("dark", await theme);
        Assert.Equal("ember", await style);
        Assert.Single(runtime.Imports);
        Assert.Single(module.Identifiers, i => i == "watchTheme");
    }

    [Fact]
    public async Task The_change_event_carries_the_preference_and_what_is_on_screen()
    {
        var (service, _, _) = Create();
        var seen = new List<ThemeChangedEventArgs>();
        service.ThemeChanged += seen.Add;

        await service.OnThemeChangedAsync("system", "dark");

        Assert.Equal([new ThemeChangedEventArgs("system", "dark")], seen);
    }

    [Fact]
    public async Task A_change_with_nobody_listening_is_ignored()
    {
        var (service, _, _) = Create();

        await service.OnThemeChangedAsync("light", "light"); // no subscriber: must not throw

        var seen = 0;
        service.ThemeChanged += _ => seen++;
        await service.OnThemeChangedAsync("dark", "dark");
        Assert.Equal(1, seen);
    }

    [Fact]
    public async Task The_script_is_given_a_reference_it_can_report_changes_through()
    {
        var (service, module, _) = Create();
        var seen = new List<ThemeChangedEventArgs>();
        service.ThemeChanged += seen.Add;

        await service.GetThemeAsync();

        // watchTheme(reference): the script calls OnThemeChangedAsync on it by name.
        var reference = Assert.IsType<DotNetObjectReference<ThemeService>>(Assert.Single(module.ArgumentsOf("watchTheme")));
        Assert.Same(service, reference.Value);
        var callback = typeof(ThemeService).GetMethod("OnThemeChangedAsync")!;
        Assert.NotNull(callback.GetCustomAttribute<JSInvokableAttribute>());
        await (Task)callback.Invoke(reference.Value, ["light", "light"])!;
        Assert.Equal([new ThemeChangedEventArgs("light", "light")], seen);
    }

    [Fact]
    public async Task A_failed_import_is_tried_again_by_the_next_call()
    {
        var module = new RecordingJsModule { Results = { ["watchTheme"] = 7, ["getTheme"] = "dark" } };
        var attempts = 0;
        var runtime = new RecordingJsRuntime(_ => ++attempts == 1
            ? Task.FromException<IJSObjectReference>(new JSException("Failed to fetch dynamically imported module"))
            : Task.FromResult<IJSObjectReference>(module));
        var service = new ThemeService(runtime);

        await Assert.ThrowsAsync<JSException>(() => service.GetThemeAsync().AsTask());

        Assert.Equal("dark", await service.GetThemeAsync());
        Assert.Equal(2, runtime.Imports.Count);
    }

    [Fact]
    public async Task A_cancelled_import_is_tried_again_too()
    {
        var module = new RecordingJsModule { Results = { ["watchTheme"] = 7, ["getStyle"] = "ember" } };
        var attempts = 0;
        var runtime = new RecordingJsRuntime(_ => ++attempts == 1
            ? Task.FromCanceled<IJSObjectReference>(new CancellationToken(canceled: true))
            : Task.FromResult<IJSObjectReference>(module));
        var service = new ThemeService(runtime);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetStyleAsync().AsTask());

        Assert.Equal("ember", await service.GetStyleAsync());
    }

    [Fact]
    public async Task A_watcher_that_could_not_be_set_up_is_set_up_again_and_the_old_reference_is_let_go()
    {
        var (service, module, runtime) = Create();
        module.Failures["watchTheme"] = new JSException("not ready");
        await Assert.ThrowsAsync<JSException>(() => service.GetThemeAsync().AsTask());
        var first = (DotNetObjectReference<ThemeService>)module.Calls[0].Arguments[0]!;

        module.Failures.Clear();
        module.Results["getTheme"] = "light";
        Assert.Equal("light", await service.GetThemeAsync());

        Assert.Equal(2, runtime.Imports.Count);
        Assert.Equal(2, module.Identifiers.Count(i => i == "watchTheme"));
        Assert.Throws<ObjectDisposedException>(() => first.Value);
        var second = (DotNetObjectReference<ThemeService>)module.Calls.Last(c => c.Identifier == "watchTheme").Arguments[0]!;
        Assert.Same(service, second.Value);
    }

    [Fact]
    public async Task Teardown_stops_the_watcher_and_releases_the_module_and_the_reference()
    {
        var (service, module, _) = Create();
        await service.GetThemeAsync();
        var reference = (DotNetObjectReference<ThemeService>)module.ArgumentsOf("watchTheme")[0]!;

        await service.DisposeAsync();

        Assert.Equal([7], module.ArgumentsOf("unwatchTheme"));
        Assert.True(module.Disposed);
        Assert.Throws<ObjectDisposedException>(() => reference.Value);
    }

    [Fact]
    public async Task Teardown_of_a_service_that_was_never_used_touches_nothing()
    {
        var (service, module, runtime) = Create();

        await service.DisposeAsync();

        Assert.Empty(runtime.Imports);
        Assert.Empty(module.Calls);
        Assert.False(module.Disposed);
    }

    [Fact]
    public async Task Teardown_while_the_import_is_still_running_does_not_wait_for_it()
    {
        var import = new TaskCompletionSource<IJSObjectReference>();
        var runtime = new RecordingJsRuntime(_ => import.Task);
        var service = new ThemeService(runtime);
        var pending = service.GetThemeAsync();

        var teardown = service.DisposeAsync();

        Assert.True(teardown.IsCompletedSuccessfully);
        Assert.False(pending.IsCompleted);
    }

    [Fact]
    public async Task Teardown_after_a_failed_import_does_not_throw()
    {
        var runtime = new RecordingJsRuntime(_ => Task.FromException<IJSObjectReference>(new JSException("offline")));
        var service = new ThemeService(runtime);
        await Assert.ThrowsAsync<JSException>(() => service.GetThemeAsync().AsTask());

        await service.DisposeAsync();
    }

    [Fact]
    public async Task Teardown_after_the_connection_is_gone_is_quiet_and_still_lets_the_reference_go()
    {
        var (service, module, _) = Create();
        await service.GetThemeAsync();
        var reference = (DotNetObjectReference<ThemeService>)module.ArgumentsOf("watchTheme")[0]!;
        module.Failures["unwatchTheme"] = new JSDisconnectedException("circuit gone");

        await service.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => reference.Value);
    }

    [Fact]
    public async Task With_no_watcher_registered_there_is_none_to_stop()
    {
        // The script's watcher ids start at 1: a 0 is a host that answered with nothing (a
        // stand-in runtime in a test host returns the default).
        var module = new RecordingJsModule { Results = { ["watchTheme"] = 0 } };
        var service = new ThemeService(new RecordingJsRuntime(module));
        await service.GetThemeAsync();

        await service.DisposeAsync();

        Assert.DoesNotContain("unwatchTheme", module.Identifiers);
        Assert.True(module.Disposed);
    }
}
