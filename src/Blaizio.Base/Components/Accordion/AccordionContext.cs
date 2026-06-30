using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// State a <see cref="BaseAccordion"/> cascades to its items: the expansion mode, the open
/// values, the id scheme wiring triggers to panels, and the toggle callback.
/// </summary>
/// <param name="Type">The expansion mode (single / multiple).</param>
/// <param name="Values">The item values currently open (zero or one of them when single).</param>
/// <param name="Disabled">Whether the whole accordion is disabled.</param>
/// <param name="BaseId">Unique prefix for this accordion's trigger/content ids.</param>
/// <param name="Toggle">Invoked by a trigger (with its item's value) to toggle it.</param>
public sealed record AccordionContext(
    AccordionType Type,
    IReadOnlyList<string> Values,
    bool Disabled,
    string BaseId,
    EventCallback<string> Toggle)
{
    /// <summary>Whether the item with <paramref name="itemValue"/> is open.</summary>
    public bool IsOpen(string itemValue) => Values.Contains(itemValue);

    /// <summary>The trigger element id for <paramref name="itemValue"/>.</summary>
    public string TriggerId(string itemValue) => $"{BaseId}-trigger-{itemValue}";

    /// <summary>The content element id for <paramref name="itemValue"/>.</summary>
    public string ContentId(string itemValue) => $"{BaseId}-content-{itemValue}";
}

/// <summary>
/// Per-item state a <see cref="BaseAccordionItem"/> cascades to its header, trigger, and content.
/// </summary>
/// <param name="Value">The item's value.</param>
/// <param name="Open">Whether this item is expanded.</param>
/// <param name="Disabled">Whether this item (or the whole accordion) is disabled.</param>
public sealed record AccordionItemContext(string Value, bool Open, bool Disabled);
