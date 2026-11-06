namespace Blaizio.Ui;

/// <summary>One changed cell: the row, the column it belongs to, and the value before and after.</summary>
/// <typeparam name="TItem">The row data type.</typeparam>
/// <param name="Item">The edited row.</param>
/// <param name="ColumnKey">The column's key (its <c>ColumnKey</c>, else its title or property name).</param>
/// <param name="OldValue">The value before the edit.</param>
/// <param name="NewValue">The value after the edit, already parsed to the column's type.</param>
public sealed record DataTableCellChange<TItem>(TItem Item, string ColumnKey, object? OldValue, object? NewValue);

/// <summary>
/// Raised by <see cref="BzDataTable{TItem}.OnEdit"/> once per user action: a committed cell, a
/// committed row, a toggled checkbox, a paste or a clear. Every changed cell is in
/// <see cref="Changes"/>, so a paste of fifty cells is one call.
/// <para>
/// When the column's property has a setter, the row already holds the new values; call
/// <see cref="Reject(string?)"/> (after a failed save, say) and the table puts the old values back,
/// marks the cells and announces the message. Records and <c>init</c> properties have no setter:
/// apply the change yourself (replace the row) before any <c>await</c>, so the table shows it at once.
/// </para>
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public sealed class DataTableEditEventArgs<TItem>
{
    private Dictionary<DataTableCellChange<TItem>, string>? _cellMessages;

    internal DataTableEditEventArgs(DataTableEditReason reason, IReadOnlyList<DataTableCellChange<TItem>> changes)
    {
        Reason = reason;
        Changes = changes;
    }

    /// <summary>What produced the changes.</summary>
    public DataTableEditReason Reason { get; }

    /// <summary>The changed cells, in row then column order.</summary>
    public IReadOnlyList<DataTableCellChange<TItem>> Changes { get; }

    /// <summary>Whether <see cref="Reject(string?)"/> was called.</summary>
    public bool Rejected { get; private set; }

    /// <summary>The message passed to <see cref="Reject(string?)"/>, if any.</summary>
    public string? Message { get; private set; }

    /// <summary>
    /// Refuses the whole change: the table restores the old values (where it wrote them), marks the
    /// cells invalid and announces <paramref name="message"/>. A row or dialog editor stays open.
    /// </summary>
    public void Reject(string? message = null)
    {
        Rejected = true;
        Message = message ?? Message;
    }

    /// <summary>
    /// Refuses the whole change and pins <paramref name="message"/> to one cell - a cross-field rule
    /// in a row or dialog editor ("End must be after Start") shows under that field.
    /// </summary>
    public void Reject(DataTableCellChange<TItem> change, string message)
    {
        ArgumentNullException.ThrowIfNull(change);
        Rejected = true;
        (_cellMessages ??= [])[change] = message;
        Message ??= message;
    }

    /// <summary>The message <see cref="Reject(DataTableCellChange{TItem}, string)"/> pinned to <paramref name="change"/>, if any.</summary>
    public string? MessageFor(DataTableCellChange<TItem> change) =>
        _cellMessages is not null && _cellMessages.TryGetValue(change, out var message) ? message : null;
}
