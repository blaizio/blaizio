namespace Blazeo;

/// <summary>
/// Where a toast stack is anchored on the viewport. A toast can override the toaster's default with
/// its own position, which opens a second stack at that corner. Surfaced as the
/// <c>data-y-position</c> / <c>data-x-position</c> pair the layout engine reads.
/// </summary>
public enum ToastPosition
{
    /// <summary>Top-left corner.</summary>
    TopLeft,

    /// <summary>Top edge, horizontally centred.</summary>
    TopCenter,

    /// <summary>Top-right corner.</summary>
    TopRight,

    /// <summary>Bottom-left corner.</summary>
    BottomLeft,

    /// <summary>Bottom edge, horizontally centred.</summary>
    BottomCenter,

    /// <summary>Bottom-right corner (the default).</summary>
    BottomRight,
}

/// <summary>The <c>data-y-position</c> / <c>data-x-position</c> strings for a <see cref="ToastPosition"/>.</summary>
public static class ToastPositionExtensions
{
    /// <summary>The vertical anchor: <c>top</c> or <c>bottom</c>.</summary>
    public static string Y(this ToastPosition position) => position switch
    {
        ToastPosition.TopLeft or ToastPosition.TopCenter or ToastPosition.TopRight => "top",
        _ => "bottom",
    };

    /// <summary>The horizontal anchor: <c>left</c>, <c>center</c> or <c>right</c>.</summary>
    public static string X(this ToastPosition position) => position switch
    {
        ToastPosition.TopLeft or ToastPosition.BottomLeft => "left",
        ToastPosition.TopCenter or ToastPosition.BottomCenter => "center",
        _ => "right",
    };
}
