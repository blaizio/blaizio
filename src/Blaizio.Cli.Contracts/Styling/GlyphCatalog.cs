namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// The glyphs the styled components draw with, and the icon each set supplies for them. The
/// components reference <c>BzGlyphs.&lt;Name&gt;</c> (a file in the <c>utils</c> item); the CLI
/// rewrites that file's members against this table when a project picks a set other than Tabler,
/// so one table is the whole "which set do the components use" feature. Names are the Tabler
/// names, the set the map defaults to. Every row names a member in every set - the rewriter
/// never has to fall back, and a test holds the table to the generated icon classes.
/// </summary>
public static class GlyphCatalog
{
    /// <summary>One glyph: its <c>BzGlyphs</c> member name and the member each set supplies,
    /// keyed by <see cref="IconSetDefinition.Name"/>, as the full <c>Class.Family.Icon</c> expression.</summary>
    public sealed record Glyph(string Name, IReadOnlyDictionary<string, string> Members);

    /// <summary>The family each set draws the components' glyphs from - the weight closest to
    /// Tabler's 2px stroke on a 24 grid (Phosphor's Regular is visibly lighter at control sizes).</summary>
    public static readonly IReadOnlyDictionary<string, string> Families = new Dictionary<string, string>
    {
        ["tabler"] = "Tabler.Outline",
        ["lucide"] = "Lucide.Outline",
        ["phosphor"] = "Phosphor.Bold",
        ["remix"] = "Remix.Line",
        ["hugeicons"] = "HugeIcons.StrokeRounded",
    };

