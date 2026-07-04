namespace Blaizio;

/// <summary>The axis (or plane) the items are laid out on - it decides how a drag's target slot is
/// computed.</summary>
public enum SortableOrientation
{
    /// <summary>A single top-to-bottom column; the target slot is picked from the pointer's Y.</summary>
    Vertical,

    /// <summary>A single left-to-right (RTL-aware) row; the target slot is picked from the pointer's X.</summary>
    Horizontal,

    /// <summary>A wrapping grid; the target slot is the item whose centre is nearest the pointer (2-D).</summary>
    Grid,
}
