namespace Blazeo;

/// <summary>How a <see cref="BaseSelectContent"/> listbox is positioned relative to its trigger.</summary>
public enum SelectPosition
{
    /// <summary>
    /// Float below (or above, when it would overflow) the trigger, like a popover - the default. The
    /// listbox never covers the trigger and flips to stay in view.
    /// </summary>
    Popper,

    /// <summary>
    /// Overlay the listbox on the trigger so the selected option sits directly over it (the native
    /// select feel). The list is clamped to the viewport and scrolled so the selected option stays
    /// aligned with the trigger; with nothing selected the first option aligns instead.
    /// </summary>
    ItemAligned,
}
