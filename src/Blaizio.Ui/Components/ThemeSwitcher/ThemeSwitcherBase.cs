using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blaizio.Ui;

/// <summary>
/// Shared plumbing for the theme switcher components (<c>BzThemeSwitcher</c>, <c>BzThemeMenu</c>,
/// <c>BzThemeToggle</c>): binds Blaizio.Base's theme module (localStorage persistence; the
/// pre-paint counterpart is its <c>dist/boot.js</c>) behind two rendered states. Every switcher
/// on the page subscribes to the module's change notifications, so a theme set anywhere - another
/// switcher, or the OS flipping while in system mode - re-renders them all in step.
/// </summary>
public abstract class ThemeSwitcherBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected IJSRuntime Js { get; set; } = default!;

    /// <summary>The persisted preference: <c>"light"</c>, <c>"dark"</c> or <c>"system"</c> (the default).</summary>
    protected string Theme { get; private set; } = "system";

    /// <summary>What is on screen right now: <c>"light"</c> or <c>"dark"</c> (system resolved against the OS).</summary>
    protected string Resolved { get; private set; } = "light";

    private IJSObjectReference? _module;
    private DotNetObjectReference<ThemeSwitcherBase>? _selfRef;
    private int _watchId;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            _module = await Js.InvokeAsync<IJSObjectReference>("import", "./_content/blaizio.base/dist/theme.js");
            Theme = await _module.InvokeAsync<string>("getTheme");
            Resolved = await _module.InvokeAsync<string>("getResolvedTheme");
            _selfRef = DotNetObjectReference.Create(this);
            _watchId = await _module.InvokeAsync<int>("watchTheme", _selfRef);
            StateHasChanged();
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone before the module was wired.
        }
    }

    /// <summary>Applies and persists a preference. State updates arrive back through the watcher.</summary>
    protected async Task SetThemeAsync(string theme)
    {
        if (_module is null) return;
        try
        {
            await _module.InvokeVoidAsync("setTheme", theme);
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone - nothing to apply to.
        }
    }

    /// <summary>Invoked from JS on every theme change (this switcher's or any other's).</summary>
    [JSInvokable]
    public Task OnThemeChangedAsync(string theme, string resolved)
    {
        Theme = theme;
        Resolved = resolved;
        return InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                if (_watchId != 0) await _module.InvokeVoidAsync("unwatchTheme", _watchId);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone - nothing to clean up.
        }
        finally
        {
            _selfRef?.Dispose();
        }
    }
}
