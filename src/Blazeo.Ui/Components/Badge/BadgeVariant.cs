namespace Blazeo.Ui;

/// <summary>Visual style of a <see cref="Badge"/>.</summary>
public enum BadgeVariant
{
    /// <summary>Solid primary.</summary>
    Default,

    /// <summary>Muted secondary.</summary>
    Secondary,

    /// <summary>Destructive / dangerous.</summary>
    Destructive,

    /// <summary>Bordered, transparent background.</summary>
    Outline,

    /// <summary>No background or border until hovered.</summary>
    Ghost,

    /// <summary>Renders like an inline text link.</summary>
    Link,
}
