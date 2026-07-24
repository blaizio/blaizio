namespace Blaizio;

/// <summary>
/// A position on a <see cref="BaseColorArea"/>: both coordinates are fractions in <c>[0, 1]</c>,
/// <see cref="X"/> from the left edge and <see cref="Y"/> from the BOTTOM edge, so the pair maps
/// directly onto HSV saturation (x) and value (y).
/// </summary>
/// <param name="X">Horizontal fraction, 0 at the left edge.</param>
/// <param name="Y">Vertical fraction, 0 at the bottom edge.</param>
public readonly record struct ColorAreaPoint(double X, double Y);
