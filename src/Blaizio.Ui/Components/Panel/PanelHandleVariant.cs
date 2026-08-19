namespace Blaizio.Ui;

/// <summary>
/// The affordance drawn on a <see cref="BzPanel"/> resize handle. Mirrors the Resizable handle's
/// options. Every variant except <see cref="None"/> also reveals a thin centered line on hover and
/// while dragging; <see cref="None"/> is the off switch - a bare hit strip, the panel's own border
/// being the only line.
/// </summary>
public enum PanelHandleVariant
{
    /// <summary>No affordance - a thin invisible hit strip over the panel's border.</summary>
    None,

    /// <summary>No grip; just the thin centered line on hover and while dragging.</summary>
    Line,

    /// <summary>A small bordered tab with a grip glyph.</summary>
    Grip,

    /// <summary>A bare grip glyph.</summary>
    Dots,

    /// <summary>A round bordered knob with a grip glyph.</summary>
    Knob,

    /// <summary>A rounded solid bar.</summary>
    Pill,
}
