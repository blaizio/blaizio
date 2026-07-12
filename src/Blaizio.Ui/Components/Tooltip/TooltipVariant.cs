using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Surface color of a <see cref="BzTooltipContent"/> (its arrow follows along).</summary>
public enum TooltipVariant
{
    /// <summary>Soft accent surface - the default.</summary>
    [Description("accent")]
    Accent,

    /// <summary>Solid primary.</summary>
    [Description("primary")]
    Primary,

    /// <summary>Muted secondary.</summary>
    [Description("secondary")]
    Secondary,

    /// <summary>Destructive / dangerous.</summary>
    [Description("destructive")]
    Destructive,

    /// <summary>Positive status.</summary>
    [Description("success")]
    Success,

    /// <summary>Caution status.</summary>
    [Description("warning")]
    Warning,

    /// <summary>Informational status.</summary>
    [Description("info")]
    Info,
}
