namespace Blazeo;

/// <summary>
/// Generates stable, collision-resistant ids for wiring ARIA relationships
/// (<c>aria-controls</c>, <c>aria-labelledby</c>, <c>for</c>/<c>id</c> pairs, …).
/// </summary>
/// <remarks>
/// Create the id once (e.g. in <c>OnInitialized</c>) and reuse it across renders — do not call
/// <see cref="Create"/> inside the render path or the id will churn every frame.
/// </remarks>
public static class Identifier
{
    /// <summary>Creates an id of the form <c>{prefix}-{12 hex chars}</c>, e.g. <c>blaze-9f3c1a2b4d5e</c>.</summary>
    public static string Create(string prefix = "blaze") => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 1 + 12)];
}
