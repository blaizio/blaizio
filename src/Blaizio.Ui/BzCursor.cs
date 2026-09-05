namespace Blaizio.Ui;

/// <summary>
/// Builds CSS <c>cursor</c> values from Blaizio icons, for components that take a cursor
/// (e.g. a sortable's <c>Cursor</c>/<c>GrabbingCursor</c>). The icon is inlined as an SVG data URI -
/// no asset to ship - with a keyword fallback for browsers that refuse image cursors.
/// </summary>
/// <example>
/// <code>
/// &lt;BzSortable Cursor="@BzCursor.From(Tabler.Outline.HandStop)"
///             GrabbingCursor="@BzCursor.From(Tabler.Outline.HandGrab, fallback: "grabbing")"&gt;
/// </code>
/// </example>
public static class BzCursor
{
    /// <summary>
    /// A <c>cursor</c> value rendering <paramref name="icon"/> at <paramref name="size"/> px.
    /// </summary>
    /// <param name="icon">The icon (e.g. <c>Tabler.Outline.HandStop</c>).</param>
    /// <param name="size">Cursor bitmap size in px. Defaults to 24 (browsers cap at 128).</param>
    /// <param name="hotspotX">The click point's x within the image. Defaults to the centre.</param>
    /// <param name="hotspotY">The click point's y within the image. Defaults to the centre.</param>
    /// <param name="color">Stroke/fill colour. A CSS color; <c>currentColor</c> does not work inside a cursor. Defaults to black.</param>
    /// <param name="halo">
    /// A contrasting backing drawn behind the glyph, the way the OS hands pair a white body with a
    /// dark line. Without one an outline icon is bare 2px strokes with a see-through interior -
    /// legible over a plain page, invisible over busy or same-coloured content. A CSS color;
    /// <see langword="null"/> (the default) draws none.
    /// </param>
    /// <param name="fallback">The keyword cursor used when the image can't be shown. Defaults to <c>grab</c>.</param>
    public static string From(
        Icon icon, int size = 24, int? hotspotX = null, int? hotspotY = null,
        string color = "black", string? halo = null, string fallback = "grab")
    {
        if (string.IsNullOrEmpty(icon.Body)) return fallback;

        string body;
        if (icon.Kind == IconKind.Outline)
        {
            // The halo is the same strokes drawn first and much fatter - AND filled: the fat pass
            // alone still leaves the regions between distant subpaths (a palm between fingers)
            // transparent, while filling auto-closes each subpath and covers them.
            var haloPass = halo is null
                ? ""
                : $"<g fill=\"{halo}\" stroke=\"{halo}\" stroke-width=\"5.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{icon.Body}</g>";
            body =
                haloPass +
                $"<g fill=\"none\" stroke=\"{color}\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{icon.Body}</g>";
        }
        else
        {
            // paint-order puts the stroke UNDER the fill: an even rim, not a fattened glyph.
            var haloPaint = halo is null ? "" : $" stroke=\"{halo}\" stroke-width=\"2.5\" paint-order=\"stroke\" stroke-linejoin=\"round\"";
            body = $"<g fill=\"{color}\"{haloPaint}>{icon.Body}</g>";
        }

        var svg =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" width=\"{size}\" height=\"{size}\">{body}</svg>";
        var x = hotspotX ?? size / 2;
        var y = hotspotY ?? size / 2;
        return $"url(\"data:image/svg+xml,{Uri.EscapeDataString(svg)}\") {x} {y}, {fallback}";
    }
}
