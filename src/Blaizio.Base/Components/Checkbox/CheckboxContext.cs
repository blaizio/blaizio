namespace Blaizio;

/// <summary>
/// State a <see cref="BaseCheckbox"/> cascades to its <see cref="BaseCheckboxIndicator"/> so the
/// indicator can mirror the checkbox's state and only render while checked/indeterminate - the
/// checkbox's shared context.
/// </summary>
/// <param name="State">The current tri-state value.</param>
/// <param name="Disabled">Whether the checkbox is disabled.</param>
public sealed record CheckboxContext(CheckedState State, bool Disabled);
