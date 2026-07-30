using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Width preset of a <see cref="BzColorPicker"/>. Override freely via <c>Class</c>.</summary>
public enum ColorPickerSize
{
    /// <summary>224px wide.</summary>
    [Description("sm")]
    Sm,

    /// <summary>256px wide. The default.</summary>
    [Description("md")]
    Default,

    /// <summary>288px wide.</summary>
    [Description("lg")]
    Lg,
}
