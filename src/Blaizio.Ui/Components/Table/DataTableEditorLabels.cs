namespace Blaizio.Ui;

/// <summary>
/// What a <see cref="BzDataTable{TItem}"/>'s built-in date and time editors say to screen readers,
/// and the time editor's AM and PM text: give one to the table's <c>EditorLabels</c> to word them
/// in the reader's language. Each property is the matching <see cref="BzInputDate"/> or
/// <see cref="BzInputTime"/> parameter without its suffix, and defaults to the same English.
/// Set only the ones you need: <c>new DataTableEditorLabels { Day = "...", Hour = "..." }</c>.
/// </summary>
public sealed record DataTableEditorLabels
{
    /// <summary>The labels the editors use while the table has none: English throughout.</summary>
    internal static readonly DataTableEditorLabels English = new();

    /// <summary>The date editor's day segment, for screen readers. Defaults to "Day".</summary>
    public string Day { get; init; } = "Day";

    /// <summary>The date editor's month segment, for screen readers. Defaults to "Month".</summary>
    public string Month { get; init; } = "Month";

    /// <summary>The date editor's year segment, for screen readers. Defaults to "Year".</summary>
    public string Year { get; init; } = "Year";

    /// <summary>The name of the date editor's button that opens its calendar, for screen readers. Defaults to "Open calendar".</summary>
    public string CalendarButton { get; init; } = "Open calendar";

    /// <summary>The date editor's calendar popup, for screen readers. Defaults to "Calendar".</summary>
    public string Calendar { get; init; } = "Calendar";

    /// <summary>The name of the calendar popup's previous-month button, for screen readers. Defaults to "Go to the previous month".</summary>
    public string PreviousMonth { get; init; } = "Go to the previous month";

    /// <summary>The name of the calendar popup's next-month button, for screen readers. Defaults to "Go to the next month".</summary>
    public string NextMonth { get; init; } = "Go to the next month";

    /// <summary>The time editor's hour segment, for screen readers. Defaults to "Hour".</summary>
    public string Hour { get; init; } = "Hour";

    /// <summary>The time editor's minute segment, for screen readers. Defaults to "Minute".</summary>
    public string Minute { get; init; } = "Minute";

    /// <summary>
    /// The name of the time editor's AM/PM toggle, for screen readers. The editor has one when the
    /// current culture writes times on a 12-hour clock. Keep the <see cref="Am"/> and
    /// <see cref="Pm"/> text in it, as the default does, so speech users can say what they see.
    /// Defaults to "AM or PM".
    /// </summary>
    public string Meridiem { get; init; } = "AM or PM";

    /// <summary>The AM/PM toggle's text before noon. Defaults to "AM".</summary>
    public string Am { get; init; } = "AM";

    /// <summary>The AM/PM toggle's text from noon on. Defaults to "PM".</summary>
    public string Pm { get; init; } = "PM";
}
