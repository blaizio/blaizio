namespace Blazeo.Ui;

/// <summary>Which way a <see cref="DataTable{TItem}"/> column is sorted.</summary>
public enum SortDirection
{
    /// <summary>Smallest first (A→Z, 0→9, earliest→latest).</summary>
    Ascending,

    /// <summary>Largest first (Z→A, 9→0, latest→earliest).</summary>
    Descending,
}

/// <summary>
/// The active sort of a <see cref="DataTable{TItem}"/>: which column (by <paramref name="ColumnKey"/>)
/// and which way. A <see langword="null"/> sort means the rows are in their natural order.
/// </summary>
/// <param name="ColumnKey">The sorted column's <c>Key</c>.</param>
/// <param name="Direction">The sort direction.</param>
public sealed record SortState(string ColumnKey, SortDirection Direction)
{
    /// <summary>The opposite direction (for toggling).</summary>
    public SortState Flipped =>
        this with { Direction = Direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending };
}
