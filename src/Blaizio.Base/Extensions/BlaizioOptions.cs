using System.Globalization;

namespace Blaizio;

/// <summary>
/// App-wide component defaults, configured once in <c>AddBlaizioBase(options => ...)</c>. Every value
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
    // rendering a primitive without AddBlaizioBase). Never mutated - Resolve hands it out as-is.
    private static readonly BlaizioOptions s_fallback = new();

    /// <summary>
    /// The registered <see cref="BlaizioOptions"/>, or the built-in defaults when the container has none
    /// (a component rendered without <c>AddBlaizioBase()</c>, e.g. under a unit test).
    /// </summary>
    public static BlaizioOptions Resolve(IServiceProvider services) =>
        services.GetService(typeof(BlaizioOptions)) as BlaizioOptions ?? s_fallback;
}

/// <summary>App-wide defaults for the toast provider. Provider parameters override per app root.</summary>
public sealed class ToastDefaults
{
    /// <summary>The corner toasts stack in. Defaults to <see cref="ToastPosition.BottomRight"/>.</summary>
    public ToastPosition Position { get; set; } = ToastPosition.BottomRight;

    /// <summary>How many toasts stay fully visible before the rest collapse behind. Defaults to 3.</summary>
    public int VisibleToasts { get; set; } = 3;

    /// <summary>The auto-close duration. Defaults to 4 seconds.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>Show a corner close button on every toast. Defaults to <see langword="false"/>.</summary>
    public bool CloseButton { get; set; }

    /// <summary>Fill every toast with its type colour. Defaults to <see langword="false"/>.</summary>
    public bool RichColors { get; set; }
}

/// <summary>App-wide tooltip timing defaults.</summary>
public sealed class TooltipDefaults
{
    /// <summary>Hover delay (ms) before a tooltip opens. Defaults to 400.</summary>
    public int DelayDuration { get; set; } = 400;

    /// <summary>How long (ms) after a tooltip closes that the next one in a group still opens instantly. Defaults to 300.</summary>
    public int SkipDelayDuration { get; set; } = 300;
}

/// <summary>App-wide hover card timing defaults.</summary>
public sealed class HoverCardDefaults
{
    /// <summary>Hover delay (ms) before a card opens. Defaults to 100.</summary>
    public int OpenDelay { get; set; } = 100;

    /// <summary>Grace period (ms) after the pointer leaves before the card closes. Defaults to 200.</summary>
    public int CloseDelay { get; set; } = 200;
}

/// <summary>App-wide navigation menu timing defaults.</summary>
public sealed class NavigationMenuDefaults
{
    /// <summary>Hover delay (ms) before an item's content opens. Defaults to 200.</summary>
    public int OpenDelay { get; set; } = 200;

    /// <summary>Grace period (ms) after the pointer leaves before the content closes. Defaults to 150.</summary>
    public int CloseDelay { get; set; } = 150;
}

/// <summary>App-wide menu timing defaults.</summary>
public sealed class MenuDefaults
{
    /// <summary>Hover delay (ms) before a submenu opens. Defaults to 100.</summary>
    public int SubOpenDelay { get; set; } = 100;
}

/// <summary>App-wide calendar rendering defaults.</summary>
public sealed class CalendarDefaults
{
    /// <summary>First day of the week. <see langword="null"/> (the default) follows the culture.</summary>
    public DayOfWeek? WeekStartsOn { get; set; }

    /// <summary>Render day/year numbers with the culture's native digits. Defaults to <see langword="true"/>.</summary>
    public bool NativeDigits { get; set; } = true;
}
