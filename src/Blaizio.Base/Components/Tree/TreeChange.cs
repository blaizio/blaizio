namespace Blaizio;

/// <summary>
/// One committed tree move: the dragged node's value, the drop target's value, and where the node
/// lands relative to the target (<see cref="TreeDropPosition"/>). Raised by
/// <see cref="BaseTree{TItem}"/> for pointer drops - apply it to your own data with
/// <see cref="Apply{T}(System.Collections.Generic.IList{T}, Func{T, System.Collections.Generic.IList{T}?}, Func{T, string})"/>
/// (or the two-tree overload for a cross-tree transfer) and re-render.
/// </summary>
public sealed record TreeChange
{
    /// <summary>The value of the node that was dragged.</summary>
    public required string SourceValue { get; init; }

    /// <summary>
    /// The value of the drop target node, or <see langword="null"/> for a drop on the tree's empty
    /// space (lands at the end of the root level).
    /// </summary>
    public required string? TargetValue { get; init; }

    /// <summary>Where the node lands relative to <see cref="TargetValue"/>.</summary>
    public required TreeDropPosition Position { get; init; }

    /// <summary>The id of the tree the node was dragged out of.</summary>
    public required string FromId { get; init; }

    /// <summary>The id of the tree the node was dropped into.</summary>
    public required string ToId { get; init; }

    /// <summary>True when the node moved between two different trees (of the same Group).</summary>
    public bool IsCrossTree => !string.Equals(FromId, ToId, StringComparison.Ordinal);

    /// <summary>
    /// Applies a same-tree move to <paramref name="roots"/>: detaches the source node from wherever
    /// it sits and re-inserts it at the target position. <paramref name="children"/> returns a
    /// node's mutable child list (or <see langword="null"/> for a leaf); <paramref name="value"/>
    /// returns a node's unique value. Returns <see langword="false"/> - leaving the data untouched -
    /// for a cross-tree change, an unknown source/target, a drop of a node onto itself or into its
    /// own subtree, or an <see cref="TreeDropPosition.Inside"/> drop on a node with no child list.
    /// </summary>
    public bool Apply<T>(IList<T> roots, Func<T, IList<T>?> children, Func<T, string> value) =>
        !IsCrossTree && ApplyCore(roots, roots, children, value);

    /// <summary>
    /// Applies a move between two trees: detaches the source node from
    /// <paramref name="sourceRoots"/> and inserts it at the target position in
    /// <paramref name="targetRoots"/>. Also handles the same-tree case (both arguments the same
    /// list), so one code path can serve both sides of a shared handler.
    /// </summary>
    public bool Apply<T>(IList<T> sourceRoots, IList<T> targetRoots, Func<T, IList<T>?> children, Func<T, string> value) =>
        ApplyCore(sourceRoots, targetRoots, children, value);

    private bool ApplyCore<T>(IList<T> sourceRoots, IList<T> targetRoots, Func<T, IList<T>?> children, Func<T, string> value)
    {
        if (SourceValue == TargetValue) return false;

        var source = Locate(sourceRoots, children, value, SourceValue);
        if (source is null) return false;
        var (sourceList, sourceIndex) = source.Value;
        var node = sourceList[sourceIndex];

        // A node can't land inside its own subtree (that would orphan it).
        if (TargetValue is not null && Locate([node], children, value, TargetValue) is not null) return false;

        // Detach first, then locate the target - removal may shift the target's index.
        sourceList.RemoveAt(sourceIndex);

        if (TargetValue is null)
        {
            targetRoots.Add(node);
            return true;
        }

        var target = Locate(targetRoots, children, value, TargetValue);
        if (target is null)
        {
            sourceList.Insert(sourceIndex, node); // target vanished: put the node back
            return false;
        }

        var (targetList, targetIndex) = target.Value;
        switch (Position)
        {
            case TreeDropPosition.Before:
                targetList.Insert(targetIndex, node);
                break;
            case TreeDropPosition.After:
                targetList.Insert(targetIndex + 1, node);
                break;
            default:
                var kids = children(targetList[targetIndex]);
                if (kids is null)
                {
                    sourceList.Insert(sourceIndex, node); // the target can't hold children
                    return false;
                }
                kids.Add(node);
                break;
        }

        return true;
    }

    // Depth-first search for the list + index holding the node with the given value.
    private static (IList<T> List, int Index)? Locate<T>(
        IList<T> list, Func<T, IList<T>?> children, Func<T, string> value, string target)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(value(list[i]), target, StringComparison.Ordinal)) return (list, i);
            if (children(list[i]) is { } kids && Locate(kids, children, value, target) is { } found) return found;
        }

        return null;
    }
}
