using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>One option in a caption month/year dropdown: the value to commit and the label to show.</summary>
/// <param name="Value">The calendar-system month number (1-based) or year.</param>
/// <param name="Label">The display text (localized month name, or the year in the culture's digits).</param>
public readonly record struct CalendarOption(int Value, string Label);

/// <summary>
/// What a <c>MonthCaption</c> template receives, so the styled layer can render the month/year pickers
/// with its own dropdown component (rather than a native <c>&lt;select&gt;</c>): the current month and
/// year, the option lists, which pickers to show, and the callbacks to commit a pick.
/// </summary>
public sealed class CalendarCaptionContext
{
    /// <summary>The current calendar-system year.</summary>
    public required int Year { get; init; }

    /// <summary>The current calendar-system month number (1-based).</summary>
    public required int MonthNumber { get; init; }

    /// <summary>The current month's localized name.</summary>
    public required string MonthName { get; init; }

    /// <summary>The current year, in the culture's digits.</summary>
    public required string YearLabel { get; init; }

    /// <summary>Whether to show the month picker (else a plain label).</summary>
    public required bool ShowMonthDropdown { get; init; }

    /// <summary>Whether to show the year picker (else a plain label).</summary>
    public required bool ShowYearDropdown { get; init; }

    /// <summary>The selectable months (number + localized name).</summary>
    public required IReadOnlyList<CalendarOption> Months { get; init; }

    /// <summary>The selectable years (number + digit label).</summary>
    public required IReadOnlyList<CalendarOption> Years { get; init; }

    /// <summary>Commit a chosen month number; the calendar jumps to it in the current year.</summary>
    public required EventCallback<int> MonthSelected { get; init; }

    /// <summary>Commit a chosen year; the calendar jumps to the current month in it.</summary>
    public required EventCallback<int> YearSelected { get; init; }
}
