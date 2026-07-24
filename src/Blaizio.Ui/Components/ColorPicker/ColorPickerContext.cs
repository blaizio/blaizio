namespace Blaizio.Ui;

/// <summary>
/// State a <see cref="BzColorPicker"/> cascades to its parts (area, hue/alpha sliders, input,
/// swatches, preview, eye dropper): the HSVA model, the configured format and flags, and the root
/// to write changes back through. Fresh each render, so parts re-render with the color.
/// </summary>
/// <param name="H">Hue in degrees, 0-360.</param>
/// <param name="S">Saturation, 0-1.</param>
/// <param name="V">Value (brightness), 0-1.</param>
/// <param name="A">Alpha, 0-1. Serialized only when <paramref name="ShowAlpha"/> is on.</param>
/// <param name="Format">The string format the picker reads and writes.</param>
/// <param name="Disabled">Whether the whole picker is disabled.</param>
/// <param name="ShowAlpha">Whether alpha is part of the picker's surface.</param>
/// <param name="Root">The owning picker - parts write changes through it.</param>
public sealed record ColorPickerContext(
    double H,
    double S,
    double V,
    double A,
    ColorFormat Format,
    bool Disabled,
    bool ShowAlpha,
    BzColorPicker Root)
{
    /// <summary>The effective alpha - forced opaque when the picker has no alpha surface.</summary>
    public double EffectiveA => ShowAlpha ? A : 1;

    /// <summary>The pure hue at full chroma (<c>#rrggbb</c>) - the area and alpha gradients use it.</summary>
    public string HueHex => ColorMath.Format(H, 1, 1, 1, ColorFormat.Hex);

    /// <summary>The current color ignoring alpha (<c>#rrggbb</c>) - thumb fills and gradient ends.</summary>
    public string SolidHex => ColorMath.Format(H, S, V, 1, ColorFormat.Hex);

    /// <summary>The current color including alpha, as a CSS paint.</summary>
    public string Css => ColorMath.Format(H, S, V, EffectiveA, ColorFormat.Rgb);

    /// <summary>The current color serialized in the picker's format - what <c>Value</c> reads.</summary>
    public string Serialized => ColorMath.Format(H, S, V, EffectiveA, Format);
}
