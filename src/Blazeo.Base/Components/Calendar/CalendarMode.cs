namespace Blazeo;

/// <summary>
/// How a <see cref="BaseCalendar"/> (and the styled BzCalendar) interprets day clicks.
/// </summary>
public enum CalendarMode
{
    /// <summary>One date at a time. Clicking another replaces it; clicking the selected one clears it.</summary>
    Single,

    /// <summary>Any number of independent dates, each toggled on or off by a click.</summary>
    Multiple,

    /// <summary>A contiguous <c>start..end</c> range, chosen in two clicks (the third starts over).</summary>
    Range,
}
