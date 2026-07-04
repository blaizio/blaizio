namespace Blaizio;

/// <summary>
/// A committed drag: what moved, where it landed, and how (<see cref="Strategy"/>). Raised by
/// <see cref="BaseSortable.OnReorder"/> on drop and after each keyboard step. The pointer/keyboard
/// mechanics live elsewhere - this is the pure description of the resulting rearrangement, with
/// <see cref="Apply{T}(System.Collections.Generic.IList{T})"/> to perform it on the caller's own data.
/// </summary>
/// <remarks>
/// <see cref="From"/> is the item's index in its source list; <see cref="To"/> is the destination
/// index in the target list <i>after</i> the move (0..count, matching the common remove-then-insert
/// convention). <see cref="FromId"/>/<see cref="ToId"/> carry the source/target container ids (each
/// <see cref="BaseSortable.Id"/>); when they differ the item crossed between two lists that share a
/// <see cref="BaseSortable.Group"/> (see <see cref="IsCrossList"/>).
/// </remarks>
public sealed record SortableChange
{
    /// <summary>The item's index in the list it was dragged from.</summary>
    public required int From { get; init; }

    /// <summary>The index the item lands at in the destination list (remove-then-insert convention).</summary>
    public required int To { get; init; }

    /// <summary>The source container's <see cref="BaseSortable.Id"/>.</summary>
    public string? FromId { get; init; }

    /// <summary>The destination container's <see cref="BaseSortable.Id"/>. Equals <see cref="FromId"/> for a same-list drag.</summary>
    public string? ToId { get; init; }

    /// <summary>Whether this was a reorder (<see cref="SortableStrategy.Sort"/>) or a swap.</summary>
    public required SortableStrategy Strategy { get; init; }

    /// <summary>True when the item moved between two different lists (a cross-list transfer).</summary>
    public bool IsCrossList => FromId is not null && ToId is not null && FromId != ToId;

    /// <summary>
    /// Apply this change to a single list in place (the same-list case). No-op when
    /// <see cref="IsCrossList"/> - use <see cref="Apply{T}(IList{T}, IList{T})"/> for that.
    /// </summary>
    public void Apply<T>(System.Collections.Generic.IList<T> list)
    {
        if (IsCrossList) return;
        Apply(list, list);
    }

    /// <summary>
    /// Apply this change across a source and destination list. Pass the same instance for both to
    /// reorder within one list. Silently ignores out-of-range indices so a stale event can't throw.
    /// </summary>
    public void Apply<T>(System.Collections.Generic.IList<T> source, System.Collections.Generic.IList<T> destination)
    {
        if (source is null || destination is null) return;
        if (From < 0 || From >= source.Count) return;
        var sameList = ReferenceEquals(source, destination);

        if (Strategy == SortableStrategy.Swap)
        {
            if (To < 0 || To >= destination.Count) return;
            if (sameList && From == To) return;
            (source[From], destination[To]) = (destination[To], source[From]);
            return;
        }

        var item = source[From];
        source.RemoveAt(From);
        // After the removal the valid insert range is [0, destination.Count] (same-list: count shrank by one).
        var to = System.Math.Clamp(To, 0, destination.Count);
        destination.Insert(to, item);
    }
}
