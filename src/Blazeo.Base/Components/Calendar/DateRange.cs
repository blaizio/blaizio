namespace Blazeo;

/// <summary>
/// An inclusive span of calendar days. Either end may be <see langword="null"/> while the user is
/// mid-selection (the first click sets <see cref="Start"/> with no <see cref="End"/> yet).
/// </summary>
/// <param name="Start">The first day of the range, or <see langword="null"/> if nothing is picked.</param>
/// <param name="End">The last day of the range, or <see langword="null"/> while only the start is picked.</param>
public readonly record struct DateRange(DateOnly? Start, DateOnly? End)
{
    /// <summary><see langword="true"/> once both ends are set.</summary>
    public bool IsComplete => Start is not null && End is not null;

    /// <summary>The two ends in chronological order (caller guarantees both are set).</summary>
    public (DateOnly Low, DateOnly High) Ordered()
    {
        var start = Start!.Value;
        var end = End!.Value;
        return start <= end ? (start, end) : (end, start);
    }

    /// <summary>
    /// Whether <paramref name="date"/> falls within the range. With only a start picked, that single
    /// day counts; with both ends, every day from the earlier to the later end (inclusive) counts.
    /// </summary>
    public bool Contains(DateOnly date)
    {
        if (Start is not { } start) return false;
        if (End is null) return date == start;
        var (low, high) = Ordered();
        return date >= low && date <= high;
    }
}
