using System.ComponentModel;

namespace Blazeo;

/// <summary>The axis a <see cref="BaseCarousel"/> scrolls its slides along.</summary>
public enum CarouselOrientation
{
    /// <summary>Slides sit side by side; prev/next move left/right (the default).</summary>
    [Description("horizontal")] Horizontal,

    /// <summary>Slides stack; prev/next move up/down. The content needs a height.</summary>
    [Description("vertical")] Vertical,
}
