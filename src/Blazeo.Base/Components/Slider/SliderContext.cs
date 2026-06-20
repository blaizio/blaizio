using System.Linq;

namespace Blazeo;

/// <summary>
/// State a <see cref="BzSlider"/> cascades to its <see cref="BzSliderTrack"/>,
/// <see cref="BzSliderRange"/> and <see cref="BzSliderThumb"/> parts: the current thumb values, the
/// <c>[Min, Max]</c> domain and <see cref="Step"/>, the axis, and the disabled/inverted flags. The
/// parts read it to mirror <c>data-orientation</c>/<c>data-disabled</c> and to place themselves -
/// fractions are computed here so every part agrees on the mapping.
/// </summary>
/// <param name="Values">The current value of each thumb, in thumb order.</param>
/// <param name="Min">Domain minimum (the start of the track).</param>
/// <param name="Max">Domain maximum (the end of the track).</param>
/// <param name="Step">The granularity values snap to.</param>
/// <param name="Orientation">The layout/navigation axis.</param>
/// <param name="Disabled">Whether the whole slider is disabled.</param>
/// <param name="Inverted">Whether values increase toward the start side instead of the end.</param>
public sealed record SliderContext(
    IReadOnlyList<double> Values,
    double Min,
    double Max,
    double Step,
    Orientation Orientation,
    bool Disabled,
    bool Inverted)
{
    // Guard a zero/negative domain so fractions stay finite (a degenerate slider just pins to 0).
    private double Span => Max > Min ? Max - Min : 1;

    /// <summary>
    /// The position fraction (0..1, measured from the <i>start</i> side of the track) for a raw
    /// value, already adjusted for <see cref="Inverted"/>. The styled layer turns this into an offset.
    /// </summary>
    public double FractionForValue(double value)
    {
        var fraction = Math.Clamp((value - Min) / Span, 0, 1);
        return Inverted ? 1 - fraction : fraction;
    }

    /// <summary>
    /// The filled span as <c>(start, end)</c> fractions from the start side. A single thumb fills
    /// from the track origin to the thumb (flipped when inverted); multiple thumbs fill the gap
    /// between the extreme thumbs.
    /// </summary>
    public (double Start, double End) RangeFractions()
    {
        if (Values.Count == 0) return (0, 0);

        if (Values.Count == 1)
        {
            var f = FractionForValue(Values[0]);
            return Inverted ? (f, 1) : (0, f);
        }

        var fractions = Values.Select(FractionForValue).ToArray();
        return (fractions.Min(), fractions.Max());
    }
}
