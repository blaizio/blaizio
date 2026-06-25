namespace Blazeo;

/// <summary>
/// The slice of rows a <see cref="BaseWindowVirtualizer"/> currently wants mounted, plus the spacer
/// heights that stand in for the rows above and below it. The host renders a top spacer of
/// <see cref="PaddingTop"/>, the rows in <see cref="Indices"/>, then a bottom spacer of
/// <see cref="PaddingBottom"/> - so the scroll height stays exact while only the visible band is real.
/// </summary>
/// <param name="Start">Index of the first row to mount (inclusive).</param>
/// <param name="End">Index just past the last row to mount (exclusive).</param>
/// <param name="PaddingTop">Pixel height of the spacer standing in for rows <c>[0, Start)</c>.</param>
/// <param name="PaddingBottom">Pixel height of the spacer standing in for rows <c>[End, Count)</c>.</param>
public readonly record struct VirtualWindow(int Start, int End, double PaddingTop, double PaddingBottom)
{
    /// <summary>The number of rows in the window.</summary>
    public int Length => End - Start;

    /// <summary>The mounted row indices, in order - iterate these to render the visible rows.</summary>
    public IEnumerable<int> Indices => Enumerable.Range(Start, Math.Max(0, End - Start));
}
