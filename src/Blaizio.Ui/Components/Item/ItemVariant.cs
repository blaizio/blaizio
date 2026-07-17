using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Visual style of a <see cref="BzItem"/>.</summary>
public enum ItemVariant
{
    /// <summary>Transparent row - blends into the surrounding surface.</summary>
    [Description("default")]
    Default,

    /// <summary>Bordered row.</summary>
    [Description("outline")]
    Outline,

    /// <summary>Muted fill.</summary>
    [Description("muted")]
    Muted,
}

/// <summary>Density of a <see cref="BzItem"/>.</summary>
public enum ItemSize
{
    /// <summary>Comfortable spacing.</summary>
    [Description("default")]
    Default,

    /// <summary>Compact spacing for dense lists.</summary>
    [Description("sm")]
    Sm,
}

/// <summary>Visual treatment of a <see cref="BzItemMedia"/> slot.</summary>
public enum ItemMediaVariant
{
    /// <summary>Bare media - renders the content (avatar, illustration) as-is.</summary>
    [Description("default")]
    Default,

    /// <summary>Muted tile around a single icon.</summary>
    [Description("icon")]
    Icon,

    /// <summary>Fixed-size thumbnail that crops its image to fill.</summary>
    [Description("image")]
    Image,
}
