using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>How a <see cref="BzSidebar"/> collapses when toggled off on desktop.</summary>
public enum SidebarCollapsible
{
    /// <summary>Slides entirely off the edge, leaving no rail (the default).</summary>
    [Description("offcanvas")] Offcanvas,

    /// <summary>Shrinks to a narrow icon rail; labels hide and menu buttons become square.</summary>
    [Description("icon")] Icon,

    /// <summary>Never collapses - the toggle and rail are inert; the sidebar is always at full width.</summary>
    [Description("none")] None,
}
