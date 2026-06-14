namespace Blazeo;

/// <summary>Which Tabler family an <see cref="Icon"/> is, deciding how it is painted.</summary>
public enum IconKind
{
    /// <summary>Stroked paths (<c>fill:none</c>, <c>stroke:currentColor</c>) - Tabler's default style.</summary>
    Outline,

    /// <summary>Solid paths (<c>fill:currentColor</c>, no stroke).</summary>
    Filled,
}

/// <summary>
/// A single icon: the inner SVG markup plus the family that decides its paint. The generated
/// <c>Icons.Outline.*</c> / <c>Icons.Filled.*</c> members are <see cref="Icon"/>-valued and feed
/// <c>BzIcon</c>; a <see langword="default"/> value (null <see cref="Body"/>) renders nothing.
/// </summary>
/// <remarks>
/// A <see langword="readonly record struct"/> so it is allocation-free and self-describing - this is
/// what lets the generated members be trim-friendly <b>expression-bodied properties</b> (no static
/// constructor over the whole family, so the IL trimmer drops the icons a consumer never references).
/// </remarks>
/// <param name="Body">The inner SVG markup (the <c>&lt;path&gt;</c> elements), without the wrapping <c>&lt;svg&gt;</c>.</param>
/// <param name="Kind">The family (outline / filled) that selects stroke vs fill rendering.</param>
public readonly record struct Icon(string Body, IconKind Kind);
