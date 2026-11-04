using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>State of a <see cref="BzCodeBlockLine"/>.</summary>
public enum CodeBlockLineVariant
{
    /// <summary>A plain line (the default).</summary>
    [Description("default")]
    Default,

    /// <summary>An added diff line: tinted, with a <c>+</c> marker.</summary>
    [Description("added")]
    Added,

    /// <summary>A removed diff line: tinted, with a <c>−</c> marker. Left out of what the copy button copies.</summary>
    [Description("removed")]
    Removed,

    /// <summary>A line called out from the rest.</summary>
    [Description("highlighted")]
    Highlighted,
}
