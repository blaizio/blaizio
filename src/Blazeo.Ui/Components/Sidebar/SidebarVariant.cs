using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>The surface treatment of a <see cref="BzSidebar"/>.</summary>
public enum SidebarVariant
{
    /// <summary>A flush panel against the viewport edge with a hairline border (the default).</summary>
    [Description("sidebar")] Sidebar,

    /// <summary>A detached, rounded, bordered card floating inside a small inset of padding.</summary>
    [Description("floating")] Floating,

    /// <summary>A flush panel paired with a <see cref="BzSidebarInset"/> that lifts the main content into a rounded card.</summary>
    [Description("inset")] Inset,
}
