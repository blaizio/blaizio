namespace Blaizio.Ui;

/// <summary>
/// The stacking layer a surface sits in, cascaded by the modal surfaces (dialog, alert dialog,
/// sheet, drawer) to everything rendered inside them.
/// </summary>
/// <remarks>
/// <para>
/// Popups portal to the document body, so a select opened inside a dialog is a stacking SIBLING of
/// that dialog rather than a descendant - their z-indices do not compose, they compete. With the
/// popups fixed at the base layer and <c>BzDialogProvider</c> stacking each dialog 10 above the
/// last (60/61, 70/71, ...), a popup inside an imperatively shown dialog painted BEHIND it and was
/// simply invisible.
/// </para>
/// <para>
/// Cascading solves it where the DOM cannot: a portal moves the element, not the component tree, so
/// a cascaded value still reaches a popup that has been re-parented to the body. Each modal surface
/// publishes the layer it occupies; a popup rendered inside one lifts itself to
/// <see cref="PopupZ"/>, which clears that dialog's window but stays below the next dialog up - so
/// a popup opened in the first of two stacked dialogs correctly sits under the second.
/// </para>
/// </remarks>
/// <param name="Z">The overlay's z-index. The surface's own window sits one above this.</param>
public sealed record OverlayLayer(int Z)
{
    /// <summary>The cascade's name; matched by the <c>[CascadingParameter]</c> on every popup.</summary>
    public const string CascadeName = "BzOverlayLayer";

    /// <summary>
    /// The base layer, for surfaces rendered outside any modal (a declarative dialog leaves its
    /// z-index to the stylesheet's <c>z-50</c>).
    /// </summary>
    public const int Base = 50;

    /// <summary>How far a popup lifts above its layer's overlay: clear of the window
    /// (<c>Z + 1</c>) and short of the next stacked surface (<c>Z + 10</c>).</summary>
    private const int PopupOffset = 5;

    /// <summary>Where a popup opened inside this layer belongs.</summary>
    public int PopupZ => Z + PopupOffset;

    /// <summary>
    /// The inline <c>z-index</c> a popup needs to clear <paramref name="layer"/>, or
    /// <see langword="null"/> outside any modal surface - the stylesheet's own class is right there
    /// and an inline value would only fight it.
    /// </summary>
    public static string? PopupStyle(OverlayLayer? layer) =>
        layer is null || layer.Z <= Base ? null : $"z-index:{layer.PopupZ}";
}
