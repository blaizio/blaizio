namespace Blaizio.Docs;

/// <summary>
/// Row sources for the big-list demos. A virtualizer only ever touches the handful of items in
/// view, so materializing a hundred thousand records up front is pure cost - on WebAssembly it
/// blocked the page for seconds before the first paint. <see cref="Lazy{T}"/> keeps the count
/// honest while computing a row only when it is asked for; <see cref="Cached{TKey, T}"/> is for the
/// demos that genuinely need the whole set (a data table sorts and filters across every row) and
/// just avoids rebuilding it on every visit.
/// </summary>
public static class DemoRows
{
    /// <summary>
    /// A list of <paramref name="count"/> rows computed on access and never stored. The factory
    /// must be deterministic - it is called again each time the same index is read.
    /// </summary>
    public static IReadOnlyList<T> Lazy<T>(int count, Func<int, T> factory) => new LazyRows<T>(count, factory);

    /// <summary>
    /// Builds the list once per session and hands back the same instance afterwards, keyed by
    /// <typeparamref name="TKey"/> so each demo gets its own.
    /// </summary>
    public static IReadOnlyList<T> Cached<TKey, T>(Func<IReadOnlyList<T>> build) => Store<TKey, T>.Value ??= build();

    private static class Store<TKey, T>
    {
        public static IReadOnlyList<T>? Value;
    }

    private sealed class LazyRows<T>(int count, Func<int, T> factory) : IReadOnlyList<T>
    {
        public int Count => count;

        public T this[int index] => (uint)index < (uint)count
            ? factory(index)
            : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < count; i++) yield return factory(i);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>A stable pseudo-random value for a row index - no shared Random, so a row computed
    /// on demand looks the same every time it is asked for.</summary>
    public static int Pick(int index, int salt, int length)
    {
        var h = (uint)(index * 2654435761u ^ (uint)salt * 2246822519u);
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        return (int)(h % (uint)length);
    }
}
