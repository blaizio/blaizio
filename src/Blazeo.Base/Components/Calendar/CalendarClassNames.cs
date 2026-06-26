namespace Blazeo;

/// <summary>
/// Per-slot class names the styled layer threads through <see cref="BaseCalendar"/> to its
/// <see cref="BaseCalendarMonth"/> / <see cref="BaseCalendarDay"/> parts - the analogue of
/// react-day-picker's <c>classNames</c> map. Every slot is optional; the unstyled primitive leaves
/// them all <see langword="null"/>, so it renders structure with no look of its own.
/// </summary>
public sealed record CalendarClassNames
{
    /// <summary>The wrapper around the month grid(s).</summary>
    public string? Months { get; init; }

    /// <summary>One month column (caption + grid).</summary>
    public string? Month { get; init; }

    /// <summary>The previous/next button row overlaid on the caption.</summary>
    public string? Nav { get; init; }

    /// <summary>The previous-month button.</summary>
    public string? ButtonPrevious { get; init; }

    /// <summary>The next-month button.</summary>
    public string? ButtonNext { get; init; }

    /// <summary>The month/year caption row.</summary>
    public string? MonthCaption { get; init; }

    /// <summary>The caption text (e.g. "June 2026").</summary>
    public string? CaptionLabel { get; init; }

    /// <summary>The wrapper around the month/year dropdowns (caption layout).</summary>
    public string? Dropdowns { get; init; }

    /// <summary>The wrapper around one dropdown - holds the visible label + the transparent select.</summary>
    public string? DropdownRoot { get; init; }

    /// <summary>The visible label (text + chevron) shown over a dropdown.</summary>
    public string? DropdownLabel { get; init; }

    /// <summary>The (transparent, overlaid) <c>&lt;select&gt;</c> element itself.</summary>
    public string? Dropdown { get; init; }

    /// <summary>The <c>&lt;table&gt;</c> grid.</summary>
    public string? MonthGrid { get; init; }

    /// <summary>The weekday header row.</summary>
    public string? Weekdays { get; init; }

    /// <summary>A single weekday header cell.</summary>
    public string? Weekday { get; init; }

    /// <summary>The week-number column header cell.</summary>
    public string? WeekNumberHeader { get; init; }

    /// <summary>A week-number cell (leading each week row).</summary>
    public string? WeekNumber { get; init; }

    /// <summary>A week (one row of days).</summary>
    public string? Week { get; init; }

    /// <summary>A day cell (the <c>&lt;td&gt;</c>).</summary>
    public string? Day { get; init; }

    /// <summary>The day button inside the cell.</summary>
    public string? DayButton { get; init; }
}
