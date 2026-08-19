namespace Blaizio.Ui;

/// <summary>
/// The grip shown on a <see cref="BzResizableHandle"/>. Every variant keeps the same thin draggable
/// hairline; they differ in the grab affordance drawn on top of it, and every variant except
/// <see cref="None"/> also highlights a thin centered line on hover and while dragging. Use
/// <see cref="None"/> for just the hairline, or pass your own content to the handle to override
/// the grip entirely.
/// </summary>
public enum ResizableHandleVariant
{
    /// <summary>No grip and no hover feedback - just the thin draggable line.</summary>
    None,

    /// <summary>No grip; the hairline plus the centered line highlight on hover and while dragging.</summary>
    Line,

    /// <summary>A bordered box with a grip-dots icon (the classic look).</summary>
    Grip,

    /// <summary>Just the grip-dots icon, no box - a subtler hint.</summary>
    Dots,

    /// <summary>A raised round knob with a grip-dots icon.</summary>
    Knob,

    /// <summary>A small rounded pill bar, like a drag handle on a sheet.</summary>
    Pill,
}
