namespace Blaizio.Ui;

/// <summary>How an <see cref="BzInputTime"/> presents the hour.</summary>
public enum InputTimeHourCycle
{
    /// <summary>
    /// The browser's own convention (a native time input): an en-US browser shows AM/PM, a de-DE one a
    /// 24-hour clock. The default.
    /// </summary>
    Auto,

    /// <summary>Always 12-hour with an AM/PM segment, whatever the browser locale.</summary>
    TwelveHour,

    /// <summary>Always 24-hour (00-23), whatever the browser locale.</summary>
    TwentyFourHour,
}
