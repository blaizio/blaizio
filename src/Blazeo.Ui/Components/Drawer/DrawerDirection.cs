using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Which edge a <see cref="BzDrawerContent"/> is anchored to, slides in from, and is dragged toward to dismiss. Start/End are logical (inline edges) and mirror under RTL; Top/Bottom are physical.</summary>
public enum DrawerDirection
{
    /// <summary>The bottom edge; drag down to dismiss (the default). Shows the drag handle.</summary>
    [Description("bottom")] Bottom,

    /// <summary>The top edge; drag up to dismiss.</summary>
    [Description("top")] Top,

    /// <summary>The inline-start edge - left in LTR, right in RTL; drag toward that edge to dismiss.</summary>
    [Description("start")] Start,

    /// <summary>The inline-end edge - right in LTR, left in RTL; drag toward that edge to dismiss.</summary>
    [Description("end")] End,
}
