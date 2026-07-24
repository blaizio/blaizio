using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>The string format a <see cref="BzColorPicker"/> reads and writes its value in.</summary>
public enum ColorFormat
{
    /// <summary>Hex - <c>#2563eb</c>, or <c>#2563eb80</c> when alpha is below 1. The default.</summary>
    [Description("HEX")]
    Hex,

    /// <summary>Modern rgb - <c>rgb(37 99 235)</c>, or <c>rgb(37 99 235 / 0.5)</c> with alpha.</summary>
    [Description("RGB")]
    Rgb,

    /// <summary>Legacy rgba - <c>rgba(37, 99, 235, 0.5)</c>, alpha always written.</summary>
    [Description("RGBA")]
    Rgba,

    /// <summary>Modern hsl - <c>hsl(217 91% 60%)</c>, or with <c>/ alpha</c>.</summary>
    [Description("HSL")]
    Hsl,

    /// <summary>Legacy hsla - <c>hsla(217, 91%, 60%, 0.5)</c>, alpha always written.</summary>
    [Description("HSLA")]
    Hsla,

    /// <summary>Hue-saturation-brightness (aka HSV, the design-tool space) - <c>hsb(217 84% 92%)</c>.</summary>
    [Description("HSB")]
    Hsb,

    /// <summary>Hue-whiteness-blackness - <c>hwb(217 15% 8%)</c>.</summary>
    [Description("HWB")]
    Hwb,

    /// <summary>Naive print separation - <c>device-cmyk(84% 58% 0% 8%)</c>.</summary>
    [Description("CMYK")]
    Cmyk,

    /// <summary>Perceptual lightness-chroma-hue - <c>oklch(0.55 0.19 262)</c>.</summary>
    [Description("OKLCH")]
    Oklch,

    /// <summary>Perceptual Lab - <c>oklab(0.55 -0.02 -0.19)</c>.</summary>
    [Description("OKLab")]
    Oklab,
}
