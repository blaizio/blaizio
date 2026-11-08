using Microsoft.AspNetCore.Components;

namespace Blaizio.Ui;

/// <summary>
/// What a <see cref="BzDataTable{TItem}"/>'s <c>EditDialogContent</c> (Dialog mode) or
/// <c>EditDrawerContent</c> (Drawer mode) receives: the row being edited, a draft value per editable
/// column, the validation messages, and Save / Cancel. Render your own dialog, sheet or drawer around
/// it: bind its <c>Open</c> to <see cref="Open"/> and its <c>OpenChanged</c> to
/// <see cref="OpenChanged"/>, lay the fields out with <see cref="Field"/> (the built-in field: label,
/// editor and message) or with your own controls bound through <see cref="GetValue{TValue}"/> and
/// <see cref="SetValue{TValue}"/>, and save with <see cref="SaveAsync"/>. Nothing reaches the row
/// until a save passes. A close asked while the form has changes waits for an answer: render
/// Discard and Keep editing while <see cref="ConfirmingDiscard"/> is true.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public sealed class DataTableEditForm<TItem>
{
    private readonly BzDataTable<TItem> _table;
    private readonly DataTableEditSession<TItem> _session;

    internal DataTableEditForm(
        BzDataTable<TItem> table,
        DataTableEditSession<TItem> session,
        bool open,
        IReadOnlyList<string> columnKeys,
        EventCallback<bool> openChanged)
    {
        _table = table;
        _session = session;
        Open = open;
        ColumnKeys = columnKeys;
        OpenChanged = openChanged;
    }

    /// <summary>Whether the edit is open. Bind your surface's <c>Open</c> to it; it turns false once a save passes or the edit is closed.</summary>
    public bool Open { get; }

    /// <summary>
    /// Bind your surface's <c>OpenChanged</c> to it: a close (Escape, the close button, a press
    /// outside) drops the draft, or, when the form has changes, asks first
    /// (<see cref="ConfirmingDiscard"/>) and keeps the surface open.
    /// </summary>
    public EventCallback<bool> OpenChanged { get; }

    /// <summary>The row being edited. After a close it stays the last row, while your surface animates out.</summary>
    public TItem Item => _session.Item;

    /// <summary>The keys of the editable columns in column order, hidden columns included: one field each.</summary>
    public IReadOnlyList<string> ColumnKeys { get; }

    /// <summary>A save is running: <c>OnEdit</c> has not returned yet.</summary>
    public bool Busy => _session.Busy;

    /// <summary>Why the last save was refused (a <c>Reject</c> naming no column), or <see langword="null"/>.</summary>
    public string? Message => _session.Message;

    /// <summary>Whether any field holds a value other than the row's own.</summary>
    public bool HasChanges => _table.HasChanges(_session);

    /// <summary>
    /// A close was asked while the form has changes. Show the question in place of your footer's
    /// buttons, with <see cref="DiscardAsync"/> and <see cref="KeepEditing"/> as the answers. Another
    /// close request (a second Escape) answers Keep editing.
    /// </summary>
    public bool ConfirmingDiscard => _session.ConfirmingDiscard;

    /// <summary>Whether a row comes before this one in the table's current order (its sort and search, among the rows it has loaded).</summary>
    public bool HasPrevious => _table.HasNeighbour(_session, -1);

    /// <summary>Whether a row comes after this one in the table's current order (its sort and search, among the rows it has loaded).</summary>
    public bool HasNext => _table.HasNeighbour(_session, +1);

    /// <summary>The validation message for a column's field, or <see langword="null"/> while it is valid.</summary>
    public string? ErrorFor(string columnKey) => _session.Errors.GetValueOrDefault(columnKey);

    /// <summary>A column's draft: the value entered so far, else the row's own value.</summary>
    /// <exception cref="ArgumentException">No editable column has that key.</exception>
    public TValue GetValue<TValue>(string columnKey) => _table.GetFormValue<TValue>(_session, columnKey);

    /// <summary>Changes a column's draft. The row is not touched until <see cref="SaveAsync"/> passes.</summary>
    /// <exception cref="ArgumentException">No editable column has that key, or the value is not of the column's type.</exception>
    public void SetValue<TValue>(string columnKey, TValue value) => _table.SetFormValue(_session, columnKey, value);

    /// <summary>
    /// The built-in footer's buttons: Cancel and Save, or, while <see cref="ConfirmingDiscard"/>, the
    /// question with Keep editing and Discard. Place it inside a form that saves on submit
    /// (<c>@onsubmit="context.SaveAsync"</c>): Save is that form's submit button.
    /// </summary>
    public RenderFragment Actions => _table.FormActions(_session);

    /// <summary>The column's built-in field, as the built-in form lays it out: its label, its editor and its validation message.</summary>
    /// <exception cref="ArgumentException">No editable column has that key.</exception>
    public RenderFragment Field(string columnKey) => _table.FormField(_session, columnKey);

    /// <summary>
    /// Validates and saves the drafts, as the built-in form's Save does: writes the row, raises
    /// <c>OnEdit</c> and closes. Returns <see langword="false"/> when a value is invalid or the save
    /// was refused; the edit then stays open, with the reasons in <see cref="ErrorFor"/> and
    /// <see cref="Message"/>.
    /// </summary>
    public Task<bool> SaveAsync() => _table.SaveFormAsync(_session);

    /// <summary>
    /// Saves, then opens the previous row in the same form, as the built-in drawer's Previous
    /// button does. Returns <see langword="false"/> when a value is invalid, the save was refused or
    /// no row comes before this one.
    /// </summary>
    public Task<bool> PreviousAsync() => _table.MoveFormAsync(_session, -1);

    /// <summary>Saves, then opens the next row in the same form, as the built-in drawer's Next button does. See <see cref="PreviousAsync"/>.</summary>
    public Task<bool> NextAsync() => _table.MoveFormAsync(_session, +1);

    /// <summary>
    /// Closes the edit without saving it. When the form has changes it asks first instead
    /// (<see cref="ConfirmingDiscard"/> turns true) and stays open.
    /// </summary>
    public Task CancelAsync() => _table.CloseFormAsync(_session, ask: true);

    /// <summary>Closes the edit without saving it, and without asking.</summary>
    public Task DiscardAsync() => _table.CloseFormAsync(_session, ask: false);

    /// <summary>Answers Keep editing: the question goes away and the form stays as it was.</summary>
    public void KeepEditing() => _table.KeepEditing(_session);
}
