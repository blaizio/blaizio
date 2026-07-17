using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Size of a <see cref="BzSwitch"/> - drives the style sheet's track/thumb scale via <c>data-size</c>.</summary>
public enum SwitchSize
{
    /// <summary>Standard.</summary>
    [Description("default")]
    Default,

    /// <summary>Small.</summary>
    [Description("sm")]
    Sm,

    /// <summary>Large - roomy enough for track and thumb icons.</summary>
    [Description("lg")]
    Lg,
}
