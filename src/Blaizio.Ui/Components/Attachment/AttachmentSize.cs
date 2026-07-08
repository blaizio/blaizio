using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Density of a <see cref="BzAttachment"/> card, flowed to the style sheet via <c>data-size</c>.</summary>
public enum AttachmentSize
{
    /// <summary>Standard density.</summary>
    [Description("default")]
    Default,

    /// <summary>Compact.</summary>
    [Description("sm")]
    Sm,

    /// <summary>Minimal - a chip-like row.</summary>
    [Description("xs")]
    Xs,
}
