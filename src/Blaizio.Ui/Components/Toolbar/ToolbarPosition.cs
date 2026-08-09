using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>
/// Where a <see cref="BzToolbar"/> sits. <see cref="Inline"/> leaves it in the layout flow; every
/// other value floats it against an edge of the viewport, sliding in from that edge on mount. The
/// edges are logical, so a <c>Start*</c> bar sits on the left in LTR and on the right in RTL. Pass
/// <c>absolute</c> in <c>Class</c> to anchor a floating bar inside the nearest positioned ancestor
/// instead of the viewport - the usual shape for a contextual toolbar that appears over a region.
/// </summary>
public enum ToolbarPosition
{
    /// <summary>In the layout flow, wherever it is placed (the default).</summary>
    [Description("inline")] Inline,

    /// <summary>Floating along the top edge, at the start.</summary>
    [Description("top-start")] TopStart,

    /// <summary>Floating along the top edge, centred.</summary>
    [Description("top-center")] TopCenter,

    /// <summary>Floating along the top edge, at the end.</summary>
    [Description("top-end")] TopEnd,

    /// <summary>Floating along the bottom edge, at the start.</summary>
    [Description("bottom-start")] BottomStart,

    /// <summary>Floating along the bottom edge, centred - the classic dock.</summary>
    [Description("bottom-center")] BottomCenter,

    /// <summary>Floating along the bottom edge, at the end.</summary>
    [Description("bottom-end")] BottomEnd,

    /// <summary>Floating against the start edge, vertically centred (pair with a vertical bar).</summary>
    [Description("start-center")] StartCenter,

    /// <summary>Floating against the end edge, vertically centred (pair with a vertical bar).</summary>
    [Description("end-center")] EndCenter,
}

/// <summary>The layout classes behind a <see cref="ToolbarPosition"/>.</summary>
public static class ToolbarPositionExtensions
{
    /// <summary>True for every value that takes the bar out of the layout flow.</summary>
    public static bool IsFloating(this ToolbarPosition position) => position != ToolbarPosition.Inline;

    /// <summary>
    /// The positioning utilities for a floating bar (empty for <see cref="ToolbarPosition.Inline"/>).
    /// Ordinary Tailwind classes, so a consumer's <c>Class</c> still wins the merge: <c>absolute</c>
    /// anchors the bar inside a positioned ancestor, and <c>bottom-*</c>/<c>start-*</c> change the
    /// gap it keeps from the edge. Centring uses insets + auto margins rather than a transform, so
    /// the entrance animation's translate stays free.
    /// </summary>
    public static string Classes(this ToolbarPosition position) => position switch
    {
        ToolbarPosition.Inline => string.Empty,
        ToolbarPosition.TopStart => "fixed z-40 top-4 start-4",
        ToolbarPosition.TopCenter => "fixed z-40 top-4 inset-x-4 mx-auto",
        ToolbarPosition.TopEnd => "fixed z-40 top-4 end-4",
        ToolbarPosition.BottomStart => "fixed z-40 bottom-4 start-4",
        ToolbarPosition.BottomCenter => "fixed z-40 bottom-4 inset-x-4 mx-auto",
        ToolbarPosition.BottomEnd => "fixed z-40 bottom-4 end-4",
        ToolbarPosition.StartCenter => "fixed z-40 start-4 inset-y-4 my-auto h-fit",
        _ => "fixed z-40 end-4 inset-y-4 my-auto h-fit",
    };
}
