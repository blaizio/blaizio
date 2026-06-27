using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Which edge a <see cref="BzSheetContent"/> is anchored to and slides in from. Start/End are logical (inline edges) and mirror under RTL; Top/Bottom are physical.</summary>
public enum SheetSide
{
    /// <summary>The inline-end edge - right in LTR, left in RTL (the default).</summary>
    [Description("end")] End,

    /// <summary>The inline-start edge - left in LTR, right in RTL.</summary>
    [Description("start")] Start,

    /// <summary>The top edge.</summary>
    [Description("top")] Top,

    /// <summary>The bottom edge.</summary>
    [Description("bottom")] Bottom,
}
