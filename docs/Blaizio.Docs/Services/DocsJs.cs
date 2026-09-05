using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blaizio.Docs.Services;

/// <summary>The docs app's browser interop: clipboard + theme persistence.</summary>
public interface IDocsJs : IAsyncDisposable
{
    /// <summary>Copies <paramref name="text"/> to the clipboard.</summary>
    ValueTask CopyAsync(string text);

    /// <summary>The persisted theme preference: <c>"light"</c>, <c>"dark"</c> or <c>"system"</c> (the default).</summary>
    ValueTask<string> GetThemeAsync();

    /// <summary>Applies and persists a theme preference. The pre-paint application lives in index.html.</summary>
    ValueTask SetThemeAsync(string theme);

    /// <summary>The persisted Blaizio style name (<c>"ember"</c> when none).</summary>
    ValueTask<string> GetStyleAsync();

    /// <summary>Applies and persists a Blaizio style (the <c>style-*</c> class on the root element).</summary>
    ValueTask SetStyleAsync(string style);

    /// <summary>The persisted color preset name (<c>"nova"</c>, the default palette, when none).</summary>
    ValueTask<string> GetPresetAsync();

    /// <summary>Applies and persists a color preset (the <c>preset-*</c> class on the root element; <c>"nova"</c> removes it).</summary>
    ValueTask SetPresetAsync(string preset);

    /// <summary>The persisted reading direction (<c>"ltr"</c> when none).</summary>
    ValueTask<string> GetDirAsync();

    /// <summary>Applies and persists a reading direction (the <c>dir</c> attribute on the root element). The pre-paint application lives in index.html.</summary>
    ValueTask SetDirAsync(string dir);

    /// <summary>The persisted chart palette (<c>"default"</c> when none).</summary>
    ValueTask<string> GetChartAsync();

    /// <summary>Applies and persists a chart palette (the <c>chart-*</c> class; <c>"default"</c> removes it).</summary>
    ValueTask SetChartAsync(string chart);

    /// <summary>The persisted radius scale (<c>"default"</c> when none).</summary>
    ValueTask<string> GetRadiusAsync();

    /// <summary>Applies and persists a radius scale (the <c>radius-*</c> class; <c>"default"</c> removes it).</summary>
    ValueTask SetRadiusAsync(string radius);

    /// <summary>The persisted body font stack (<c>"default"</c> when none).</summary>
    ValueTask<string> GetFontAsync();

    /// <summary>Applies and persists a body font stack (the <c>font-*</c> class; <c>"default"</c> removes it).</summary>
    ValueTask SetFontAsync(string font);

    /// <summary>The persisted heading font stack (<c>"default"</c> when none).</summary>
    ValueTask<string> GetHeadingAsync();

    /// <summary>Applies and persists a heading font stack (the <c>heading-*</c> class; <c>"default"</c> removes it).</summary>
    ValueTask SetHeadingAsync(string heading);

    /// <summary>Injects the Google Fonts stylesheet for a Themes webfont pick (once per URL).</summary>
    ValueTask LoadWebFontAsync(string href);

    /// <summary>The applied /community theme's name, or <c>""</c> when none is active.</summary>
    ValueTask<string> GetCommunityThemeAsync();

    /// <summary>Applies and persists a /community theme: its <c>:root</c>/<c>.dark</c> override CSS, injected as a style element (the pre-paint re-injection lives in index.html).</summary>
    ValueTask SetCommunityThemeAsync(string name, string css);

    /// <summary>Removes the applied /community theme and its persisted state.</summary>
    ValueTask ClearCommunityThemeAsync();

    /// <summary>The persisted /themes token-override stylesheet text (<c>""</c> when none).</summary>
    ValueTask<string> GetTokenOverridesAsync();

    /// <summary>Applies and persists the token-override stylesheet (empty clears it). The
    /// pre-paint re-injection lives in index.html next to the community snippet.</summary>
    ValueTask SetTokenOverridesAsync(string css);

    /// <summary>Whether the document currently renders dark (the resolved mode, not the preference).</summary>
    ValueTask<bool> IsDarkAsync();

    /// <summary>The live computed value of a theme custom property (name without <c>--</c>), <c>""</c> when undefined.</summary>
    ValueTask<string> GetTokenValueAsync(string name);

    /// <summary>Bulk form of <see cref="GetTokenValueAsync"/> - one call for the dock's swatch strip.</summary>
    ValueTask<Dictionary<string, string>> GetTokenValuesAsync(string[] names);

    /// <summary><see cref="GetTokenValuesAsync"/> resolved in the requested mode rather than the
    /// one on screen - the /themes token popover editing dark from a light page. The document is
    /// never repainted in the other mode.</summary>
    ValueTask<Dictionary<string, string>> GetTokenValuesInModeAsync(string[] names, bool dark);

    /// <summary>The persisted sidebar grouping preference (<c>false</c>, the flat list, when none).</summary>
    ValueTask<bool> GetNavGroupedAsync();

    /// <summary>Persists the sidebar grouping preference.</summary>
    ValueTask SetNavGroupedAsync(bool grouped);

    /// <summary>Re-places the sidebar's sliding active-row indicators. Call after the nav (re)renders.</summary>
    ValueTask NavPositionAsync();

