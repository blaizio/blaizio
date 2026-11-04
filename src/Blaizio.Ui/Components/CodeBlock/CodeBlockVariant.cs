using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Tone of a <see cref="BzCodeBlock"/>.</summary>
public enum CodeBlockVariant
{
    /// <summary>Code and output (the default).</summary>
    [Description("default")]
    Default,

    /// <summary>Errors and failed output.</summary>
    [Description("destructive")]
    Destructive,
}
