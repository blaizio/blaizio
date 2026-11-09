using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blaizio.Ui;

/// <summary>Where a built-in editor renders.</summary>
internal enum DataTableEditorPlacement
{
    /// <summary>Over one cell (Cell mode).</summary>
    Cell,

    /// <summary>Over one cell of an open row (Row mode).</summary>
    Row,

    /// <summary>As a field of the form editor (Dialog and Drawer modes).</summary>
    Form,
}

/// <summary>One option of a built-in pick list: the key the select submits, its label and the value it stands for.</summary>
internal readonly record struct DataTableEditorOption<TValue>(string Key, string Label, TValue Value);

/// <summary>What the table hands a column to render one built-in editor bound to the open edit.</summary>
internal sealed class DataTableEditorRequest
{
    /// <summary>The open edit. A different one seeds the control again.</summary>
    public required object Session { get; init; }

    /// <summary>The column's title, for accessible names.</summary>
    public required string Title { get; init; }

    public required DataTableEditorPlacement Placement { get; init; }

    /// <summary>The accessible name, or the field's wiring (id, aria-describedby, aria-invalid), for the control.</summary>
    public required IReadOnlyDictionary<string, object> Attributes { get; init; }

    /// <summary>The column's alignment: <c>start</c>, <c>center</c> or <c>end</c>.</summary>
    public string Align { get; init; } = "start";

    /// <summary>Form: this field takes the focus when the form opens.</summary>
    public bool Focus { get; init; }

    /// <summary>How the edit started, as this editor sees it (the typed text and the popup apply to the editor the edit started in).</summary>
    public DataTableEditActivation Activation { get; init; }

    /// <summary>Counts the times code set this column's draft: a text box takes its text again when it moves.</summary>
    public int Version { get; init; }

    /// <summary>The table's <c>EditorLabels</c>: the date and time editors' words, English while null.</summary>
    public DataTableEditorLabels? Labels { get; init; }

    /// <summary>The draft value, or the row's value while there is none.</summary>
    public required Func<object?> ReadValue { get; init; }

    /// <summary>Stores a new draft value. Renders nothing.</summary>
    public required Action<object?> WriteValue { get; init; }

    /// <summary>A text box's starting text, fixed for the whole edit.</summary>
    public required Func<string?> ReadText { get; init; }

    /// <summary>Stores a text box's text. Renders nothing.</summary>
    public required Action<string?> WriteText { get; init; }

    /// <summary>Commits the cell. Cell mode only: a pick from a list ends the edit.</summary>
    public required Func<Task> Commit { get; init; }
}

