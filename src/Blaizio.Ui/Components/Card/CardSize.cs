using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Size of a <see cref="BzCard"/> - drives the style sheet's padding/gap scale via <c>data-size</c>.</summary>
public enum CardSize
{
    /// <summary>Standard paddings.</summary>
    [Description("default")]
    Default,

    /// <summary>Tighter paddings.</summary>
    [Description("sm")]
    Sm,
}
