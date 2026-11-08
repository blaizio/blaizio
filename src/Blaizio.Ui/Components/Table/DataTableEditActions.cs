namespace Blaizio.Ui;

/// <summary>
/// What a <see cref="BzEditActionsColumn{TItem}"/>'s <c>ChildContent</c> receives for each row: the
/// row, whether it is being edited, and the actions that begin, save and cancel its edit. Render
/// your own buttons with it in place of the built-in Edit, Save and Cancel; they count as part of
/// the row's edit, as the built-in ones do.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public sealed class DataTableEditActions<TItem>
{
    private readonly BzDataTable<TItem> _table;

    internal DataTableEditActions(BzDataTable<TItem> table, TItem item)
    {
        _table = table;
        Item = item;
    }

    /// <summary>The row.</summary>
    public TItem Item { get; }

    /// <summary>How the table edits: Row edits in place (Save and Cancel belong in the row), Dialog and Drawer in a form.</summary>
    public DataTableEditMode Mode => _table.EditMode;

    /// <summary>An edit is open on this row: its editors (Row mode) or its form (Dialog and Drawer modes).</summary>
    public bool IsEditing => _table.IsEditing(Item);

    /// <summary>This row's save is running: <c>OnEdit</c> has not returned yet.</summary>
    public bool Busy => _table.IsBusy(Item);

    /// <summary>
    /// Opens the row's edit, as Enter or a double-click would: its editors in Row mode, focused on
    /// <paramref name="columnKey"/>'s field (by default the first editable column), or its form.
    /// </summary>
    public Task BeginAsync(string? columnKey = null) => _table.BeginEditAsync(Item, columnKey);

    /// <summary>
    /// Saves the row's open edit, as Enter (or the form's Save) would. Returns <see langword="false"/>
    /// when the row is not being edited, a value is invalid or the save was refused; the editors then
    /// stay open, showing why.
    /// </summary>
    public Task<bool> SaveAsync() => IsEditing ? _table.CommitEditAsync() : Task.FromResult(false);

    /// <summary>Closes the row's open edit without saving it, as Escape would.</summary>
    public Task CancelAsync() => IsEditing ? _table.CancelEditAsync() : Task.CompletedTask;
}
