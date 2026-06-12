using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Visual style of a <see cref="Toggle"/>.</summary>
public enum ToggleVariant
{
    /// <summary>Transparent background; tints when pressed.</summary>
    [Description("default")]
    Default,

    /// <summary>Bordered, transparent background.</summary>
    [Description("outline")]
    Outline,
}

/// <summary>Size of a <see cref="Toggle"/>. Each is square-ish with a matching min-width.</summary>
public enum ToggleSize
{
    /// <summary>Standard height (h-9).</summary>
    [Description("default")]
    Default,

    /// <summary>Small (h-8).</summary>
    [Description("sm")]
    Sm,

    /// <summary>Large (h-10).</summary>
    [Description("lg")]
    Lg,
}
