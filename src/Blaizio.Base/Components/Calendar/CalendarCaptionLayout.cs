namespace Blaizio;

/// <summary>
/// How the month caption is rendered - a plain label, or dropdowns that let you jump months and/or
/// years directly (handy for far-off dates like a birthday). Mirrors react-day-picker's
/// <c>captionLayout</c>.
/// </summary>
public enum CalendarCaptionLayout
{
    /// <summary>A read-only "Month Year" label (default). Navigate with the prev/next arrows.</summary>
    Label,

    /// <summary>Both the month and the year are <c>&lt;select&gt;</c> dropdowns.</summary>
    Dropdown,

    /// <summary>The month is a dropdown; the year stays a label.</summary>
    DropdownMonths,

    /// <summary>The year is a dropdown; the month stays a label.</summary>
    DropdownYears,
}
