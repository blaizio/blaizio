using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>How an <see cref="ImageFillValue"/> lays its bitmap into the box it paints.</summary>
public enum ImageFillFit
{
    /// <summary>Scale until the box is covered, cropping the overflow. The default.</summary>
    [Description("Fill")]
    Fill,

    /// <summary>Scale until the whole image is visible, letterboxing the rest.</summary>
    [Description("Fit")]
    Fit,

    /// <summary>Paint at the bitmap's own size, cropped by the box.</summary>
    [Description("Crop")]
    Crop,

    /// <summary>Repeat from the top-start corner, each tile half the box wide.</summary>
    [Description("Tile")]
    Tile,
}
