namespace Blaizio.Ui;

/// <summary>
/// The picker's drag cursors, drawn from the Tabler hand icons instead of the browser's
/// <c>grab</c>/<c>grabbing</c> defaults. <see cref="BzColorPicker"/> publishes them as custom
/// properties on its root, and the sheets read them
/// (<c>cursor:var(--bz-color-cursor-stop),grab</c>) - keeping the long data URI out of the
/// stylesheet, where the registry inliner would have to carry it through class strings.
/// </summary>
internal static class ColorCursors
{
    /// <summary>Custom property carrying the at-rest cursor.</summary>
    public const string StopVariable = "--bz-color-cursor-stop";

    /// <summary>Custom property carrying the dragging cursor.</summary>
    public const string GrabVariable = "--bz-color-cursor-grab";

    /// <summary>The open hand image, for a draggable surface at rest.</summary>
    public static string StopImage { get; } = Image(Icons.Outline.HandStop.Body);

    /// <summary>The closed hand image, for a surface being dragged.</summary>
    public static string GrabImage { get; } = Image(Icons.Outline.HandGrab.Body);

    /// <summary>The hotspot, in image pixels - the middle of the palm.</summary>
    public const int Hotspot = 13;

    /// <summary>
    /// Both cursors as a style declaration, for the picker root to cascade. Chromium ignores an
    /// SVG cursor, so ts/cursors.ts overwrites these with rasterised PNGs on first render; this is
    /// what shows in the meantime, and what engines that DO render SVG cursors keep using if the
    /// script never runs.
    /// </summary>
    public static string Variables { get; } =
        $"{StopVariable}:url({StopImage}) {Hotspot} {Hotspot};" +
        $"{GrabVariable}:url({GrabImage}) {Hotspot} {Hotspot}";

    // 26px so the hand reads at a glance.
    private const int Size = 26;

    /// <summary>
    /// One icon as a cursor IMAGE - a SOLID hand, the way a cursor reads: the glyph's paths are
    /// filled white and outlined black, so it stays legible over both a white swatch and a black
    /// one. (Tabler ships no filled hands, so the outline paths are filled here rather than
    /// stroked twice.) Percent-encoding the whole document keeps the URI free of spaces, quotes
    /// and <c>#</c>, so it needs no quoting inside <c>url()</c> and survives being carried in a
    /// style attribute.
    /// </summary>
    private static string Image(string body)
    {
        var svg =
            $"<svg xmlns='http://www.w3.org/2000/svg' width='{Size}' height='{Size}' viewBox='0 0 24 24' " +
            "stroke-linecap='round' stroke-linejoin='round'>" +
            $"<g fill='none' stroke='#000000' stroke-width='3.5'>{body}</g>" +
            $"<g fill='#ffffff' stroke='#ffffff' stroke-width='1.25'>{body}</g>" +
            "</svg>";
        return $"data:image/svg+xml,{Uri.EscapeDataString(svg)}";
    }
}
