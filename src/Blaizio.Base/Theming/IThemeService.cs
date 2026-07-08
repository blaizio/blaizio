namespace Blaizio;

/// <summary>
/// Programmatic access to the three app-wide appearance knobs Blaizio persists in localStorage and
/// applies to <c>&lt;html&gt;</c>: the <b>theme</b> (<c>"light"</c> / <c>"dark"</c> / <c>"system"</c>,
/// the <c>dark</c> class), the <b>style</b> (the active skin, the <c>style-*</c> class) and the
/// <b>direction</b> (the <c>dir</c> attribute). Registered by <c>AddBlaizioBase()</c>; wraps the same
/// <c>theme.js</c> module the theme switcher components use, so changes made here and changes made
/// through a switcher stay in sync (both fire <see cref="ThemeChanged"/>).
/// <para>
/// Backed by JS interop: call these from <c>OnAfterRenderAsync</c> or event handlers, never during
/// prerendering.
/// </para>
/// </summary>
public interface IThemeService
{
    /// <summary>The persisted preference: <c>"light"</c>, <c>"dark"</c> or <c>"system"</c> (the default).</summary>
    ValueTask<string> GetThemeAsync();

    /// <summary>What is on screen right now: <c>"light"</c> or <c>"dark"</c> (system resolved against the OS).</summary>
    ValueTask<string> GetResolvedThemeAsync();

    /// <summary>
    /// Applies and persists a theme preference (<c>"light"</c>, <c>"dark"</c> or <c>"system"</c>;
    /// anything else normalizes to <c>"system"</c>). Notifies every <see cref="ThemeChanged"/> subscriber
    /// and every theme switcher on the page.
    /// </summary>
    ValueTask SetThemeAsync(string theme);

    /// <summary>The active skin name (the <c>style-*</c> class on <c>&lt;html&gt;</c>, without the prefix).</summary>
    ValueTask<string> GetStyleAsync();

    /// <summary>Applies and persists a skin by name (swaps the <c>style-*</c> class on <c>&lt;html&gt;</c>).</summary>
    ValueTask SetStyleAsync(string style);

    /// <summary>The current reading direction (the <c>dir</c> attribute on <c>&lt;html&gt;</c>).</summary>
    ValueTask<Direction> GetDirectionAsync();

    /// <summary>Applies and persists the reading direction.</summary>
    ValueTask SetDirectionAsync(Direction direction);

    /// <summary>
    /// Raised on every theme change - one made through this service, a theme switcher component, or the
    /// OS flipping while in system mode. Starts firing once the service has touched the browser (any
    /// method call initializes it).
    /// </summary>
    event Action<ThemeChangedEventArgs>? ThemeChanged;
}

/// <summary>Payload of <see cref="IThemeService.ThemeChanged"/>.</summary>
/// <param name="Theme">The persisted preference: <c>"light"</c>, <c>"dark"</c> or <c>"system"</c>.</param>
/// <param name="Resolved">What is on screen: <c>"light"</c> or <c>"dark"</c>.</param>
public sealed record ThemeChangedEventArgs(string Theme, string Resolved);
