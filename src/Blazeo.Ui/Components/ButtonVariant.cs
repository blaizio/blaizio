namespace Blazeo.Ui;

/// <summary>Visual style of a <see cref="Button"/>.</summary>
public enum ButtonVariant
{
    /// <summary>Solid primary action.</summary>
    Default,

    /// <summary>Destructive / dangerous action (delete, etc.).</summary>
    Destructive,

    /// <summary>Bordered, transparent background.</summary>
    Outline,

    /// <summary>Muted secondary action.</summary>
    Secondary,

    /// <summary>No background until hovered.</summary>
    Ghost,

    /// <summary>Renders as an inline text link.</summary>
    Link,
}

/// <summary>Size of a <see cref="Button"/>. The <c>Icon*</c> sizes are square, for icon-only buttons.</summary>
public enum ButtonSize
{
    /// <summary>Standard height (h-9).</summary>
    Default,

    /// <summary>Extra small (h-6).</summary>
    Xs,

    /// <summary>Small (h-8).</summary>
    Sm,

    /// <summary>Large (h-10).</summary>
    Lg,

    /// <summary>Square icon button (size-9).</summary>
    Icon,

    /// <summary>Extra-small square icon button (size-6).</summary>
    IconXs,

    /// <summary>Small square icon button (size-8).</summary>
    IconSm,

    /// <summary>Large square icon button (size-10).</summary>
    IconLg,
}
