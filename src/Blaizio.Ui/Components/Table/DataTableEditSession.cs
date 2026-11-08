namespace Blaizio.Ui;

/// <summary>How an edit started, as one editor sees it: the typed text and the popup apply only to the editor the edit started in.</summary>
internal readonly record struct DataTableEditActivation(DataTableEditStart Start, string? TypedText, bool OpenPopup)
{
    /// <summary>An editor the edit did not start in (a row's other cells, the form's other fields).</summary>
    public DataTableEditActivation Elsewhere => new(Start, null, false);
}

/// <summary>
/// The one open edit of a <see cref="BzDataTable{TItem}"/>: which row, which column (Cell mode) or
/// which field to focus (Row, Dialog and Drawer modes), how it started, and the drafts .NET holds.
/// Built-in cell and row editors keep their text in the browser and send it with the commit;
/// template and checkbox editors, and every form field, keep theirs here.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
internal sealed class DataTableEditSession<TItem>(TItem item, DataTableEditMode mode, string? focusKey, DataTableEditActivation activation)
{
    /// <summary>The row being edited. Re-pointed at the live instance when the rows re-render.</summary>
    public TItem Item { get; set; } = item;

    public DataTableEditMode Mode { get; } = mode;

    /// <summary>A form mode: Dialog or Drawer.</summary>
    public bool InForm => Mode is DataTableEditMode.Dialog or DataTableEditMode.Drawer;

    /// <summary>Cell mode: the edited column. The other modes: the field that takes focus first.</summary>
    public string? FocusKey { get; } = focusKey;

    /// <summary>How the edit started, for the <see cref="FocusKey"/> editor.</summary>
    public DataTableEditActivation Activation { get; } = activation;

    /// <summary>Quick entry: the typed text that replaces the <see cref="FocusKey"/> cell's value.</summary>
    public string? Seed => Activation.TypedText;

    /// <summary>Typed drafts of template and checkbox editors, by column key.</summary>
    public Dictionary<string, object?> Drafts { get; } = new(StringComparer.Ordinal);

    /// <summary>Text drafts of the built-in text boxes, by column key.</summary>
    public Dictionary<string, string?> Texts { get; } = new(StringComparer.Ordinal);

    /// <summary>How many times code set each column's draft (a custom form's SetValue): a built-in text box shows the new text when it moves.</summary>
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

    /// <summary>Row and form modes: the table is waiting on <c>OnEdit</c>.</summary>
    public bool Busy { get; set; }

    /// <summary>A form with changes was asked to close: its footer asks Discard or Keep editing.</summary>
    public bool ConfirmingDiscard { get; set; }

    /// <summary>Opened by the form's Previous / Next: no field takes the focus, which stays on the button.</summary>
    public bool Moved { get; set; }

    /// <summary>Row mode, opened from code with a column key: the <see cref="FocusKey"/> editor takes the focus (the grid script looks for <c>data-bz-focus</c>).</summary>
    public bool FocusRequested { get; set; }

    /// <summary>The activation one editor sees: the full one in the column the edit started in.</summary>
    public DataTableEditActivation ActivationFor(string key) =>
        key == FocusKey ? Activation : Activation.Elsewhere;
}
