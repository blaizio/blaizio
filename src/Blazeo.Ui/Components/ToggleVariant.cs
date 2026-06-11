namespace Blazeo.Ui;

/// <summary>Visual style of a <see cref="Toggle"/>.</summary>
public enum ToggleVariant
{
    /// <summary>Transparent background; tints when pressed.</summary>
    Default,

    /// <summary>Bordered, transparent background.</summary>
    Outline,
}

/// <summary>Size of a <see cref="Toggle"/>. Each is square-ish with a matching min-width.</summary>
public enum ToggleSize
{
    /// <summary>Standard height (h-9).</summary>
    Default,

    /// <summary>Small (h-8).</summary>
    Sm,

    /// <summary>Large (h-10).</summary>
    Lg,
}
