namespace Blaizio.Ui;

/// <summary>
/// The query a <see cref="BzVirtualizer{TItem}"/> sends its <see cref="VirtualizerItemsProvider{TItem}"/>
/// for one window of items: return the <see cref="Count"/> items starting at <see cref="StartIndex"/>.
/// Honour <see cref="CancellationToken"/> - it is cancelled when scrolling supersedes this request
/// with a newer window, so you can drop stale work. The provider is called as the window moves;
/// cache at the source if a fetch is expensive.
/// </summary>
/// <param name="StartIndex">Zero-based index of the first item to return.</param>
/// <param name="Count">How many items to return (the window plus overscan).</param>
/// <param name="CancellationToken">Cancelled when a newer window supersedes this one.</param>
public sealed record VirtualizerRequest(
    int StartIndex,
    int Count,
    CancellationToken CancellationToken);

/// <summary>
/// One window of items returned to a <see cref="BzVirtualizer{TItem}"/>: the <see cref="Items"/> for
/// the requested slice, plus <see cref="TotalCount"/> - the total size of the data set, which sizes
/// the scroll range so the user can grab the scrollbar and jump anywhere immediately.
/// </summary>
/// <typeparam name="TItem">The item data type.</typeparam>
/// <param name="Items">The items for the requested window.</param>
/// <param name="TotalCount">Total items in the data set.</param>
public sealed record VirtualizerResult<TItem>(IReadOnlyList<TItem> Items, int TotalCount);

/// <summary>
/// Supplies a <see cref="BzVirtualizer{TItem}"/> with items on demand, one window at a time - the
/// constant-memory path for large server-backed lists. Only the visible window (plus overscan) is
/// ever held in memory; what scrolls away is discarded and re-requested if the user returns. This
/// is one of the virtualizer's two data sources and they never combine: <c>Items</c> (optionally
/// with <c>OnLoadMore</c>) means YOU own the list; <c>ItemsProvider</c> means the virtualizer asks
/// you for each window. Setting both throws.
/// </summary>
/// <typeparam name="TItem">The item data type.</typeparam>
public delegate ValueTask<VirtualizerResult<TItem>> VirtualizerItemsProvider<TItem>(VirtualizerRequest request);
