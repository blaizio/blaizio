using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// State a <see cref="BaseToggleGroup"/> cascades to its <see cref="BaseToggleGroupItem"/>s: the
/// selection mode (which decides the items' ARIA contract), the values currently on, the group's
/// disabled state, and the callback an item invokes to flip itself.
/// </summary>
/// <param name="Type">The selection mode (single / multiple).</param>
/// <param name="Values">The item values currently on (zero or one of them when single).</param>
/// <param name="Disabled">Whether the whole group is disabled.</param>
/// <param name="Toggle">Invoked by an item (with its value) to toggle itself.</param>
public sealed record ToggleGroupContext(
    ToggleGroupType Type, IReadOnlyList<string> Values, bool Disabled, EventCallback<string> Toggle)
{
    /// <summary>Whether <paramref name="itemValue"/> is currently on.</summary>
    public bool IsOn(string itemValue) => Values.Contains(itemValue);
}
