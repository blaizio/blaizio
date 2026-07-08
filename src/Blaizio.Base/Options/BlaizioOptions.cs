using System.Globalization;

namespace Blaizio;

/// <summary>
/// App-wide component defaults, configured once in <c>AddBlaizio(options => ...)</c>. Every value
/// here is a <em>fallback</em>: the matching component parameter (or per-call options record), when set,
/// always wins. Only mechanism-level knobs with headless types live here - skin-level choices stay on
/// the styled components.
/// </summary>
public sealed class BlaizioOptions
{
    /// <summary>
    /// The default reading direction when no ancestor <see cref="BaseDirectionProvider"/> cascades one
    /// and the component has no explicit <c>Dir</c>. Defaults to <see cref="Blaizio.Direction.Ltr"/>.
    /// </summary>
    public Direction Direction { get; set; } = Direction.Ltr;

    /// <summary>
    /// The culture used by date/number components (Calendar, InputDate, InputNumber) when their own
    /// <c>Culture</c> parameter is unset. Defaults to <see cref="CultureInfo.CurrentCulture"/> at render
    /// time when left <see langword="null"/>.
    /// </summary>
    public CultureInfo? Culture { get; set; }

    /// <summary>
    /// The options applied when an <see cref="IDialogService"/> call passes none. Assign a derived
    /// record (e.g. the styled layer's <c>UiDialogOptions</c>) to default the skin knobs too.
    /// </summary>
    public DialogOptions DialogDefaults { get; set; } = new();

    /// <summary>Toast provider defaults (position, duration, ...).</summary>
    public ToastDefaults Toast { get; } = new();

    /// <summary>Tooltip timing defaults.</summary>
    public TooltipDefaults Tooltip { get; } = new();

    /// <summary>Hover card timing defaults.</summary>
    public HoverCardDefaults HoverCard { get; } = new();

    /// <summary>Navigation menu timing defaults.</summary>
    public NavigationMenuDefaults NavigationMenu { get; } = new();

    /// <summary>Dropdown / context menu timing defaults.</summary>
    public MenuDefaults Menu { get; } = new();

    /// <summary>Calendar rendering defaults (also used by InputDate's popup calendar).</summary>
    public CalendarDefaults Calendar { get; } = new();

    // Shared fallback so components resolve leniently when DI has no registration (e.g. unit tests
    // rendering a primitive without AddBlaizio). The s_ prefix is the standard .NET naming for a
    // private static field; it stays private so the ONLY way to read options is Resolve - exposing
    // it publicly would invite reading built-in defaults that ignore the app's configuration.
    private static readonly BlaizioOptions s_fallback = new();

    /// <summary>
    /// The registered <see cref="BlaizioOptions"/>, or the built-in defaults when the container has none
    /// (a component rendered without <c>AddBlaizio()</c>, e.g. under a unit test).
    /// </summary>
    public static BlaizioOptions Resolve(IServiceProvider services) =>
        services.GetService(typeof(BlaizioOptions)) as BlaizioOptions ?? s_fallback;
}
