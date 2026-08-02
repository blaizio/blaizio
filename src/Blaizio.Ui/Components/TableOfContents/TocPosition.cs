using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>
/// Where a <see cref="BzTableOfContents"/> sits. <see cref="Inline"/> leaves it in the layout flow;
/// every other value floats it against an edge of the viewport. The edges are logical, so a
/// <c>Start*</c> panel sits on the left in LTR and on the right in RTL.
/// </summary>
public enum TocPosition
{
    /// <summary>In the layout flow, wherever it is placed (the default).</summary>
    [Description("inline")] Inline,

    /// <summary>Floating against the start edge, at the top.</summary>
    [Description("start-top")] StartTop,

    /// <summary>Floating against the start edge, vertically centred.</summary>
    [Description("start-center")] StartCenter,

    /// <summary>Floating against the start edge, at the bottom.</summary>
    [Description("start-bottom")] StartBottom,

    /// <summary>Floating against the end edge, at the top.</summary>
    [Description("end-top")] EndTop,

    /// <summary>Floating against the end edge, vertically centred.</summary>
    [Description("end-center")] EndCenter,

    /// <summary>Floating against the end edge, at the bottom.</summary>
    [Description("end-bottom")] EndBottom,
}

/// <summary>The layout classes behind a <see cref="TocPosition"/>.</summary>
public static class TocPositionExtensions
{
    /// <summary>True for every value that takes the panel out of the layout flow.</summary>
    public static bool IsFloating(this TocPosition position) => position != TocPosition.Inline;

    /// <summary>
    /// The positioning utilities for a floating panel (empty for <see cref="TocPosition.Inline"/>).
    /// They are ordinary Tailwind classes so a consumer's <c>Class</c> still wins the merge: passing
    /// <c>absolute</c> anchors the panel inside a positioned ancestor instead of the viewport, and
    /// <c>start-*</c> / <c>top-*</c> change the gap it keeps from the edge. The gap is an inset
    /// rather than a margin so overriding it cannot take the centring's <c>my-auto</c> with it, and
    /// centring avoids a transform so the entrance animation's own translate stays free.
    /// </summary>
    public static string Classes(this TocPosition position) => position switch
    {
        TocPosition.Inline => string.Empty,
        TocPosition.StartTop => "fixed z-30 start-6 top-6",
        TocPosition.StartCenter => "fixed z-30 start-6 inset-y-6 my-auto h-fit",
        TocPosition.StartBottom => "fixed z-30 start-6 bottom-6",
        TocPosition.EndTop => "fixed z-30 end-6 top-6",
        TocPosition.EndCenter => "fixed z-30 end-6 inset-y-6 my-auto h-fit",
        _ => "fixed z-30 end-6 bottom-6",
    };
}
