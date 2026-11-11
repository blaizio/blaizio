using System.Globalization;
using System.Text;

namespace Blaizio;

/// <summary>
/// Date arithmetic for a calendar grid carried out in a culture's own calendar system - Gregorian for
/// most locales, but Persian (Jalali) for <c>fa-IR</c>, Hijri (Umm al-Qura) for <c>ar-SA</c>, and so
/// on. One component, several calendars: the selectable
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

    /// <summary>
    /// The first day this calendar system can express. Gregorian starts at year 1; Umm al-Qura
    /// only at 30 April 1900. A day before it is read as this day, so the arithmetic below never
    /// throws on a date the calendar cannot name.
    /// </summary>
    public DateOnly MinDate => DateOnly.FromDateTime(_calendar.MinSupportedDateTime);

    /// <summary>The last day this calendar system can express (see <see cref="MinDate"/>).</summary>
    public DateOnly MaxDate => DateOnly.FromDateTime(_calendar.MaxSupportedDateTime);

    /// <summary>Whether the calendar system can express <paramref name="date"/>.</summary>
    public bool Supports(DateOnly date) => date >= MinDate && date <= MaxDate;

    // Every conversion goes through here, so a day outside the calendar's span (a Min or Value far
    // in the past under Umm al-Qura, say) lands on the nearest day it does have.
    private DateTime ToDateTime(DateOnly date)
    {
        var min = MinDate;
        var max = MaxDate;
        return (date < min ? min : date > max ? max : date).ToDateTime(TimeOnly.MinValue);
    }

    private static DateOnly ToDateOnly(DateTime dateTime) => DateOnly.FromDateTime(dateTime);

    private int ClampYear(int year) =>
        Math.Clamp(year, _calendar.GetYear(_calendar.MinSupportedDateTime), _calendar.GetYear(_calendar.MaxSupportedDateTime));

    /// <summary>The calendar-system year of <paramref name="date"/> (e.g. 1405 for a Persian locale).</summary>
    public int GetYear(DateOnly date) => _calendar.GetYear(ToDateTime(date));

    /// <summary>The calendar-system month of <paramref name="date"/> (1-based).</summary>
    public int GetMonth(DateOnly date) => _calendar.GetMonth(ToDateTime(date));

    /// <summary>The day-of-month in the calendar system (1-based).</summary>
    public int GetDayOfMonth(DateOnly date) => _calendar.GetDayOfMonth(ToDateTime(date));

    /// <summary>Number of months in the calendar-system year <paramref name="year"/> (a year outside the system's span counts as its nearest one).</summary>
    public int MonthsInYear(int year) => _calendar.GetMonthsInYear(ClampYear(year));

    /// <summary>
    /// Adds <paramref name="days"/> days, stopping at the system's first or last day instead of
    /// running past it (<see cref="DateOnly.AddDays"/> throws there).
    /// </summary>
    public DateOnly AddDays(DateOnly date, int days)
    {
        var number = (long)date.DayNumber + days;
        return DateOnly.FromDayNumber((int)Math.Clamp(number, MinDate.DayNumber, MaxDate.DayNumber));
    }

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

    /// <summary>
    /// Adds <paramref name="months"/> calendar-system months (clamping the day to the new month
    /// length). A result past either end of the system stops at that end.
    /// </summary>
    public DateOnly AddMonths(DateOnly date, int months)
    {
        try
        {
            return ToDateOnly(_calendar.AddMonths(ToDateTime(date), months));
        }
        catch (ArgumentException)
        {
            return months < 0 ? MinDate : MaxDate;
        }
    }

    /// <summary>Adds <paramref name="years"/> calendar-system years, stopping at either end of the system.</summary>
    public DateOnly AddYears(DateOnly date, int years)
    {
        try
        {
            return ToDateOnly(_calendar.AddYears(ToDateTime(date), years));
        }
        catch (ArgumentException)
        {
            return years < 0 ? MinDate : MaxDate;
        }
    }

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
        year = ClampYear(year);
        var months = _calendar.GetMonthsInYear(year);
        month = Math.Clamp(month, 1, months);
        try
        {
            return ToDateOnly(_calendar.ToDateTime(year, month, 1, 0, 0, 0, 0));
        }
        catch (ArgumentException)
        {
            // The system's first or last year need not hold every month (Umm al-Qura ends mid-year).
            return year <= _calendar.GetYear(_calendar.MinSupportedDateTime) ? MinDate : FirstOfMonth(MaxDate);
        }
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