    /// <summary>Scrolls the active nav row into view inside the sidebar's scroller (deep links, first load).</summary>
    ValueTask NavRevealAsync();

    /// <summary>
    /// Starts the landing page scroll reveal: every <c>[data-reveal]</c> under <paramref name="root"/>
    /// is marked <c>data-revealed</c> the first time it enters the viewport. Returns the instance;
    /// call <c>dispose</c> on it (then dispose the reference) to stop.
    /// </summary>
    ValueTask<IJSObjectReference> RevealStartAsync(ElementReference root);

    /// <summary>
    /// Starts Inspect mode on a demo preview: outlines every <c>[data-slot]</c> part and reports
    /// the hovered one back through <paramref name="receiver"/>'s <c>OnInspectHover</c>. Returns
    /// the instance; call <c>dispose</c> on it (then dispose the reference) to stop.
    /// </summary>
    ValueTask<IJSObjectReference> InspectStartAsync<T>(ElementReference root, DotNetObjectReference<T> receiver)
        where T : class;

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

    public async ValueTask<string> GetPresetAsync() =>
        await (await _module.Value).InvokeAsync<string>("getPreset");

    public async ValueTask SetPresetAsync(string preset) =>
        await (await _module.Value).InvokeVoidAsync("setPreset", preset);

    public async ValueTask<string> GetDirAsync() =>
        await (await _module.Value).InvokeAsync<string>("getDir");

    public async ValueTask SetDirAsync(string dir) =>
        await (await _module.Value).InvokeVoidAsync("setDir", dir);

    public async ValueTask<string> GetChartAsync() =>
        await (await _module.Value).InvokeAsync<string>("getChart");

    public async ValueTask SetChartAsync(string chart) =>
        await (await _module.Value).InvokeVoidAsync("setChart", chart);

    public async ValueTask<string> GetRadiusAsync() =>
        await (await _module.Value).InvokeAsync<string>("getRadius");

    public async ValueTask SetRadiusAsync(string radius) =>
        await (await _module.Value).InvokeVoidAsync("setRadius", radius);

    public async ValueTask<string> GetFontAsync() =>
        await (await _module.Value).InvokeAsync<string>("getFont");

    public async ValueTask SetFontAsync(string font) =>
        await (await _module.Value).InvokeVoidAsync("setFont", font);

    public async ValueTask<string> GetHeadingAsync() =>
        await (await _module.Value).InvokeAsync<string>("getHeading");

    public async ValueTask SetHeadingAsync(string heading) =>
        await (await _module.Value).InvokeVoidAsync("setHeading", heading);

    public async ValueTask LoadWebFontAsync(string href) =>
        await (await _module.Value).InvokeVoidAsync("loadWebFont", href);

    public async ValueTask<string> GetCommunityThemeAsync() =>
        await (await _module.Value).InvokeAsync<string>("getCommunityTheme");

    public async ValueTask SetCommunityThemeAsync(string name, string css) =>
        await (await _module.Value).InvokeVoidAsync("setCommunityTheme", name, css);

    public async ValueTask ClearCommunityThemeAsync() =>
        await (await _module.Value).InvokeVoidAsync("clearCommunityTheme");

    public async ValueTask<string> GetTokenOverridesAsync() =>
        await (await _module.Value).InvokeAsync<string>("getTokenOverrides");

    public async ValueTask SetTokenOverridesAsync(string css) =>
        await (await _module.Value).InvokeVoidAsync("setTokenOverrides", css);

    public async ValueTask<bool> IsDarkAsync() =>
        await (await _module.Value).InvokeAsync<bool>("isDark");

    public async ValueTask<string> GetTokenValueAsync(string name) =>
        await (await _module.Value).InvokeAsync<string>("getTokenValue", name);

    public async ValueTask<Dictionary<string, string>> GetTokenValuesAsync(string[] names) =>
        await (await _module.Value).InvokeAsync<Dictionary<string, string>>("getTokenValues", (object)names);

    public async ValueTask<Dictionary<string, string>> GetTokenValuesInModeAsync(string[] names, bool dark) =>
        await (await _module.Value).InvokeAsync<Dictionary<string, string>>("getTokenValuesInMode", names, dark);

    public async ValueTask<bool> GetNavGroupedAsync() =>
        await (await _module.Value).InvokeAsync<bool>("getNavGrouped");

    public async ValueTask SetNavGroupedAsync(bool grouped) =>
        await (await _module.Value).InvokeVoidAsync("setNavGrouped", grouped);

    public async ValueTask NavPositionAsync() =>
        await (await _module.Value).InvokeVoidAsync("navPosition");

    public async ValueTask NavRevealAsync() =>
        await (await _module.Value).InvokeVoidAsync("navReveal");

    public async ValueTask<IJSObjectReference> RevealStartAsync(ElementReference root) =>
        await (await _module.Value).InvokeAsync<IJSObjectReference>("revealStart", root);

    public async ValueTask<IJSObjectReference> InspectStartAsync<T>(
        ElementReference root, DotNetObjectReference<T> receiver) where T : class =>
        await (await _module.Value).InvokeAsync<IJSObjectReference>("inspectStart", root, receiver);

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
