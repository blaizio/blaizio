using System.Globalization;
using System.Text;

namespace Blaizio;

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

    /// <summary>Number of months in the calendar-system year <paramref name="year"/>.</summary>
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

    /// <summary>The 1-based week-of-year for <paramref name="date"/> in the culture's calendar + week rule.</summary>
    public int WeekOfYear(DateOnly date, CultureInfo culture, DayOfWeek weekStart) =>
        _calendar.GetWeekOfYear(ToDateTime(date), culture.DateTimeFormat.CalendarWeekRule, weekStart);

    // Native month names for the calendars whose names Blazor WASM's reduced ICU romanizes (it ships
    // "Muharram"/"Tir" instead of محرم/تیر). The names are fixed, so we carry them and only fall back to
    // the culture for ordinary Gregorian locales (which WASM localizes correctly).
    private static readonly string[] PersianMonths =
        ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];

    private static readonly string[] HijriMonths =
        ["محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
         "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة"];

    /// <summary>The localized month name (1-based), in native script for Persian/Hijri calendars.</summary>
    public string MonthName(CultureInfo culture, int month)
    {
        if (month is >= 1 and <= 12)
        {
            if (_calendar is PersianCalendar) return PersianMonths[month - 1];
            if (_calendar is HijriCalendar or UmAlQuraCalendar) return HijriMonths[month - 1];
        }
        return culture.DateTimeFormat.GetMonthName(month);
    }

    /// <summary>
    /// Substitutes ASCII digits in <paramref name="value"/> with the culture's native digits (Persian
    /// ۰-۹, Arabic ٠-٩, ...). A no-op for cultures whose native digits are already Latin.
    /// </summary>
    public static string Digits(string value, CultureInfo culture)
    {
        var native = culture.NumberFormat.NativeDigits;
        if (native.Length < 10 || native[0] == "0") return value;

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
            builder.Append(ch is >= '0' and <= '9' ? native[ch - '0'] : ch.ToString());
        return builder.ToString();
    }

    /// <inheritdoc cref="Digits(string, CultureInfo)"/>
    public static string Digits(int value, CultureInfo culture) =>
        Digits(value.ToString(CultureInfo.InvariantCulture), culture);
}
