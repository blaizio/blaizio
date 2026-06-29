namespace Blazeo.Ui;

/// <summary>
/// The grip shown on a <see cref="BzResizableHandle"/>. Every variant keeps the same thin draggable
/// hairline; they differ only in the little grab affordance drawn on top of it. Use <see cref="None"/>
/// for just the hairline, or pass your own content to the handle to override the grip entirely.
/// </summary>
public enum ResizableHandleVariant
{
    /// <summary>No grip - just the thin draggable line.</summary>
    None,

    /// <summary>A bordered box with a grip-dots icon (the classic look).</summary>
    Grip,

    /// <summary>Just the grip-dots icon, no box - a subtler hint.</summary>
    Dots,

    /// <summary>A raised round knob with a grip-dots icon.</summary>
    Knob,

    /// <summary>A small rounded pill bar, like a drag handle on a sheet.</summary>
    Pill,
}
