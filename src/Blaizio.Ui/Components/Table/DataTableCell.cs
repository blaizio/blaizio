namespace Blaizio.Ui;

/// <summary>
/// The state of one cell as the table knows it in .NET, handed to a column's <c>CellContent</c>.
/// The cursor is not part of it: it lives in the browser (moving it never calls .NET), and the cell
/// shows it to CSS instead (<c>data-active</c> on the cell, styled through <c>group/table-cell</c>).
/// </summary>
/// <param name="IsEditing">An edit is open on the cell's row.</param>
/// <param name="Error">Why the cell's last value was refused, or <see langword="null"/>.</param>
/// <param name="Pending">A save of the cell's value is still running.</param>
internal readonly record struct DataTableCellState(bool IsEditing, string? Error, bool Pending);

/// <summary>
/// What a <see cref="BzTemplateColumn{TItem}"/>'s <c>CellContent</c> receives: the row and the
/// cell's state. A cell of an editable table also carries <c>data-active</c> while it holds the
/// keyboard cursor and the class <c>group/table-cell</c>, so a template can follow the cursor in
/// CSS alone, for example <c>group-data-active/table-cell:underline</c>.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public class DataTableCell<TItem>
{
    internal DataTableCell(TItem item, DataTableCellState state)
    {
        Item = item;
        IsEditing = state.IsEditing;
        Error = state.Error;
        Pending = state.Pending;
    }

    /// <summary>The row.</summary>
    public TItem Item { get; }

    /// <summary>
    /// An edit is open on the cell's row: one of its cells (Cell mode), the whole row (Row mode) or
    /// its form (Dialog and Drawer modes).
    /// </summary>
    public bool IsEditing { get; }

    /// <summary>Why the cell's last value was refused (a failed paste, a rejected save), or <see langword="null"/>.</summary>
    public string? Error { get; }

    /// <summary>A save of the cell's value is still running: <c>OnEdit</c> has not returned yet.</summary>
    public bool Pending { get; }
}

/// <summary>
/// What a <see cref="BzPropertyColumn{TItem, TProp}"/>'s <c>CellContent</c> receives: the row, the
/// property's value (and its text as the cell shows it), and the cell's state.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
/// <typeparam name="TValue">The property's type.</typeparam>
public sealed class DataTableCell<TItem, TValue> : DataTableCell<TItem>
{
    private readonly Func<TValue, string> _format;

    internal DataTableCell(TItem item, DataTableCellState state, TValue value, Func<TValue, string> format)
        : base(item, state)
    {
        Value = value;
        _format = format;
    }

    /// <summary>The property's value for this row.</summary>
    public TValue Value { get; }

    /// <summary>The value as the column shows it by default: its <c>Format</c> applied in the current culture.</summary>
    public string Text => _format(Value);
}
