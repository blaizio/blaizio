namespace Blaizio;

/// <summary>
/// Programmatic access to the four app-wide appearance knobs Blaizio persists in localStorage and
/// applies to <c>&lt;html&gt;</c>: the <b>theme</b> (<c>"light"</c> / <c>"dark"</c> / <c>"system"</c>,
/// the <c>dark</c> class), the <b>style</b> (the active skin, the <c>style-*</c> class), the
/// <b>preset</b> (the active color palette, the <c>preset-*</c> class) and the <b>direction</b>
/// (the <c>dir</c> attribute). Registered by <c>AddBlaizio()</c>; wraps the same
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

    /// <summary>
    /// The active color preset name (the <c>preset-*</c> class on <c>&lt;html&gt;</c>, without the
    /// prefix); <c>"nova"</c> - the built-in default palette - when none is applied.
    /// </summary>
    ValueTask<string> GetPresetAsync();

    /// <summary>
    /// Applies and persists a color preset by name (swaps the <c>preset-*</c> class on
    /// <c>&lt;html&gt;</c>; <c>"nova"</c> removes it, restoring the default palette).
    /// </summary>
    ValueTask SetPresetAsync(string preset);

    /// <summary>The current reading direction (the <c>dir</c> attribute on <c>&lt;html&gt;</c>).</summary>
    ValueTask<Direction> GetDirectionAsync();

    /// <summary>Applies and persists the reading direction.</summary>
    ValueTask SetDirectionAsync(Direction direction);

    /// <summary>The active chart palette (the <c>chart-*</c> class); <c>"default"</c> when none.</summary>
    ValueTask<string> GetChartAsync();

    /// <summary>Applies and persists a chart palette (swaps the <c>chart-*</c> class; <c>"default"</c> removes it).</summary>
    ValueTask SetChartAsync(string chart);

    /// <summary>The active radius scale (the <c>radius-*</c> class); <c>"default"</c> when none.</summary>
    ValueTask<string> GetRadiusAsync();

    /// <summary>Applies and persists a radius scale (swaps the <c>radius-*</c> class; <c>"default"</c> removes it).</summary>
    ValueTask SetRadiusAsync(string radius);

    /// <summary>The active body font stack (the <c>font-*</c> class); <c>"default"</c> when none.</summary>
    ValueTask<string> GetFontAsync();

    /// <summary>Applies and persists a body font stack (swaps the <c>font-*</c> class; <c>"default"</c> removes it).</summary>
    ValueTask SetFontAsync(string font);

    /// <summary>The active heading font stack (the <c>heading-*</c> class); <c>"default"</c> when none.</summary>
    ValueTask<string> GetHeadingAsync();

    /// <summary>Applies and persists a heading font stack (swaps the <c>heading-*</c> class; <c>"default"</c> removes it).</summary>
    ValueTask SetHeadingAsync(string heading);

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
