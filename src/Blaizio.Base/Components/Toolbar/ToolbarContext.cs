namespace Blaizio;

/// <summary>
/// Cascaded by <see cref="BaseToolbar"/> to everything inside it. Items read
/// <paramref name="Disabled"/> to disable themselves with the whole bar, and
/// <paramref name="Orientation"/> to lay themselves out (a separator, for instance, runs across the
/// toolbar's axis). A <see cref="BaseToggleGroup"/> that finds this context hands its keyboard
/// navigation over to the toolbar's roving focus instead of running its own - one composite, one
/// tab stop, seamless arrow travel across the whole bar.
/// </summary>
/// <param name="Disabled">Whether the whole toolbar is disabled.</param>
/// <param name="Orientation">The toolbar's navigation and layout axis.</param>
public sealed record ToolbarContext(bool Disabled, Orientation Orientation);
