namespace Blazeo;

/// <summary>When a <see cref="BaseScrollArea"/>'s overlay scrollbars are shown.</summary>
public enum ScrollAreaType
{
    /// <summary>Shown whenever the content overflows that axis (the default).</summary>
    Auto,

    /// <summary>Always shown while the content overflows; never auto-hidden.</summary>
    Always,

    /// <summary>Shown only while scrolling, then fades out shortly after.</summary>
    Scroll,

    /// <summary>Shown while the pointer is over the area or while scrolling.</summary>
    Hover,
}
