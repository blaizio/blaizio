namespace Blaizio;

/// <summary>
/// State a <see cref="BaseColorArea"/> cascades to its <see cref="BaseColorAreaThumb"/>: the
/// current position pair and the disabled flag. Both coordinates are fractions in <c>[0, 1]</c> -
/// <see cref="X"/> from the left edge, <see cref="Y"/> from the BOTTOM edge (mapping directly
/// onto HSV: x = saturation, y = value).
/// </summary>
/// <param name="X">Horizontal fraction, 0 at the left edge.</param>
/// <param name="Y">Vertical fraction, 0 at the bottom edge.</param>
/// <param name="Disabled">Whether the whole surface is disabled.</param>
public sealed record ColorAreaContext(double X, double Y, bool Disabled);
