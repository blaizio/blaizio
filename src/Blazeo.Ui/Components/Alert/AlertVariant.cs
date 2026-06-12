using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Visual style of an <see cref="Alert"/>.</summary>
public enum AlertVariant
{
    /// <summary>Neutral informational alert.</summary>
    [Description("default")]
    Default,

    /// <summary>Error / destructive alert.</summary>
    [Description("destructive")]
    Destructive,
}
