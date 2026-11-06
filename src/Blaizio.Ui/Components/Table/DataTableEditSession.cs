namespace Blaizio.Ui;

/// <summary>
/// The one open edit of a <see cref="BzDataTable{TItem}"/>: which row, which column (Cell mode) or
/// which field to focus (Row and Dialog modes), and the drafts .NET holds. Built-in cell and row
/// editors keep their text in the browser and send it with the commit; template and checkbox
/// editors, and every dialog field, keep theirs here.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
internal sealed class DataTableEditSession<TItem>(TItem item, DataTableEditMode mode, string? focusKey, string? seed)
{
    /// <summary>The row being edited. Re-pointed at the live instance when the rows re-render.</summary>
    public TItem Item { get; set; } = item;

    public DataTableEditMode Mode { get; } = mode;

    /// <summary>Cell mode: the edited column. Row and Dialog modes: the field that takes focus first.</summary>
    public string? FocusKey { get; } = focusKey;

    /// <summary>Quick entry: the typed text that replaces the <see cref="FocusKey"/> cell's value.</summary>
    public string? Seed { get; } = seed;

    /// <summary>Typed drafts of template and checkbox editors, by column key.</summary>
    public Dictionary<string, object?> Drafts { get; } = new(StringComparer.Ordinal);

    /// <summary>Text drafts of the built-in text boxes, by column key.</summary>
    public Dictionary<string, string?> Texts { get; } = new(StringComparer.Ordinal);

    /// <summary>How many times code set each column's draft (a custom dialog's SetValue): a built-in text box shows the new text when it moves.</summary>
    public Dictionary<string, int> Versions { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The text each built-in cell or row editor started from, fixed for the session: the row's value
    /// can change under an open editor (written by a commit, rolled back by a Reject), and the editor
    /// must keep what was typed rather than re-render to the row's value.
    /// </summary>
    public Dictionary<string, string> Seeds { get; } = new(StringComparer.Ordinal);

    /// <summary>Validation messages, by column key.</summary>
    public Dictionary<string, string> Errors { get; } = new(StringComparer.Ordinal);

    /// <summary>A message for the whole row (a rejected save).</summary>
    public string? Message { get; set; }

    /// <summary>Row and Dialog modes: the table is waiting on <c>OnEdit</c>.</summary>
    public bool Busy { get; set; }
}
