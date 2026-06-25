using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Visual style of a <see cref="BzBadge"/>.</summary>
public enum BadgeVariant
{
    /// <summary>Solid primary.</summary>
    [Description("default")]
    Default,

    /// <summary>Muted secondary.</summary>
    [Description("secondary")]
    Secondary,

    /// <summary>Destructive / dangerous.</summary>
    [Description("destructive")]
    Destructive,

    /// <summary>Bordered, transparent background.</summary>
    [Description("outline")]
    Outline,

    /// <summary>No background or border until hovered.</summary>
    [Description("ghost")]
    Ghost,

    /// <summary>Renders like an inline text link.</summary>
    [Description("link")]
    Link,
}
