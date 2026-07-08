using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Visual style of a <see cref="BzButton"/>.</summary>
public enum ButtonVariant
{
    /// <summary>Solid primary action.</summary>
    [Description("default")]
    Default,

    /// <summary>Destructive / dangerous action (delete, etc.).</summary>
    [Description("destructive")]
    Destructive,

    /// <summary>Positive / confirming action (approve, publish).</summary>
    [Description("success")]
    Success,

    /// <summary>Caution action (proceed with care).</summary>
    [Description("warning")]
    Warning,

    /// <summary>Informational action.</summary>
    [Description("info")]
    Info,

    /// <summary>Bordered, transparent background.</summary>
    [Description("outline")]
    Outline,

    /// <summary>Muted secondary action.</summary>
    [Description("secondary")]
    Secondary,

    /// <summary>No background until hovered.</summary>
    [Description("ghost")]
    Ghost,

    /// <summary>Renders as an inline text link.</summary>
    [Description("link")]
    Link,
}

/// <summary>Size of a <see cref="BzButton"/>. The <c>Icon*</c> sizes are square, for icon-only buttons.</summary>
public enum ButtonSize
{
    /// <summary>Standard height (h-9).</summary>
    [Description("default")]
    Default,

    /// <summary>Extra small (h-6).</summary>
    [Description("xs")]
    Xs,

    /// <summary>Small (h-8).</summary>
    [Description("sm")]
    Sm,

    /// <summary>Large (h-10).</summary>
    [Description("lg")]
    Lg,

    /// <summary>Square icon button (size-9).</summary>
    [Description("icon")]
    Icon,

    /// <summary>Extra-small square icon button (size-6).</summary>
    [Description("icon-xs")]
    IconXs,

    /// <summary>Small square icon button (size-8).</summary>
    [Description("icon-sm")]
    IconSm,

    /// <summary>Large square icon button (size-10).</summary>
    [Description("icon-lg")]
    IconLg,
}
