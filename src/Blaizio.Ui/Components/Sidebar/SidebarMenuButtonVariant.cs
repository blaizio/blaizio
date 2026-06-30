using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Visual style of a <see cref="BzSidebarMenuButton"/>.</summary>
public enum SidebarMenuButtonVariant
{
    /// <summary>A plain row that tints on hover and when active (the default).</summary>
    [Description("default")] Default,

    /// <summary>A bordered row on the page background, for a more card-like menu.</summary>
    [Description("outline")] Outline,
}

/// <summary>Height of a <see cref="BzSidebarMenuButton"/>.</summary>
public enum SidebarMenuButtonSize
{
    /// <summary>The standard 8-unit row (the default).</summary>
    [Description("default")] Default,

    /// <summary>A compact 7-unit row with smaller text.</summary>
    [Description("sm")] Sm,

    /// <summary>A tall 12-unit row, for a header entry with a sub-label.</summary>
    [Description("lg")] Lg,
}

/// <summary>Height of a <see cref="BzSidebarMenuSubButton"/>.</summary>
public enum SidebarMenuSubButtonSize
{
    /// <summary>A compact sub-row with extra-small text.</summary>
    [Description("sm")] Sm,

    /// <summary>The standard sub-row (the default).</summary>
    [Description("md")] Md,
}
