namespace Blaizio;

/// <summary>How the pending move is previewed while dragging.</summary>
public enum SortableSwapMode
{
    /// <summary>Live preview: items slide out of the way (or the swap partner trades places) as you hover.</summary>
    Swap,

    /// <summary>Nothing moves until release - the action happens on drop. Pair with
    /// <see cref="BaseSortable.ShowPlaceholder"/> so the target is still visible.</summary>
    Drop,
}
