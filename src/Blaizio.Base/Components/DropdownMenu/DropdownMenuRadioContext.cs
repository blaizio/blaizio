using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// State a <see cref="BaseDropdownMenuRadioGroup"/> cascades to its <see cref="BaseDropdownMenuRadioItem"/>s:
/// the selected value and the callback an item invokes to select itself.
/// </summary>
/// <param name="Value">The currently selected item value (<see langword="null"/> when none).</param>
/// <param name="Select">Invoked by an item (with its value) to become the selection.</param>
public sealed record DropdownMenuRadioContext(string? Value, EventCallback<string> Select)
{
    /// <summary>Whether <paramref name="itemValue"/> is the selected one.</summary>
    public bool IsChecked(string itemValue) => Value == itemValue;
}