/// <summary>
/// One built-in editor of an editable column: a BzInputText, BzInputNumber, BzSelect, BzCheckbox,
/// BzSwitch, BzInputDate or BzInputTime bound to the open edit's draft. It holds the control's value
/// itself, so a change re-renders this editor alone and never the table (typing in a form stays
/// off the table body). A text box keeps the text it started from for the whole edit: what was typed
/// lives in the browser, which sends it with the commit (cells and rows), and in the session's text
/// draft (a form), so no render can put the old text back under the caret.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
/// <typeparam name="TValue">The column's value type.</typeparam>
internal sealed class DataTableEditorHost<TItem, TValue> : ComponentBase, IHandleEvent
{
    private static readonly Type s_type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);

    [Parameter, EditorRequired] public DataTableEditorRequest Request { get; set; } = default!;

    /// <summary>The resolved control (never <see cref="DataTableEditor.Auto"/>).</summary>
    [Parameter] public DataTableEditor Kind { get; set; }

    /// <summary>The pick list's options (<see cref="DataTableEditor.Select"/>).</summary>
    [Parameter] public IReadOnlyList<DataTableEditorOption<TValue>> Options { get; set; } = [];

    /// <summary>The <c>inputmode</c> hint for the text box.</summary>
    [Parameter] public string? InputMode { get; set; }

    /// <summary>The label of a pick list's blank option (the column's <c>NoneLabel</c>).</summary>
    [Parameter] public string NoneLabel { get; set; } = "None";

    private object? _session;
    private int _version;
    private TValue _value = default!;
    private string? _text;
    private DateOnly? _date;
    private TimeOnly? _time;
    private TimeOnly _timeOfDay;
    // A date's calendar popup: open from the start when the edit asks for it; a day picked while it
    // is open commits a cell.
    private bool _calendarOpen;

    // Over a cell, a control fills the editor rather than keeping its form height. Utility classes,
    // so they win over the styles' sizes (which sit in the components layer) and merge with the
    // control's own (a select trigger's w-fit, a number field's h-9). A date or time field never
    // squeezes below its natural width (in a narrow column the grid script takes the date's calendar
    // button away first).
    private const string Fill = "h-full min-h-0 w-full";
    private const string FillWide = "h-full min-h-0 w-max min-w-full shrink-0 gap-1 px-2";

    private bool InForm => Request.Placement == DataTableEditorPlacement.Form;

    // The date and time editors' words: the table's EditorLabels, English while it has none.
    private DataTableEditorLabels Labels => Request.Labels ?? DataTableEditorLabels.English;

    // A text box takes its text once per edit (and again only when code sets the draft); every
    // other control shows the draft as it stands, so a value set from code shows up too.
    protected override void OnParametersSet()
    {
        var fresh = !ReferenceEquals(_session, Request.Session);
        if (fresh || Request.Version != _version)
        {
            _session = Request.Session;
            _version = Request.Version;
            _text = Request.ReadText();
        }
        var draft = Request.ReadValue();
        _value = draft is TValue value ? value : default!;
        if (fresh)
        {
            if (_value is DateTime start) _timeOfDay = TimeOnly.FromDateTime(start);
            _calendarOpen = Kind == DataTableEditor.Date && Request.Activation.OpenPopup;
            // Typed over (or cleared with Backspace): a date or time field starts empty, and the
            // script types the key into its first segment.
            if (Request.Activation.TypedText is not null && Kind is DataTableEditor.Date or DataTableEditor.Time)
            {
                Request.WriteValue(null);
                draft = null;
                _value = default!;
            }
        }
        // A half-typed date or time is a null draft: the field keeps its segments meanwhile.
        _date = draft switch
        {
            DateOnly date => date,
            DateTime stamp => DateOnly.FromDateTime(stamp),
            _ => null,
        };
        _time = draft is TimeOnly time ? time : null;
    }

    // A controlled control shows _value and needs this editor rendered again after a change; a text
    // box (and an uncontrolled number field) holds its own text, so a keystroke renders nothing.
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        var task = callback.InvokeAsync(arg);
        if (task.IsCompletedSuccessfully)
        {
            RenderIfControlled();
            return Task.CompletedTask;
        }
        return AwaitThenRender(task);

        async Task AwaitThenRender(Task pending)
        {
            await pending;
            RenderIfControlled();
        }
    }

    private void RenderIfControlled()
    {
        if (Kind != DataTableEditor.Text) StateHasChanged();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        switch (Kind)
        {
            case DataTableEditor.Number:
                BuildNumber(builder);
                break;
            case DataTableEditor.Select:
                BuildSelect(builder);
                break;
            case DataTableEditor.Checkbox:
                builder.OpenComponent<BzCheckbox>(0);
                builder.AddComponentParameter(1, nameof(BzCheckbox.Checked), ToChecked(_value));
                builder.AddComponentParameter(2, nameof(BzCheckbox.CheckedChanged),
                    EventCallback.Factory.Create<CheckedState>(this, state => SetValue(FromBool(state == CheckedState.Checked))));
                builder.AddMultipleAttributes(3, ControlAttributes());
                builder.CloseComponent();
                break;
            case DataTableEditor.Switch:
                builder.OpenComponent<BzSwitch>(0);
                builder.AddComponentParameter(1, nameof(BzSwitch.Checked), _value is true);
                builder.AddComponentParameter(2, nameof(BzSwitch.CheckedChanged),
                    EventCallback.Factory.Create<bool>(this, on => SetValue(FromBool(on))));
                builder.AddMultipleAttributes(3, ControlAttributes());
                builder.CloseComponent();
                break;
            case DataTableEditor.Date:
                builder.OpenComponent<BzInputDate>(0);
                builder.AddComponentParameter(1, nameof(BzInputDate.Value), _date);
                builder.AddComponentParameter(2, nameof(BzInputDate.ValueChanged),
                    EventCallback.Factory.Create<DateOnly?>(this, OnDate));
                builder.AddComponentParameter(3, nameof(BzInputDate.Format), DateSegments(CultureInfo.CurrentCulture));
                builder.AddComponentParameter(4, nameof(BzInputDate.ShowCalendar), true);
                builder.AddComponentParameter(5, nameof(BzInputDate.ShowIcon), false);
                builder.AddComponentParameter(6, nameof(BzInputDate.AutoFocus), InForm && Request.Focus);
                builder.AddComponentParameter(7, nameof(BzInputDate.Class), InForm ? "w-full" : FillWide);
                builder.AddComponentParameter(8, nameof(BzInputDate.CalendarOpen), _calendarOpen);
                builder.AddComponentParameter(9, nameof(BzInputDate.CalendarOpenChanged),
                    EventCallback.Factory.Create<bool>(this, open => _calendarOpen = open));
                builder.AddComponentParameter(10, nameof(BzInputDate.DayAriaLabel), Labels.Day);
                builder.AddComponentParameter(11, nameof(BzInputDate.MonthAriaLabel), Labels.Month);
                builder.AddComponentParameter(12, nameof(BzInputDate.YearAriaLabel), Labels.Year);
                builder.AddComponentParameter(13, nameof(BzInputDate.CalendarButtonAriaLabel), Labels.CalendarButton);
                builder.AddComponentParameter(14, nameof(BzInputDate.CalendarAriaLabel), Labels.Calendar);
                builder.AddComponentParameter(15, nameof(BzInputDate.PreviousMonthAriaLabel), Labels.PreviousMonth);
                builder.AddComponentParameter(16, nameof(BzInputDate.NextMonthAriaLabel), Labels.NextMonth);
                builder.AddMultipleAttributes(17, ControlAttributes());
                builder.CloseComponent();
                break;
            case DataTableEditor.Time:
                builder.OpenComponent<BzInputTime>(0);
                builder.AddComponentParameter(1, nameof(BzInputTime.Value), _time);
                builder.AddComponentParameter(2, nameof(BzInputTime.ValueChanged),
                    EventCallback.Factory.Create<TimeOnly?>(this, OnTime));
                // The segmented face (not the native time input): typed digits land in a segment.
                builder.AddComponentParameter(3, nameof(BzInputTime.HourCycle), HourCycle(CultureInfo.CurrentCulture));
                builder.AddComponentParameter(4, nameof(BzInputTime.ShowIcon), false);
                builder.AddComponentParameter(5, nameof(BzInputTime.AutoFocus), InForm && Request.Focus);
                builder.AddComponentParameter(6, nameof(BzInputTime.Class), InForm ? "w-full" : FillWide);
                builder.AddComponentParameter(7, nameof(BzInputTime.HourAriaLabel), Labels.Hour);
                builder.AddComponentParameter(8, nameof(BzInputTime.MinuteAriaLabel), Labels.Minute);
                builder.AddComponentParameter(9, nameof(BzInputTime.MeridiemAriaLabel), Labels.Meridiem);
                builder.AddComponentParameter(10, nameof(BzInputTime.AmLabel), Labels.Am);
                builder.AddComponentParameter(11, nameof(BzInputTime.PmLabel), Labels.Pm);
                builder.AddMultipleAttributes(12, ControlAttributes());
                builder.CloseComponent();
                break;
            default:
                BuildText(builder);
                break;
        }
    }

    private void BuildText(RenderTreeBuilder builder)
    {
        builder.OpenComponent<BzInputText>(0);
        builder.AddComponentParameter(1, nameof(BzInputText.Value), _text);
        builder.AddComponentParameter(2, nameof(BzInputText.ValueChanged),
            EventCallback.Factory.Create<string?>(this, text => Request.WriteText(text)));
        if (InForm && Request.Focus)
        {
            builder.AddComponentParameter(3, nameof(BzInputText.AutoFocus), true);
            builder.AddComponentParameter(4, nameof(BzInputText.AutoSelect), true);
        }
        if (!InForm) builder.AddComponentParameter(5, nameof(BzInputText.Class), Fill + (AlignClass is { } align ? " " + align : ""));
        builder.AddMultipleAttributes(6, ControlAttributes(textBox: true));
        builder.CloseComponent();
    }

    // Cells and rows: uncontrolled, the browser sends the field's text with the commit (parsed the way a
    // paste is). The dialog has no script reading it, so there the field is controlled by the draft.
    private void BuildNumber(RenderTreeBuilder builder)
    {
        builder.OpenComponent<BzInputNumber<TValue>>(0);
        if (InForm)
        {
            builder.AddComponentParameter(1, nameof(BzInputNumber<TValue>.Value), _value);
            builder.AddComponentParameter(2, nameof(BzInputNumber<TValue>.ValueChanged),
                EventCallback.Factory.Create<TValue>(this, SetValue));
            builder.AddComponentParameter(3, nameof(BzInputNumber<TValue>.AutoFocus), Request.Focus);
            builder.AddComponentParameter(4, nameof(BzInputNumber<TValue>.AutoSelect), Request.Focus);
        }
        else
        {
            builder.AddComponentParameter(5, nameof(BzInputNumber<TValue>.DefaultValue), _value);
            builder.AddComponentParameter(6, nameof(BzInputNumber<TValue>.Buttons), false);
            builder.AddComponentParameter(7, nameof(BzInputNumber<TValue>.Class), Fill);
            builder.AddComponentParameter(8, nameof(BzInputNumber<TValue>.FieldClass), AlignClass ?? "text-start");
        }
        builder.AddMultipleAttributes(9, ControlAttributes());
        builder.CloseComponent();
    }

    private void BuildSelect(RenderTreeBuilder builder)
    {
        string? selected = null;
        foreach (var option in Options)
        {
            if (!EqualityComparer<TValue>.Default.Equals(option.Value, _value)) continue;
            selected = option.Key;
            break;
        }

        builder.OpenComponent<BzSelect>(0);
        builder.AddComponentParameter(1, nameof(BzSelect.Value), selected);
        builder.AddComponentParameter(2, nameof(BzSelect.ValueChanged), EventCallback.Factory.Create<string?>(this, OnPickAsync));
        // Opened by a double-click, F2, Space, Alt+ArrowDown or typing: the list is open from the
        // start, on the current option (the editor is new for each edit, so this applies once).
        if (Request.Activation.OpenPopup) builder.AddComponentParameter(3, nameof(BzSelect.DefaultOpen), true);
        builder.AddComponentParameter(4, nameof(BzSelect.ChildContent), (RenderFragment)(select =>
        {
            select.OpenComponent<BzSelectTrigger>(0);
            select.AddComponentParameter(1, nameof(BzSelectTrigger.Class), InForm ? "w-full" : Fill);
            select.AddMultipleAttributes(3, ControlAttributes());
            select.AddComponentParameter(4, nameof(BzSelectTrigger.ChildContent), (RenderFragment)(trigger =>
            {
                trigger.OpenComponent<BzSelectValue>(0);
                trigger.CloseComponent();
            }));
            select.CloseComponent();

            select.OpenComponent<BzSelectContent>(5);
            select.AddComponentParameter(6, nameof(BzSelectContent.ChildContent), (RenderFragment)(list =>
            {
                foreach (var option in Options)
                {
                    list.OpenComponent<BzSelectItem>(0);
                    list.SetKey(option.Key);
                    list.AddComponentParameter(1, nameof(BzSelectItem.Value), option.Key);
                    list.AddComponentParameter(2, nameof(BzSelectItem.ChildContent), (RenderFragment)(item =>
                        item.AddContent(0, option.Label.Length == 0 ? NoneLabel : option.Label)));
                    list.CloseComponent();
                }
            }));
            select.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private void SetValue(TValue value)
    {
        _value = value;
        Request.WriteValue(value);
    }

    // A pick in a cell ends the cell's edit, as committing a typed value would; in a row or a form
    // it changes the draft only.
    private async Task OnPickAsync(string? key)
    {
        foreach (var option in Options)
        {
            if (option.Key != key) continue;
            SetValue(option.Value);
            if (Request.Placement == DataTableEditorPlacement.Cell) await Request.Commit();
            return;
        }
    }

    // An incomplete date (a segment still empty) is no value: the draft turns null, which a column
    // that needs a date refuses at the commit, while the field keeps the segments typed so far. A day
    // picked in the calendar ends a cell's edit, as a pick from a list does.
    private async Task OnDate(DateOnly? date)
    {
        _date = date;
        if (date is not { } day)
        {
            Request.WriteValue(null);
            return;
        }
        if (s_type == typeof(DateTime)) SetValue((TValue)(object)day.ToDateTime(_timeOfDay));
        else SetValue((TValue)(object)day);
        if (_calendarOpen && Request.Placement == DataTableEditorPlacement.Cell) await Request.Commit();
    }

    private void OnTime(TimeOnly? time)
    {
        _time = time;
        if (time is { } value) SetValue((TValue)(object)value);
        else Request.WriteValue(null);
    }

    private static TValue FromBool(bool value) => (TValue)(object)value;

    private static CheckedState ToChecked(TValue value) => value switch
    {
        true => CheckedState.Checked,
        false => CheckedState.Unchecked,
        _ => CheckedState.Indeterminate,
    };

    private string? AlignClass => Request.Align switch
    {
        "end" => "text-end",
        "center" => "text-center",
        _ => null,
    };

    private IReadOnlyDictionary<string, object> ControlAttributes(bool textBox = false)
    {
        var attributes = new Dictionary<string, object>(Request.Attributes);
        // The dialog's first field: controls with no AutoFocus of their own are marked for it.
        if (InForm && Request.Focus && Kind is DataTableEditor.Select or DataTableEditor.Checkbox or DataTableEditor.Switch)
            attributes["data-bz-autofocus"] = "";
        if (textBox)
        {
            attributes["autocomplete"] = "off";
            attributes["spellcheck"] = "false";
            if (InputMode is not null) attributes["inputmode"] = InputMode;
        }
        return attributes;
    }

    // The culture's short date as BzInputDate segments: each run of d, M or y becomes dd, MM or yyyy,
    // separators kept ("M/d/yyyy" becomes "MM/dd/yyyy"). Anything it cannot read falls back to ISO.
    internal static string DateSegments(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        var format = new StringBuilder();
        var parts = 0;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c is 'd' or 'M' or 'y')
            {
                var run = i;
                while (i + 1 < pattern.Length && pattern[i + 1] == c) i++;
                // dddd is a weekday name, not a day number.
                if (c == 'd' && i - run >= 2) return "yyyy-MM-dd";
                format.Append(c switch { 'd' => "dd", 'M' => "MM", _ => "yyyy" });
                parts++;
            }
            else if (!char.IsLetter(c) && c != '\'')
            {
                format.Append(c);
            }
        }
        var text = format.ToString().Trim();
        return parts == 3 ? text : "yyyy-MM-dd";
    }

    private static InputTimeHourCycle HourCycle(CultureInfo culture) =>
        culture.DateTimeFormat.ShortTimePattern.Contains('h') ? InputTimeHourCycle.TwelveHour : InputTimeHourCycle.TwentyFourHour;
}
