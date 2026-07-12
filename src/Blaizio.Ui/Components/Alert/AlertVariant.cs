using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Visual style of an <see cref="BzAlert"/>.</summary>
public enum AlertVariant
{
    /// <summary>Neutral informational alert.</summary>
    [Description("default")]
    Default,

    /// <summary>Accent-tinted callout.</summary>
    [Description("accent")]
    Accent,

    /// <summary>Error / destructive alert.</summary>
    [Description("destructive")]
    Destructive,

    /// <summary>Positive-outcome alert.</summary>
    [Description("success")]
    Success,

    /// <summary>Caution alert.</summary>
    [Description("warning")]
    Warning,

    /// <summary>Informational highlight alert.</summary>
    [Description("info")]
    Info,
}
