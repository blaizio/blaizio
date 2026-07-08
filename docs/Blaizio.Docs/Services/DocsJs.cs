using Microsoft.JSInterop;

namespace Blaizio.Docs.Services;

/// <summary>The docs app's browser interop: clipboard + theme persistence.</summary>
public interface IDocsJs : IAsyncDisposable
{
    /// <summary>Copies <paramref name="text"/> to the clipboard.</summary>
    ValueTask CopyAsync(string text);

    /// <summary>The persisted theme name (<c>"light"</c> when none).</summary>
    ValueTask<string> GetThemeAsync();

    /// <summary>Applies and persists a theme. The pre-paint application lives in index.html.</summary>
    ValueTask SetThemeAsync(string theme);

    /// <summary>The persisted Blaizio style name (<c>"ember"</c> when none).</summary>
    ValueTask<string> GetStyleAsync();

    /// <summary>Applies and persists a Blaizio style (the <c>style-*</c> class on the root element).</summary>
    ValueTask SetStyleAsync(string style);

    /// <summary>The persisted reading direction (<c>"ltr"</c> when none).</summary>
    ValueTask<string> GetDirAsync();

    /// <summary>Applies and persists a reading direction (the <c>dir</c> attribute on the root element). The pre-paint application lives in index.html.</summary>
    ValueTask SetDirAsync(string dir);

    /// <summary>Re-places the sidebar's sliding active-row indicators. Call after the nav (re)renders.</summary>
    ValueTask NavPositionAsync();

    /// <summary>Scrolls the active nav row into view inside the sidebar's scroller (deep links, first load).</summary>
    ValueTask NavRevealAsync();

    /// <summary>Re-measures the scroll-fade edges of every <c>[data-scroll-activity]</c> element.</summary>
    ValueTask ScrollFadeRefreshAsync();
}

/// <summary>
/// Wraps the wwwroot/js/docs.js ES module behind typed methods (the ArcBlazor CoreService
/// pattern): the module is imported lazily, once, and no <c>window</c> globals are involved.
/// </summary>
internal sealed class DocsJs(IJSRuntime js) : IDocsJs
{
    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        js.InvokeAsync<IJSObjectReference>("import", "./js/docs.js").AsTask());

    public async ValueTask CopyAsync(string text) =>
        await (await _module.Value).InvokeVoidAsync("copy", text);

    public async ValueTask<string> GetThemeAsync() =>
        await (await _module.Value).InvokeAsync<string>("getTheme");

    public async ValueTask SetThemeAsync(string theme) =>
        await (await _module.Value).InvokeVoidAsync("setTheme", theme);

    public async ValueTask<string> GetStyleAsync() =>
        await (await _module.Value).InvokeAsync<string>("getStyle");

    public async ValueTask SetStyleAsync(string style) =>
        await (await _module.Value).InvokeVoidAsync("setStyle", style);

    public async ValueTask<string> GetDirAsync() =>
        await (await _module.Value).InvokeAsync<string>("getDir");

    public async ValueTask SetDirAsync(string dir) =>
        await (await _module.Value).InvokeVoidAsync("setDir", dir);

    public async ValueTask NavPositionAsync() =>
        await (await _module.Value).InvokeVoidAsync("navPosition");

    public async ValueTask NavRevealAsync() =>
        await (await _module.Value).InvokeVoidAsync("navReveal");

    public async ValueTask ScrollFadeRefreshAsync() =>
        await (await _module.Value).InvokeVoidAsync("scrollFadeRefresh");

    public async ValueTask DisposeAsync()
    {
        if (!_module.IsValueCreated) return;
        try
        {
            await (await _module.Value).DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone - nothing to clean up.
        }
    }
}
