namespace Blaizio.Ui;

/// <summary>
/// The context an editable column's <c>EditorContent</c> receives: the row, the draft value to bind
/// your control to, how the edit started, and Commit / Cancel. Bind with
/// <c>@bind-Value="context.Value"</c>; a pick-style control can commit as soon as it changes with
/// <c>@bind-Value:after="context.Commit"</c>. Spread <see cref="Attributes"/> onto the control so it
/// is labelled, linked to its error and marked invalid. Keep text boxes on the default change
/// binding: an <c>oninput</c> binding re-renders the page that owns the template on every keystroke.
/// To behave like the built-in editors, follow <see cref="StartedBy"/>: open your popup when
/// <see cref="OpenPopup"/> is true, and start from <see cref="TypedText"/> instead of the value when
/// the user typed over the cell.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
/// <typeparam name="TValue">The column's value type.</typeparam>
public sealed class DataTableCellEditor<TItem, TValue>
{
    private readonly Func<TValue> _read;
    private readonly Action<TValue> _write;
    private readonly Func<Task> _commit;
    private readonly Func<Task> _cancel;

    internal DataTableCellEditor(
        TItem item,
        string columnKey,
        string? title,
        string? error,
        bool inDialog,
        DataTableEditActivation activation,
        IReadOnlyDictionary<string, object> attributes,
        Func<TValue> read,
        Action<TValue> write,
        Func<Task> commit,
        Func<Task> cancel)
    {
        Item = item;
        ColumnKey = columnKey;
        Title = title;
        Error = error;
        InDialog = inDialog;
        StartedBy = activation.Start;
        TypedText = activation.TypedText;
        OpenPopup = activation.OpenPopup;
        Attributes = attributes;
        _read = read;
        _write = write;
        _commit = commit;
        _cancel = cancel;
    }

    /// <summary>The row being edited.</summary>
    public TItem Item { get; }

    /// <summary>The column's key.</summary>
    public string ColumnKey { get; }

    /// <summary>The column's title, for labels and placeholders.</summary>
    public string? Title { get; }

    /// <summary>The draft value. Starts as the row's current value; setting it changes only the draft until the edit commits.</summary>
    public TValue Value
    {
        get => _read();
        set => _write(value);
    }

    /// <summary>The validation message for this field, or <see langword="null"/> when it is valid.</summary>
    public string? Error { get; }

    /// <summary>True inside a form editor (the Dialog or Drawer mode's form field), false inside a table cell.</summary>
    public bool InDialog { get; }

    /// <summary>
    /// How the edit started: <see cref="DataTableEditStart.Typing"/> (a character typed over the
    /// cell: start from <see cref="TypedText"/>), <see cref="DataTableEditStart.Key"/> (F2, Space,
    /// Alt+ArrowDown), <see cref="DataTableEditStart.Pointer"/> (a double-click or a tap) or
    /// <see cref="DataTableEditStart.Code"/> (<c>BeginEditAsync</c>). In Row mode every editor of the
    /// row sees how the row's edit started.
    /// </summary>
    public DataTableEditStart StartedBy { get; }

    /// <summary>
    /// What the user typed over the cell to start the edit (an empty string after Backspace), or
    /// <see langword="null"/>. Only the editor the edit started in gets it.
    /// </summary>
    public string? TypedText { get; }

    /// <summary>
    /// The edit asks for this editor's popup open from the start (a list, a calendar): a
    /// double-click, Alt+ArrowDown, F2 or Space on a pick list, or a press on the cell's mark. Only
    /// the editor the edit started in gets it, and form fields never do.
    /// </summary>
    public bool OpenPopup { get; }

    /// <summary>Attributes for your control: its accessible name or field id, <c>aria-describedby</c> and <c>aria-invalid</c>. Spread them with <c>@attributes</c>.</summary>
    public IReadOnlyDictionary<string, object> Attributes { get; }

    /// <summary>
    /// Commits the edit. In Cell mode this ends the cell's edit; in Row, Dialog and Drawer modes the
    /// row commits as a whole (Enter, or the form's Save), so this only keeps the draft.
    /// </summary>
    public Task Commit() => _commit();

    /// <summary>Drops the draft and ends the edit (the whole row in Row, Dialog and Drawer modes).</summary>
    public Task Cancel() => _cancel();
}
