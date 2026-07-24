using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>The string format a <see cref="BzColorPicker"/> reads and writes its value in.</summary>
public enum ColorFormat
{
    /// <summary>Hex - <c>#2563eb</c>, or <c>#2563eb80</c> when alpha is below 1. The default.</summary>
    [Description("hex")]
    Hex,

    /// <summary>Modern rgb - <c>rgb(37 99 235)</c>, or <c>rgb(37 99 235 / 0.5)</c> with alpha.</summary>
    [Description("rgb")]
    Rgb,

    /// <summary>Modern hsl - <c>hsl(217 91% 60%)</c>, or with <c>/ alpha</c>.</summary>
    [Description("hsl")]
    Hsl,
}
