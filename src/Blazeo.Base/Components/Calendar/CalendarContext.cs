using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// The state a <see cref="BaseCalendar"/> cascades to its <see cref="BaseCalendarMonth"/> and
/// <see cref="BaseCalendarDay"/> parts: the visible month anchor, the current selection (and the
/// modifiers derived from it), the focused/roving day, the enabled-date rules, and the per-slot
/// class names + chevron icons the styled layer supplies. The parts read the modifiers off it so they
/// all agree, call <see cref="SelectAsync"/> on a click, and register their day buttons via
/// <see cref="RegisterDay"/> so keyboard navigation can move DOM focus.
/// </summary>
public sealed class CalendarContext
{
    /// <summary>The selection behaviour.</summary>
    public required CalendarMode Mode { get; init; }

    /// <summary>First-of-month anchor for the leading month shown.</summary>
    public required DateOnly DisplayMonth { get; init; }

    /// <summary>How many consecutive months are rendered (the anchor and those after it).</summary>
    public required int NumberOfMonths { get; init; }

    /// <summary>The first column of the week grid.</summary>
    public required DayOfWeek WeekStartsOn { get; init; }

    /// <summary>Whether leading/trailing days from adjacent months are rendered (greyed) to fill the grid.</summary>
    public required bool ShowOutsideDays { get; init; }

    /// <summary>The current date, marked with <c>data-today</c>.</summary>
    public required DateOnly Today { get; init; }

    /// <summary>The roving day (gets <c>tabindex=0</c> and DOM focus); null before any focus lands.</summary>
    public required DateOnly? FocusedDate { get; init; }

    /// <summary>The day that is tabbable when nothing is focused yet (selection, else today, else first day).</summary>
    public required DateOnly DefaultFocusable { get; init; }

    /// <summary>Culture used for month/weekday names and the first day of week default.</summary>
    public required CultureInfo Culture { get; init; }

    /// <summary>Per-slot class names from the styled layer.</summary>
    public required CalendarClassNames ClassNames { get; init; }

    /// <summary>Whether the whole calendar is disabled.</summary>
    public bool Disabled { get; init; }

    /// <summary>Lower bound; days before it are disabled.</summary>
    public DateOnly? Min { get; init; }

    /// <summary>Upper bound; days after it are disabled.</summary>
    public DateOnly? Max { get; init; }

    /// <summary>Extra per-day disable predicate (e.g. weekends).</summary>
    public Func<DateOnly, bool>? IsDateDisabled { get; init; }

    /// <summary>Optional leading icon for the previous-month button (styled layer supplies a chevron).</summary>
    public RenderFragment? PreviousIcon { get; init; }

    /// <summary>Optional leading icon for the next-month button.</summary>
    public RenderFragment? NextIcon { get; init; }

    // --- selection snapshot (only the field for the active Mode is meaningful) ---

    /// <summary>The selected day in <see cref="CalendarMode.Single"/>.</summary>
    public DateOnly? Single { get; init; }

    /// <summary>The selected days in <see cref="CalendarMode.Multiple"/>.</summary>
    public IReadOnlyList<DateOnly> Multiple { get; init; } = [];

    /// <summary>The selected span in <see cref="CalendarMode.Range"/>.</summary>
    public DateRange Range { get; init; }

    // --- callbacks into BaseCalendar ---

    /// <summary>Commit a day click; BaseCalendar applies the mode's selection rules.</summary>
    public required Func<DateOnly, Task> SelectAsync { get; init; }

    /// <summary>A day button announces its element so focus can be moved to it by date.</summary>
    public required Action<DateOnly, ElementReference> RegisterDay { get; init; }

    /// <summary>Whether <paramref name="date"/> cannot be selected.</summary>
    public bool IsDisabled(DateOnly date) =>
        Disabled
        || (Min is { } min && date < min)
        || (Max is { } max && date > max)
        || (IsDateDisabled?.Invoke(date) ?? false);

    /// <summary>Whether <paramref name="date"/> is part of the current selection.</summary>
    public bool IsSelected(DateOnly date) => Mode switch
    {
        CalendarMode.Single => Single == date,
        CalendarMode.Multiple => Multiple.Contains(date),
        CalendarMode.Range => Range.Contains(date),
        _ => false,
    };

    /// <summary>Whether <paramref name="date"/> is the (ordered) start of a complete range.</summary>
    public bool IsRangeStart(DateOnly date) =>
        Mode == CalendarMode.Range && Range.IsComplete && Range.Ordered().Low == date;

    /// <summary>Whether <paramref name="date"/> is the (ordered) end of a complete range.</summary>
    public bool IsRangeEnd(DateOnly date) =>
        Mode == CalendarMode.Range && Range.IsComplete && Range.Ordered().High == date;

    /// <summary>Whether <paramref name="date"/> sits strictly between the ends of a complete range.</summary>
    public bool IsRangeMiddle(DateOnly date)
    {
        if (Mode != CalendarMode.Range || !Range.IsComplete) return false;
        var (low, high) = Range.Ordered();
        return date > low && date < high;
    }
}
