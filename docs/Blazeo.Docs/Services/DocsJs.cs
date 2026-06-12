using Microsoft.JSInterop;

namespace Blazeo.Docs.Services;

/// <summary>The docs app's browser interop: clipboard + theme persistence.</summary>
public interface IDocsJs : IAsyncDisposable
{
    /// <summary>Copies <paramref name="text"/> to the clipboard.</summary>
    ValueTask CopyAsync(string text);

    /// <summary>The persisted theme name (<c>"light"</c> when none).</summary>
    ValueTask<string> GetThemeAsync();

    /// <summary>Applies and persists a theme. The pre-paint application lives in index.html.</summary>
    ValueTask SetThemeAsync(string theme);

    /// <summary>The persisted Blazeo style name (<c>"ember"</c> when none).</summary>
    ValueTask<string> GetStyleAsync();

    /// <summary>Applies and persists a Blazeo style (the <c>style-*</c> class on the root element).</summary>
    ValueTask SetStyleAsync(string style);
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

    public async ValueTask DisposeAsync()
    {
        if (!_module.IsValueCreated) return;
        try
        {
            await (await _module.Value).DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone — nothing to clean up.
        }
    }
}
