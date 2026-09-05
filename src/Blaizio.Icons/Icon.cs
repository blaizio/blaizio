namespace Blaizio;

/// <summary>How an <see cref="Icon"/> is painted: stroked paths or solid fills.</summary>
public enum IconKind
{
    /// <summary>Stroked paths (<c>fill:none</c>, <c>stroke:currentColor</c>) - Tabler's, Lucide's and HugeIcons' style.</summary>
    Outline,

    /// <summary>Solid paths (<c>fill:currentColor</c>, no stroke) - Tabler Filled, every Phosphor weight, both Remix families.</summary>
    Filled,
}

/// <summary>
/// A single icon: the inner SVG markup plus what decides its paint and grid. The generated
/// members of every set (<c>Icons.Outline.*</c> for Tabler, <c>Lucide.Outline.*</c>,
/// <c>Phosphor.Regular.*</c>, <c>Remix.Line.*</c>, <c>HugeIcons.StrokeRounded.*</c>) are
/// <see cref="Icon"/>-valued and feed <c>BzIcon</c>; a <see langword="default"/> value (null
/// <see cref="Body"/>) renders nothing.
/// </summary>
/// <remarks>
/// A <see langword="readonly record struct"/> so it is allocation-free and self-describing - this is
/// what lets the generated members be trim-friendly <b>expression-bodied properties</b> (no static
/// constructor over the whole family, so the IL trimmer drops the icons a consumer never references).
/// </remarks>
/// <param name="Body">The inner SVG markup (the <c>&lt;path&gt;</c> elements), without the wrapping <c>&lt;svg&gt;</c>.</param>
/// <param name="Kind">The paint model (outline / filled) that selects stroke vs fill rendering.</param>
/// <param name="ViewBox">The set's grid. Tabler, Lucide, Remix and HugeIcons draw on <c>0 0 24 24</c>; Phosphor on <c>0 0 256 256</c>.</param>
/// <param name="StrokeWidth">The set's stroke width for outline paint (Tabler and Lucide 2, HugeIcons 1.5). Ignored for filled icons.</param>
public readonly record struct Icon(string Body, IconKind Kind, string ViewBox = "0 0 24 24", float StrokeWidth = 2f);