    // Column order: tabler, lucide, phosphor, remix, hugeicons - the IconSetCatalog order.
    private static readonly (string Name, string Tabler, string Lucide, string Phosphor, string Remix, string HugeIcons)[] Rows =
    [
        // Feedback and state
        ("Check",           "Check",           "Check",           "Check",             "Check",           "Tick02"),
        ("CircleCheck",     "CircleCheck",     "CircleCheck",     "CheckCircle",       "CheckboxCircle",  "CheckmarkCircle02"),
        ("X",               "X",               "X",               "X",                 "Close",           "Cancel01"),
        ("InfoCircle",      "InfoCircle",      "Info",            "Info",              "Information",     "InformationCircle"),
        ("AlertCircle",     "AlertCircle",     "CircleAlert",     "WarningCircle",     "ErrorWarning",    "AlertCircle"),
        ("AlertTriangle",   "AlertTriangle",   "TriangleAlert",   "Warning",           "Alert",           "Alert02"),
        ("Loader2",         "Loader2",         "LoaderCircle",    "CircleNotch",       "Loader4",         "Loading03"),
        ("Refresh",         "Refresh",         "RefreshCw",       "ArrowsClockwise",   "Refresh",         "Refresh"),
        ("RotateClockwise", "RotateClockwise", "RotateCw",        "ArrowClockwise",    "Clockwise",       "RotateRight01"),
        // Navigation and disclosure
        ("ChevronDown",     "ChevronDown",     "ChevronDown",     "CaretDown",         "ArrowDownS",      "ArrowDown01"),
        ("ChevronUp",       "ChevronUp",       "ChevronUp",       "CaretUp",           "ArrowUpS",        "ArrowUp01"),
        ("ChevronRight",    "ChevronRight",    "ChevronRight",    "CaretRight",        "ArrowRightS",     "ArrowRight01"),
        ("ChevronLeft",     "ChevronLeft",     "ChevronLeft",     "CaretLeft",         "ArrowLeftS",      "ArrowLeft01"),
        ("ChevronsLeft",    "ChevronsLeft",    "ChevronsLeft",    "CaretDoubleLeft",   "ArrowLeftDouble", "ArrowLeftDouble"),
        ("ChevronsRight",   "ChevronsRight",   "ChevronsRight",   "CaretDoubleRight",  "ArrowRightDouble","ArrowRightDouble"),
        ("Selector",        "Selector",        "ChevronsUpDown",  "CaretUpDown",       "ArrowUpDown",     "UnfoldMore"),
        ("ArrowUp",         "ArrowUp",         "ArrowUp",         "ArrowUp",           "ArrowUp",         "ArrowUp02"),
        ("ArrowDown",       "ArrowDown",       "ArrowDown",       "ArrowDown",         "ArrowDown",       "ArrowDown02"),
        ("ArrowRight",      "ArrowRight",      "ArrowRight",      "ArrowRight",        "ArrowRight",      "ArrowRight02"),
        ("ArrowLeft",       "ArrowLeft",       "ArrowLeft",       "ArrowLeft",         "ArrowLeft",       "ArrowLeft02"),
        ("Dots",            "Dots",            "Ellipsis",        "DotsThree",         "More",            "MoreHorizontal"),
        ("GripVertical",    "GripVertical",    "GripVertical",    "DotsSixVertical",   "Draggable",       "DragDropVertical"),
        ("Search",          "Search",          "Search",          "MagnifyingGlass",   "Search",          "Search01"),
        ("Adjustments",     "Adjustments",     "SlidersVertical", "Sliders",           "Equalizer",       "Settings05"),
        ("LayoutSidebar",   "LayoutSidebar",   "PanelLeft",       "SidebarSimple",     "LayoutLeft",      "SidebarLeft"),
        // Value editing
        ("Plus",            "Plus",            "Plus",            "Plus",              "Add",             "PlusSign"),
        ("Minus",           "Minus",           "Minus",           "Minus",             "Subtract",        "MinusSign"),
        ("Calendar",        "Calendar",        "Calendar",        "Calendar",          "Calendar",        "Calendar03"),
        ("CalendarDown",    "CalendarDown",    "CalendarArrowDown","CalendarDots",     "CalendarEvent",   "CalendarArrowDown"),
        ("Clock",           "Clock",           "Clock",           "Clock",             "Time",            "Clock01"),
        ("ColorPicker",     "ColorPicker",     "Pipette",         "Eyedropper",        "Sip",             "ColorPicker"),
        ("Palette",         "Palette",         "Palette",         "Palette",           "Palette",         "Palette"),
        ("Photo",           "Photo",           "Image",           "Image",             "Image",           "Image01"),
        // Theme and media
        ("Sun",             "Sun",             "Sun",             "Sun",               "Sun",             "Sun01"),
        ("Moon",            "Moon",            "Moon",            "Moon",              "Moon",            "Moon02"),
        ("DeviceDesktop",   "DeviceDesktop",   "Monitor",         "Monitor",           "Computer",        "Computer"),
        ("PlayerPlay",      "PlayerPlay",      "Play",            "Play",              "Play",            "Play"),
        ("PlayerPause",     "PlayerPause",     "Pause",           "Pause",             "Pause",           "Pause"),
    ];

    /// <summary>Every glyph, in the order the <c>BzGlyphs</c> file lists them.</summary>
    public static readonly IReadOnlyList<Glyph> All =
    [.. Rows.Select(r => new Glyph(r.Name, new Dictionary<string, string>
    {
        ["tabler"] = $"{Families["tabler"]}.{r.Tabler}",
        ["lucide"] = $"{Families["lucide"]}.{r.Lucide}",
        ["phosphor"] = $"{Families["phosphor"]}.{r.Phosphor}",
        ["remix"] = $"{Families["remix"]}.{r.Remix}",
        ["hugeicons"] = $"{Families["hugeicons"]}.{r.HugeIcons}",
    }))];

    /// <summary>The member expression <paramref name="set"/> supplies for <paramref name="glyph"/>,
    /// or null when either is unknown.</summary>
    public static string? Member(string set, string glyph)
    {
        var row = All.FirstOrDefault(g => string.Equals(g.Name, glyph, StringComparison.Ordinal));
        return row is not null && row.Members.TryGetValue(set.ToLowerInvariant(), out var member) ? member : null;
    }
}
