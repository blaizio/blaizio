namespace Blaizio.Ui;

/// <summary>How the image fills its box (CSS <c>object-fit</c>).</summary>
public enum ImageFit
{
    /// <summary>Fill the box, cropping as needed (<c>object-cover</c>). The default.</summary>
    Cover,

    /// <summary>Fit entirely inside the box, letterboxing as needed (<c>object-contain</c>).</summary>
    Contain,

    /// <summary>Stretch to the box, ignoring aspect ratio (<c>object-fill</c>).</summary>
    Fill,

    /// <summary>Render at intrinsic size, cropping overflow (<c>object-none</c>).</summary>
    None,

    /// <summary>The smaller of <c>None</c> and <c>Contain</c> (<c>object-scale-down</c>).</summary>
    ScaleDown
}
