namespace Blaizio.Ui;

/// <summary>
/// The context an editable column's <c>EditorContent</c> receives: the row, the draft value to bind
/// your control to, and Commit / Cancel. Bind with <c>@bind-Value="context.Value"</c>; a pick-style
/// control can commit as soon as it changes with <c>@bind-Value:after="context.Commit"</c>. Spread
/// <see cref="Attributes"/> onto the control so it is labelled, linked to its error and marked
/// invalid. Keep text boxes on the default change binding: an <c>oninput</c> binding re-renders the
/// page that owns the template on every keystroke.
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

    /// <summary>True inside the dialog editor (a form field), false inside a table cell.</summary>
    public bool InDialog { get; }

    /// <summary>Attributes for your control: its accessible name or field id, <c>aria-describedby</c> and <c>aria-invalid</c>. Spread them with <c>@attributes</c>.</summary>
    public IReadOnlyDictionary<string, object> Attributes { get; }

    /// <summary>
    /// Commits the edit. In Cell mode this ends the cell's edit; in Row and Dialog modes the row
    /// commits as a whole (Enter, or the dialog's Save), so this only keeps the draft.
    /// </summary>
    public Task Commit() => _commit();

    /// <summary>Drops the draft and ends the edit (the whole row in Row and Dialog modes).</summary>
    public Task Cancel() => _cancel();
}
