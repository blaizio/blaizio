using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Which edge an edge-anchored panel (a <c>BzSheetContent</c> or <c>BzDrawerContent</c>) is anchored to and slides in from. Start/End are logical (inline edges) and mirror under RTL; Top/Bottom are physical.</summary>
public enum PanelSide
{
    /// <summary>The inline-start edge - left in LTR, right in RTL.</summary>
    [Description("start")] Start,

    /// <summary>The inline-end edge - right in LTR, left in RTL.</summary>
    [Description("end")] End,

    /// <summary>The top edge.</summary>
    [Description("top")] Top,

    /// <summary>The bottom edge.</summary>
    [Description("bottom")] Bottom,
}
