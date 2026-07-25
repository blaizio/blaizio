namespace Blaizio.Ui;

/// <summary>
/// One named row of a <see cref="BzColorPalette"/> - a label and the shades under it. Rows with the
/// same number of shades line up column by column, the way a design-system palette reads.
/// </summary>
/// <param name="Label">The row heading, e.g. <c>"Blue"</c>.</param>
/// <param name="Colors">The shades, light to dark - any parseable CSS color strings.</param>
public sealed record ColorPaletteGroup(string Label, IReadOnlyList<string> Colors);
