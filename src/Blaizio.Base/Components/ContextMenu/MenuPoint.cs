namespace Blaizio;

/// <summary>A viewport-relative point (the coordinates of a right-click), where a context menu opens.</summary>
/// <param name="X">Distance from the left edge of the viewport, in pixels.</param>
/// <param name="Y">Distance from the top edge of the viewport, in pixels.</param>
public readonly record struct MenuPoint(double X, double Y);
