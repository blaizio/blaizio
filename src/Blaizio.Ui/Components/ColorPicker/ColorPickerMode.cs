using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Which surface a <see cref="BzColorPicker"/> is editing on - and therefore what its <c>Value</c> holds.</summary>
public enum ColorPickerMode
{
    /// <summary>One color, picked off the saturation/value area; <c>Value</c> is a color string in the active <see cref="ColorFormat"/>. The default.</summary>
    [Description("Solid")]
    Solid,

    /// <summary>A gradient; <c>Value</c> is the CSS paint, and the color parts edit the selected stop.</summary>
    [Description("Gradient")]
    Gradient,

    /// <summary>One color, picked out of an image; <c>Value</c> is a color string, same as <see cref="Solid"/>.</summary>
    [Description("Image")]
    Image,
}
