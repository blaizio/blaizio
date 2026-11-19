using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The browser-side services (ICore) against a recording stand-in for core.js: what reaches the
/// script and what comes back. The retrying focus and the delegated guards themselves are the
/// script's and are exercised in a browser.
/// </summary>
public class CoreTests
{
    private const string ModulePath = "./_content/blaizio.base/dist/core.js";

    private static (Core Core, RecordingJsModule Module, RecordingJsRuntime Runtime) Create()
    {
        var module = new RecordingJsModule();
        var runtime = new RecordingJsRuntime(module);
        return (new Core(runtime), module, runtime);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Focus_by_id_passes_the_id_and_returns_whether_focus_landed(bool landed)
    {
        var (core, module, _) = Create();
        module.Results["focusElement"] = landed;

        var focused = await core.FocusAsync("email");

        Assert.Equal(landed, focused);
        // No options given: the script applies its own defaults.
        Assert.Equal(["email", null], module.ArgumentsOf("focusElement"));
    }

    [Fact]
    public async Task Focus_by_reference_passes_the_element_and_the_options()
    {
        var (core, module, _) = Create();
        module.Results["focusElement"] = true;
        var element = new ElementReference("field-1");
        var options = new FocusOptions { PreventScroll = true, Select = true, Retries = 10 };

        var focused = await core.FocusAsync(element, options);

        Assert.True(focused);
        var arguments = module.ArgumentsOf("focusElement");
        Assert.Equal(element, arguments[0]);
        Assert.Same(options, arguments[1]);
    }

    [Fact]
    public void Focus_options_default_to_three_retries_with_scrolling_and_no_selection()
    {
        var options = new FocusOptions();

        Assert.Equal(3, options.Retries);
        Assert.False(options.PreventScroll);
        Assert.False(options.Select);
    }

    [Fact]
    public async Task Ensuring_the_guards_installs_them_through_the_script()
    {
        var (core, module, runtime) = Create();

        await core.EnsureGuardsAsync();

        Assert.Equal([ModulePath], runtime.Imports);
        Assert.Equal(["installGuards"], module.Identifiers);
    }

    [Fact]
    public async Task The_module_is_imported_once_however_many_calls_are_made()
    {
        var (core, _, runtime) = Create();

        await core.EnsureGuardsAsync();
        await core.FocusAsync("a");
        await core.FocusAsync(new ElementReference("b"));

        Assert.Equal([ModulePath], runtime.Imports);
    }

    [Fact]
    public async Task A_failed_import_surfaces_and_the_next_call_imports_again()
    {
        var module = new RecordingJsModule { Results = { ["focusElement"] = true } };
        var attempts = 0;
        var first = new TaskCompletionSource<IJSObjectReference>();
        var runtime = new RecordingJsRuntime(_ => ++attempts == 1
            ? first.Task
            : Task.FromResult<IJSObjectReference>(module));
        var core = new Core(runtime);

        // The failure arrives after the call went out, as a failed fetch does.
        var failing = core.FocusAsync("a").AsTask();
        first.SetException(new JSException("offline"));
        await Assert.ThrowsAsync<JSException>(() => failing);

        Assert.True(await core.FocusAsync("a"));
        Assert.Equal(2, runtime.Imports.Count);
    }

    [Fact]
    public async Task AddBlaizio_registers_the_services_once_per_scope()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new RecordingJsRuntime(new RecordingJsModule()));
        services.AddBlaizio();
        // Disposed asynchronously, as Blazor disposes a scope: the theme service has no
        // synchronous teardown.
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await using var other = provider.CreateAsyncScope();

        var core = scope.ServiceProvider.GetRequiredService<ICore>();
        Assert.IsType<Core>(core);
        Assert.Same(core, scope.ServiceProvider.GetRequiredService<ICore>());
        Assert.NotSame(core, other.ServiceProvider.GetRequiredService<ICore>());
        Assert.IsType<ThemeService>(scope.ServiceProvider.GetRequiredService<IThemeService>());
        // The toast service drives the store of its own scope, not another one's.
        scope.ServiceProvider.GetRequiredService<IToastService>().Show("here");
        Assert.Single(scope.ServiceProvider.GetRequiredService<IToastStore>().Toasts);
        Assert.Empty(other.ServiceProvider.GetRequiredService<IToastStore>().Toasts);
    }

    [Fact]
    public void A_second_AddBlaizio_keeps_the_first_options()
    {
        var services = new ServiceCollection();
        services.AddBlaizio(o => o.Toast.VisibleToasts = 9);
        services.AddBlaizio(o => o.Toast.VisibleToasts = 1);
        using var provider = services.BuildServiceProvider();

        Assert.Equal(9, provider.GetRequiredService<BlaizioOptions>().Toast.VisibleToasts);
    }
}
