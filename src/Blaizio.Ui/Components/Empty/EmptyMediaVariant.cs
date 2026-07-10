using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Visual treatment of an <see cref="BzEmptyMedia"/> slot.</summary>
public enum EmptyMediaVariant
{
    /// <summary>Bare media - renders the content (illustration, avatar, image) as-is.</summary>
    [Description("default")]
    Default,

    /// <summary>Muted tile around a single icon.</summary>
    [Description("icon")]
    Icon,
}
