namespace Blazeo;

/// <summary>
/// State a <see cref="BlazeProgress"/> cascades to its <see cref="BlazeProgressIndicator"/> so the
/// indicator can mirror the bar's <c>data-state</c>/<c>data-value</c>/<c>data-max</c> - the
/// progress bar's shared context.
/// </summary>
/// <param name="Value">The current value, or <see langword="null"/> when indeterminate.</param>
/// <param name="Max">The maximum value (the 100% point).</param>
public sealed record ProgressContext(double? Value, double Max);
