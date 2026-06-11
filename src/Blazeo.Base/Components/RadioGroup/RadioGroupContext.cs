using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// State a <see cref="BlazeRadioGroup"/> cascades to its <see cref="BlazeRadioGroupItem"/>s: the
/// selected value, the group's disabled state, and the callback an item invokes to select itself.
/// </summary>
/// <param name="Value">The currently selected item value (<see langword="null"/> when none).</param>
/// <param name="Disabled">Whether the whole group is disabled.</param>
/// <param name="Select">Invoked by an item (with its value) to become the selection.</param>
public sealed record RadioGroupContext(string? Value, bool Disabled, EventCallback<string> Select)
{
    /// <summary>Whether <paramref name="itemValue"/> is the selected one.</summary>
    public bool IsChecked(string itemValue) => Value == itemValue;
}
