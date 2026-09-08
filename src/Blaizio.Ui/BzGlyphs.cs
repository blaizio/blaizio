namespace Blaizio.Ui;

/// <summary>
/// The glyphs the styled components draw with - the check in a select item, the chevron on a
/// trigger, the grip on a sortable row - resolved through one place instead of a set named in
/// every component. Each member defaults to its Tabler icon; point it at another set's icon
/// (or your own <see cref="Icon"/>) and every component follows. The names are the Tabler
/// names, so the default reads as the table it is.
/// </summary>
/// <remarks>
/// Pick the set once with <c>blaizio add --icons lucide</c> (or <c>blaizio apply --icons lucide</c>
/// later) and the CLI retargets every member here; or edit a single member by hand, e.g.
/// <c>X =&gt; Lucide.Outline.X</c>, to swap one glyph app-wide.
/// </remarks>
public static class BzGlyphs
{
    // Feedback and state
    /// <summary>Selected / done: select and dropdown items, checkboxes, steps.</summary>
    public static Icon Check => Tabler.Outline.Check;
    /// <summary>Success in a circle: alerts, toasts, step indicators.</summary>
    public static Icon CircleCheck => Tabler.Outline.CircleCheck;
    /// <summary>Close / clear / dismiss: dialog close, tag remove, input clear.</summary>
    public static Icon X => Tabler.Outline.X;
    /// <summary>Informational note: alerts, callouts.</summary>
    public static Icon InfoCircle => Tabler.Outline.InfoCircle;
    /// <summary>Error / danger: alerts, destructive confirms.</summary>
    public static Icon AlertCircle => Tabler.Outline.AlertCircle;
    /// <summary>Warning: alerts, unsaved-change notices.</summary>
    public static Icon AlertTriangle => Tabler.Outline.AlertTriangle;
    /// <summary>The spinner: buttons, spinner component, loading states.</summary>
    public static Icon Loader2 => Tabler.Outline.Loader2;
    /// <summary>Retry / reload.</summary>
    public static Icon Refresh => Tabler.Outline.Refresh;
    /// <summary>Rotate (image and colour tools).</summary>
    public static Icon RotateClockwise => Tabler.Outline.RotateClockwise;

    // Navigation and disclosure
    /// <summary>Opens downward: select and combobox triggers, accordion, collapsible.</summary>
    public static Icon ChevronDown => Tabler.Outline.ChevronDown;
    /// <summary>Opens upward: scroll-up affordances, sort indicators.</summary>
    public static Icon ChevronUp => Tabler.Outline.ChevronUp;
    /// <summary>Forward / next: breadcrumb separator, submenu, pagination, calendar.</summary>
    public static Icon ChevronRight => Tabler.Outline.ChevronRight;
    /// <summary>Back / previous: pagination, calendar, carousel.</summary>
    public static Icon ChevronLeft => Tabler.Outline.ChevronLeft;
    /// <summary>First page / jump back.</summary>
    public static Icon ChevronsLeft => Tabler.Outline.ChevronsLeft;
    /// <summary>Last page / jump forward.</summary>
    public static Icon ChevronsRight => Tabler.Outline.ChevronsRight;
    /// <summary>Up-down pair on a select trigger or a sortable column header.</summary>
    public static Icon Selector => Tabler.Outline.Selector;
    /// <summary>Sort ascending / move up.</summary>
    public static Icon ArrowUp => Tabler.Outline.ArrowUp;
    /// <summary>Sort descending / move down / scroll to bottom.</summary>
    public static Icon ArrowDown => Tabler.Outline.ArrowDown;
    /// <summary>Forward in a flow.</summary>
    public static Icon ArrowRight => Tabler.Outline.ArrowRight;
    /// <summary>Back in a flow.</summary>
    public static Icon ArrowLeft => Tabler.Outline.ArrowLeft;
    /// <summary>Overflow / more actions.</summary>
    public static Icon Dots => Tabler.Outline.Dots;
    /// <summary>Drag handle: sortable rows, resizable panels.</summary>
    public static Icon GripVertical => Tabler.Outline.GripVertical;
    /// <summary>Search field affordance: command palette, combobox.</summary>
    public static Icon Search => Tabler.Outline.Search;
    /// <summary>Filters / settings sliders.</summary>
    public static Icon Adjustments => Tabler.Outline.Adjustments;
    /// <summary>Sidebar toggle.</summary>
    public static Icon LayoutSidebar => Tabler.Outline.LayoutSidebar;

    // Value editing
    /// <summary>Increment: number input, stepper.</summary>
    public static Icon Plus => Tabler.Outline.Plus;
    /// <summary>Decrement / indeterminate mark.</summary>
    public static Icon Minus => Tabler.Outline.Minus;
    /// <summary>Date picker trigger.</summary>
    public static Icon Calendar => Tabler.Outline.Calendar;
    /// <summary>Date picker trigger with a dropdown hint.</summary>
    public static Icon CalendarDown => Tabler.Outline.CalendarDown;
    /// <summary>Time picker trigger.</summary>
    public static Icon Clock => Tabler.Outline.Clock;
    /// <summary>Colour picker trigger / eyedropper mode.</summary>
    public static Icon ColorPicker => Tabler.Outline.ColorPicker;
    /// <summary>Palette mode in the colour picker.</summary>
    public static Icon Palette => Tabler.Outline.Palette;
    /// <summary>Image fill mode in the colour picker / image placeholder.</summary>
    public static Icon Photo => Tabler.Outline.Photo;

    // Theme and media
    /// <summary>Light theme.</summary>
    public static Icon Sun => Tabler.Outline.Sun;
    /// <summary>Dark theme.</summary>
    public static Icon Moon => Tabler.Outline.Moon;
    /// <summary>System theme.</summary>
    public static Icon DeviceDesktop => Tabler.Outline.DeviceDesktop;
    /// <summary>Play: carousel autoplay, media.</summary>
    public static Icon PlayerPlay => Tabler.Outline.PlayerPlay;
    /// <summary>Pause: carousel autoplay, media.</summary>
    public static Icon PlayerPause => Tabler.Outline.PlayerPause;
}
