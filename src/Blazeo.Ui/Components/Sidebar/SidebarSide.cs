using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Which inline edge a <see cref="BzSidebar"/> is anchored to. Both are logical (inline edges) and mirror under RTL - <see cref="Start"/> is the left in LTR and the right in RTL.</summary>
public enum SidebarSide
{
    /// <summary>The inline-start edge - left in LTR, right in RTL (the default).</summary>
    [Description("start")] Start,

    /// <summary>The inline-end edge - right in LTR, left in RTL.</summary>
    [Description("end")] End,
}
