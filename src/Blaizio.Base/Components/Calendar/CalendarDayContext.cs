namespace Blaizio;

/// <summary>
/// What a <c>DayContent</c> template receives for one day cell, so a consumer can render richer cells
/// (e.g. the day number with a price or availability under it) while still getting the culture-correct
/// day label and the day's modifiers.
/// </summary>
/// <param name="Date">The absolute day this cell represents.</param>
/// <param name="DayLabel">The day-of-month already formatted in the culture's calendar and digits.</param>
/// <param name="IsToday">Whether this is the calendar's "today".</param>
/// <param name="IsSelected">Whether this day is part of the current selection.</param>
/// <param name="IsOutside">Whether this day belongs to an adjacent month.</param>
/// <param name="IsDisabled">Whether this day is disabled (bounds or the disable predicate).</param>
public readonly record struct CalendarDayContext(
    DateOnly Date,
    string DayLabel,
    bool IsToday,
    bool IsSelected,
    bool IsOutside,
    bool IsDisabled);
