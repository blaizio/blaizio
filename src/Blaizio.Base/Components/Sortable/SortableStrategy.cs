namespace Blaizio;

/// <summary>How a drop rearranges the list.</summary>
public enum SortableStrategy
{
    /// <summary>
    /// The list reorders: the dragged item is removed from its slot and re-inserted at the
    /// drop position, and every item in between shifts to fill the gap.
    /// </summary>
    Sort,

    /// <summary>
    /// The dragged item and the item it is dropped on trade places; everything else stays put.
    /// Only meaningful within a single list.
    /// </summary>
    Swap,
}
