namespace Blaizio.Ui;

/// <summary>
/// The query a <see cref="BzDataTable{TItem}"/> sends its <see cref="DataItemsProvider{TItem}"/> for one
/// window of rows: which slice (<see cref="StartIndex"/> / <see cref="Count"/>) and the active
/// <see cref="Sort"/> and <see cref="Filter"/> to apply at the source. Honour
/// <see cref="CancellationToken"/> - it is cancelled when a newer request supersedes this one, so you
/// can drop stale work.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
/// <param name="StartIndex">Zero-based index of the first row to return (the current page's offset).</param>
/// <param name="Count">How many rows to return (the page size; <see cref="int.MaxValue"/> when not paging).</param>
/// <param name="Sort">The active sort, or <see langword="null"/> for natural order.</param>
/// <param name="Filter">The active search text, or <see langword="null"/>/empty when unfiltered.</param>
/// <param name="CancellationToken">Cancelled when a newer request supersedes this one.</param>
public sealed record DataGridRequest<TItem>(
    int StartIndex,
    int Count,
    SortState? Sort,
    string? Filter,
    CancellationToken CancellationToken);

/// <summary>
/// One window of rows returned to a <see cref="BzDataTable{TItem}"/>: the <see cref="Items"/> for the
/// requested slice, plus <see cref="TotalCount"/> - the total number of rows across all pages (after the
/// filter), which the grid uses to size the pager.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
/// <param name="Items">The rows for the requested window.</param>
/// <param name="TotalCount">Total rows across all pages, after filtering.</param>
public sealed record DataGridResult<TItem>(IReadOnlyList<TItem> Items, int TotalCount);

/// <summary>
/// Supplies a <see cref="BzDataTable{TItem}"/> with rows on demand - for server-side or otherwise async
/// data. Given a <see cref="DataGridRequest{TItem}"/> (slice + sort + filter), return that window plus
/// the total count. When set on a BzDataTable it drives the data <i>instead of</i> the in-memory
/// <c>Items</c> + LINQ pipeline, so sorting, filtering and paging all happen at the source.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public delegate ValueTask<DataGridResult<TItem>> DataItemsProvider<TItem>(DataGridRequest<TItem> request);
