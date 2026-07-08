namespace Blaizio;

/// <summary>App-wide calendar rendering defaults.</summary>
public sealed class CalendarDefaults
{
    /// <summary>First day of the week. <see langword="null"/> (the default) follows the culture.</summary>
    public DayOfWeek? WeekStartsOn { get; set; }

    /// <summary>Render day/year numbers with the culture's native digits. Defaults to <see langword="true"/>.</summary>
    public bool NativeDigits { get; set; } = true;
}
