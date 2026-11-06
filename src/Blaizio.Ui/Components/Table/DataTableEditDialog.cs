using Microsoft.AspNetCore.Components;

namespace Blaizio.Ui;

/// <summary>
/// What a <see cref="BzDataTable{TItem}"/>'s <c>EditDialogContent</c> receives in Dialog mode: the
/// row being edited, a draft value per editable column, the validation messages, and Save / Cancel.
/// Render your own dialog, sheet or drawer around it: bind its <c>Open</c> to <see cref="Open"/> and
/// its <c>OpenChanged</c> to <see cref="OpenChanged"/>, lay the fields out with <see cref="Field"/>
/// (the built-in field: label, editor and message) or with your own controls bound through
/// <see cref="GetValue{TValue}"/> and <see cref="SetValue{TValue}"/>, and save with
/// <see cref="SaveAsync"/>. Nothing reaches the row until a save passes.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public sealed class DataTableEditDialog<TItem>
{
    private readonly BzDataTable<TItem> _table;
    private readonly DataTableEditSession<TItem> _session;

    internal DataTableEditDialog(
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

    /// <summary>Whether the edit is open. Bind your dialog's <c>Open</c> to it; it turns false once a save passes or the edit is cancelled.</summary>
    public bool Open { get; }

    /// <summary>Bind your dialog's <c>OpenChanged</c> to it: a close (Escape, the close button, a press outside) drops the draft.</summary>
    public EventCallback<bool> OpenChanged { get; }

    /// <summary>The row being edited. After a close it stays the last row, while your dialog animates out.</summary>
    public TItem Item => _session.Item;

    /// <summary>The keys of the editable columns in column order, hidden columns included: one field each.</summary>
    public IReadOnlyList<string> ColumnKeys { get; }

    /// <summary>A save is running: <c>OnEdit</c> has not returned yet.</summary>
    public bool Busy => _session.Busy;

    /// <summary>Why the last save was refused (a <c>Reject</c> naming no column), or <see langword="null"/>.</summary>
    public string? Message => _session.Message;

    /// <summary>The validation message for a column's field, or <see langword="null"/> while it is valid.</summary>
    public string? ErrorFor(string columnKey) => _session.Errors.GetValueOrDefault(columnKey);

    /// <summary>A column's draft: the value entered so far, else the row's own value.</summary>
    /// <exception cref="ArgumentException">No editable column has that key.</exception>
    public TValue GetValue<TValue>(string columnKey) => _table.GetDialogValue<TValue>(_session, columnKey);

    /// <summary>Changes a column's draft. The row is not touched until <see cref="SaveAsync"/> passes.</summary>
    /// <exception cref="ArgumentException">No editable column has that key, or the value is not of the column's type.</exception>
    public void SetValue<TValue>(string columnKey, TValue value) => _table.SetDialogValue(_session, columnKey, value);

    /// <summary>The column's built-in field, as the default dialog lays it out: its label, its editor and its validation message.</summary>
    /// <exception cref="ArgumentException">No editable column has that key.</exception>
    public RenderFragment Field(string columnKey) => _table.DialogField(_session, columnKey);

    /// <summary>
    /// Validates and saves the drafts, as the default dialog's Save does: writes the row, raises
    /// <c>OnEdit</c> and closes. Returns <see langword="false"/> when a value is invalid or the save
    /// was refused; the edit then stays open, with the reasons in <see cref="ErrorFor"/> and
    /// <see cref="Message"/>.
    /// </summary>
    public Task<bool> SaveAsync() => _table.SaveDialogAsync(_session);

    /// <summary>Closes the edit without saving it.</summary>
    public Task CancelAsync() => _table.CancelDialogAsync(_session);
}
