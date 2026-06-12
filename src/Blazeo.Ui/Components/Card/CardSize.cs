using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Size of a <see cref="Card"/> - drives the style sheet's padding/gap scale via <c>data-size</c>.</summary>
public enum CardSize
{
    /// <summary>Standard paddings.</summary>
    [Description("default")]
    Default,

    /// <summary>Tighter paddings.</summary>
    [Description("sm")]
    Sm,
}
