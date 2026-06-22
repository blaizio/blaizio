namespace Blazeo.Ui;

/// <summary>
/// A multi-key sort for a <see cref="DataTable{TItem}"/> column: a primary key plus any number of
/// tie-breakers, each ascending or descending. Build it fluently and hand it to a column's <c>Sort</c>:
/// <code>GridSort&lt;Person&gt;.By(p =&gt; p.LastName).ThenBy(p =&gt; p.FirstName)</code>
/// The chain defines the column's natural ("ascending") order; the header toggle flips the whole chain
/// for the descending pass. Only used for in-memory data - a server-side <see cref="DataItemsProvider{TItem}"/>
/// applies its own ordering.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public sealed class GridSort<T>
{
    private readonly (Func<T, object?> Key, bool Descending)[] _steps;

    private GridSort((Func<T, object?> Key, bool Descending)[] steps) => _steps = steps;

    /// <summary>Start a sort by <paramref name="keySelector"/>, ascending.</summary>
    public static GridSort<T> By(Func<T, object?> keySelector) => new([(keySelector, false)]);

    /// <summary>Start a sort by <paramref name="keySelector"/>, descending.</summary>
    public static GridSort<T> ByDescending(Func<T, object?> keySelector) => new([(keySelector, true)]);

    /// <summary>Add an ascending tie-breaker, applied only when all earlier keys are equal.</summary>
    public GridSort<T> ThenBy(Func<T, object?> keySelector) => new([.._steps, (keySelector, false)]);

    /// <summary>Add a descending tie-breaker, applied only when all earlier keys are equal.</summary>
    public GridSort<T> ThenByDescending(Func<T, object?> keySelector) => new([.._steps, (keySelector, true)]);

    /// <summary>The chain's ascending comparison: the first key that breaks the tie decides the order.</summary>
    internal int Compare(T a, T b)
    {
        foreach (var (key, descending) in _steps)
        {
            var cmp = Comparer<object?>.Default.Compare(key(a), key(b));
            if (cmp != 0) return descending ? -cmp : cmp;
        }

        return 0;
    }
}
