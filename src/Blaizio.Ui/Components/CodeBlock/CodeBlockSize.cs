using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Density of a <see cref="BzCodeBlock"/>.</summary>
public enum CodeBlockSize
{
    /// <summary>Regular padding and text (the default).</summary>
    [Description("default")]
    Default,

    /// <summary>Tighter padding and <c>text-xs</c> code - for tool output inside a Trace.</summary>
    [Description("sm")]
    Sm,
}
