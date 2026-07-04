namespace Blaizio.Ui;

/// <summary>
/// Builds CSS <c>cursor</c> values from Blaizio icons, for components that take a cursor
/// (e.g. a sortable's <c>Cursor</c>/<c>GrabbingCursor</c>). The icon is inlined as an SVG data URI -
/// no asset to ship - with a keyword fallback for browsers that refuse image cursors.
/// </summary>
/// <example>
/// <code>
/// &lt;BzSortable Cursor="@BzCursor.From(Icons.Outline.HandStop)"
///             GrabbingCursor="@BzCursor.From(Icons.Outline.HandGrab, fallback: "grabbing")"&gt;
/// </code>
/// </example>
public static class BzCursor
{
    /// <summary>
    /// A <c>cursor</c> value rendering <paramref name="icon"/> at <paramref name="size"/> px.
    /// </summary>
    /// <param name="icon">The icon (e.g. <c>Icons.Outline.HandStop</c>).</param>
    /// <param name="size">Cursor bitmap size in px. Defaults to 24 (browsers cap at 128).</param>
    /// <param name="hotspotX">The click point's x within the image. Defaults to the centre.</param>
    /// <param name="hotspotY">The click point's y within the image. Defaults to the centre.</param>
    /// <param name="color">Stroke/fill colour. A CSS color; <c>currentColor</c> does not work inside a cursor. Defaults to black.</param>
    /// <param name="fallback">The keyword cursor used when the image can't be shown. Defaults to <c>grab</c>.</param>
    public static string From(
        Icon icon, int size = 24, int? hotspotX = null, int? hotspotY = null,
        string color = "black", string fallback = "grab")
    {
        if (string.IsNullOrEmpty(icon.Body)) return fallback;

        var paint = icon.Kind == IconKind.Outline
            ? $"fill=\"none\" stroke=\"{color}\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\""
            : $"fill=\"{color}\"";
        var svg =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" width=\"{size}\" height=\"{size}\" {paint}>{icon.Body}</svg>";
        var x = hotspotX ?? size / 2;
        var y = hotspotY ?? size / 2;
        return $"url(\"data:image/svg+xml,{Uri.EscapeDataString(svg)}\") {x} {y}, {fallback}";
    }
}
