using Microsoft.JSInterop;

namespace Blaizio;

/// <inheritdoc cref="IThemeService"/>
public sealed class ThemeService(IJSRuntime js) : IThemeService, IAsyncDisposable
{
    private Task<IJSObjectReference>? _moduleTask;
    private DotNetObjectReference<ThemeService>? _selfRef;
    private int _watchId;

    /// <inheritdoc/>
    public event Action<ThemeChangedEventArgs>? ThemeChanged;

    /// <inheritdoc/>
    public async ValueTask<string> GetThemeAsync() =>
        await (await Module()).InvokeAsync<string>("getTheme");

    /// <inheritdoc/>
    public async ValueTask<string> GetResolvedThemeAsync() =>
        await (await Module()).InvokeAsync<string>("getResolvedTheme");

    /// <inheritdoc/>
    public async ValueTask SetThemeAsync(string theme) =>
        await (await Module()).InvokeVoidAsync("setTheme", theme);

    /// <inheritdoc/>
    public async ValueTask<string> GetStyleAsync() =>
        await (await Module()).InvokeAsync<string>("getStyle");

    /// <inheritdoc/>
    public async ValueTask SetStyleAsync(string style)
    {
        ArgumentException.ThrowIfNullOrEmpty(style);
        await (await Module()).InvokeVoidAsync("setStyle", style);
    }

    /// <inheritdoc/>
    public async ValueTask<string> GetPresetAsync() =>
        await (await Module()).InvokeAsync<string>("getPreset");

    /// <inheritdoc/>
    public async ValueTask SetPresetAsync(string preset)
    {
        ArgumentException.ThrowIfNullOrEmpty(preset);
        await (await Module()).InvokeVoidAsync("setPreset", preset);
    }

    /// <inheritdoc/>
    public async ValueTask<Direction> GetDirectionAsync() =>
        await (await Module()).InvokeAsync<string>("getDir") == "rtl" ? Direction.Rtl : Direction.Ltr;

    /// <inheritdoc/>
    public async ValueTask SetDirectionAsync(Direction direction) =>
        await (await Module()).InvokeVoidAsync("setDir", direction.ToAttribute());

    /// <inheritdoc/>
    public async ValueTask<string> GetChartAsync() =>
        await (await Module()).InvokeAsync<string>("getChart");

    /// <inheritdoc/>
    public async ValueTask SetChartAsync(string chart)
    {
        ArgumentException.ThrowIfNullOrEmpty(chart);
        await (await Module()).InvokeVoidAsync("setChart", chart);
    }

    /// <inheritdoc/>
    public async ValueTask<string> GetRadiusAsync() =>
        await (await Module()).InvokeAsync<string>("getRadius");

    /// <inheritdoc/>
    public async ValueTask SetRadiusAsync(string radius)
    {
        ArgumentException.ThrowIfNullOrEmpty(radius);
        await (await Module()).InvokeVoidAsync("setRadius", radius);
    }

    /// <inheritdoc/>
    public async ValueTask<string> GetFontAsync() =>
        await (await Module()).InvokeAsync<string>("getFont");

    /// <inheritdoc/>
    public async ValueTask SetFontAsync(string font)
    {
        ArgumentException.ThrowIfNullOrEmpty(font);
        await (await Module()).InvokeVoidAsync("setFont", font);
    }

    /// <inheritdoc/>
    public async ValueTask<string> GetHeadingAsync() =>
        await (await Module()).InvokeAsync<string>("getHeading");

    /// <inheritdoc/>
    public async ValueTask SetHeadingAsync(string heading)
    {
        ArgumentException.ThrowIfNullOrEmpty(heading);
        await (await Module()).InvokeVoidAsync("setHeading", heading);
    }

    /// <summary>Invoked from JS on every theme change (this service's, a switcher's, or an OS flip).</summary>
    [JSInvokable]
    public Task OnThemeChangedAsync(string theme, string resolved)
    {
        ThemeChanged?.Invoke(new ThemeChangedEventArgs(theme, resolved));
        return Task.CompletedTask;
    }

    // One import per scope, shared by every caller; the watcher is wired alongside so ThemeChanged
    // fires from the first use on. The task is cached (not the reference) so concurrent first calls
    // await the same import instead of racing a second one.
    private Task<IJSObjectReference> Module() => _moduleTask ??= ImportAndWatch();

    private async Task<IJSObjectReference> ImportAndWatch()
    {
        var module = await js.InvokeAsync<IJSObjectReference>("import", "./_content/blaizio.base/dist/theme.js");
        _selfRef = DotNetObjectReference.Create(this);
        _watchId = await module.InvokeAsync<int>("watchTheme", _selfRef);
        return module;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_moduleTask is { IsCompletedSuccessfully: true })
            {
                var module = _moduleTask.Result;
                if (_watchId != 0) await module.InvokeVoidAsync("unwatchTheme", _watchId);
                await module.DisposeAsync();
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
