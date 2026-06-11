namespace Blazeo;

/// <summary>
/// State a <see cref="BlazeCheckbox"/> cascades to its <see cref="BlazeCheckboxIndicator"/> so the
/// indicator can mirror the checkbox's state and only render while checked/indeterminate — the
/// faithful analogue of Radix's Checkbox context.
/// </summary>
/// <param name="State">The current tri-state value.</param>
/// <param name="Disabled">Whether the checkbox is disabled.</param>
public sealed record CheckboxContext(CheckedState State, bool Disabled);
