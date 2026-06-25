using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Which edge a <see cref="BzSheetContent"/> is anchored to and slides in from.</summary>
public enum SheetSide
{
    /// <summary>Slides in from the right edge (the default).</summary>
    [Description("right")] Right,

    /// <summary>Slides in from the left edge.</summary>
    [Description("left")] Left,

    /// <summary>Slides in from the top edge.</summary>
    [Description("top")] Top,

    /// <summary>Slides in from the bottom edge.</summary>
    [Description("bottom")] Bottom,
}
