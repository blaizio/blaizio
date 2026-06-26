using System.Globalization;

namespace Blazeo;

/// <summary>
/// Date arithmetic for a calendar grid carried out in a culture's own calendar system - Gregorian for
/// most locales, but Persian (Jalali) for <c>fa-IR</c>, Hijri (Umm al-Qura) for <c>ar-SA</c>, and so
/// on. This is the .NET analogue of react-day-picker's <c>/persian</c> entry point: the selectable
/// value stays an absolute day (<see cref="DateOnly"/>), and every month/year decomposition routes
/// through <see cref="CultureInfo.DateTimeFormat"/>'s <see cref="Calendar"/> so the grid lays out that
/// locale's months and years. Day-of-week is universal (the same seven-day cycle), so it stays on
/// <see cref="DateOnly"/> and is read directly.
/// </summary>
public readonly struct CalendarSystem
{
    private readonly Calendar _calendar;

    /// <summary>Builds a system from the culture's formatting calendar (Gregorian, Persian, Hijri, ...).</summary>
    public CalendarSystem(CultureInfo culture) => _calendar = culture.DateTimeFormat.Calendar;

    private static DateTime ToDateTime(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);
    private static DateOnly ToDateOnly(DateTime dateTime) => DateOnly.FromDateTime(dateTime);

    /// <summary>The calendar-system year of <paramref name="date"/> (e.g. 1405 for a Persian locale).</summary>
    public int GetYear(DateOnly date) => _calendar.GetYear(ToDateTime(date));

    /// <summary>The calendar-system month of <paramref name="date"/> (1-based).</summary>
    public int GetMonth(DateOnly date) => _calendar.GetMonth(ToDateTime(date));

    /// <summary>The day-of-month in the calendar system (1-based).</summary>
    public int GetDayOfMonth(DateOnly date) => _calendar.GetDayOfMonth(ToDateTime(date));

    /// <summary>Number of months in the calendar-system year that contains <paramref name="date"/>.</summary>
    public int MonthsInYear(int year) => _calendar.GetMonthsInYear(year);

    /// <summary>The first day of the calendar-system month that contains <paramref name="date"/>.</summary>
    public DateOnly FirstOfMonth(DateOnly date)
    {
        var dateTime = ToDateTime(date);
        return ToDateOnly(_calendar.ToDateTime(_calendar.GetYear(dateTime), _calendar.GetMonth(dateTime), 1, 0, 0, 0, 0));
    }

    /// <summary>The last day of the calendar-system month that contains <paramref name="date"/>.</summary>
    public DateOnly LastOfMonth(DateOnly date)
    {
        var dateTime = ToDateTime(date);
        var year = _calendar.GetYear(dateTime);
        var month = _calendar.GetMonth(dateTime);
        return ToDateOnly(_calendar.ToDateTime(year, month, _calendar.GetDaysInMonth(year, month), 0, 0, 0, 0));
    }

    /// <summary>Adds <paramref name="months"/> calendar-system months (clamping the day to the new month length).</summary>
    public DateOnly AddMonths(DateOnly date, int months) => ToDateOnly(_calendar.AddMonths(ToDateTime(date), months));

    /// <summary>Adds <paramref name="years"/> calendar-system years.</summary>
    public DateOnly AddYears(DateOnly date, int years) => ToDateOnly(_calendar.AddYears(ToDateTime(date), years));

    /// <summary>Whether two days fall in the same calendar-system year and month.</summary>
    public bool SameMonth(DateOnly a, DateOnly b)
    {
        var da = ToDateTime(a);
        var db = ToDateTime(b);
        return _calendar.GetYear(da) == _calendar.GetYear(db) && _calendar.GetMonth(da) == _calendar.GetMonth(db);
    }

    /// <summary>The first day of the given calendar-system year and month (month/day clamped to valid ranges).</summary>
    public DateOnly FromYearMonth(int year, int month)
    {
        var months = _calendar.GetMonthsInYear(year);
        month = Math.Clamp(month, 1, months);
        return ToDateOnly(_calendar.ToDateTime(year, month, 1, 0, 0, 0, 0));
    }
}
