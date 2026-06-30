using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// State a <see cref="BaseCombobox"/> cascades to its input, content, list, groups, items, value, chips,
/// trigger, and clear button. A combobox fuses two engines, so this context carries both: the
/// <i>selection</i> (one value when single, a list when <see cref="Multiple"/>) and open state, like a
/// <see cref="BaseSelect"/>; and the <i>query</i> + item registry that filter the list, like a
/// <see cref="BaseCommand"/>. Focus stays in the input the whole time - the highlighted option is virtual
/// (<c>aria-activedescendant</c>), owned DOM-side by <c>ts/combobox.js</c>; C# owns the query, the
/// selection, the open state, and which items match.
/// </summary>
/// <param name="Value">The single selected value, or <see langword="null"/> when nothing is selected or when <see cref="Multiple"/>.</param>
/// <param name="Selected">Every selected value, in selection order (0 or 1 when single, 0..n when <see cref="Multiple"/>).</param>
/// <param name="Multiple">Whether more than one value can be selected at once (chips mode).</param>
/// <param name="IsSelected">Whether the given value is currently selected.</param>
/// <param name="Select">Picks a value: single sets it, sets the query to it, and closes; <see cref="Multiple"/> toggles it, clears the query, and stays open.</param>
/// <param name="Deselect">Removes a value from the selection (a chip's remove button).</param>
/// <param name="ClearSelection">Clears the whole selection and the query, then refocuses the input (the clear button).</param>
/// <param name="FocusInput">Moves focus to (and selects the text of) the input - used after clearing or removing a chip.</param>
/// <param name="Open">Whether the popup is open.</param>
/// <param name="RequestOpen">Opens the popup (input focus / typing / ArrowDown / trigger).</param>
/// <param name="RequestClose">Closes the popup (Escape / Tab / outside press / single-select pick).</param>
/// <param name="SetOpen">Sets the open state directly (the trigger toggles with it).</param>
/// <param name="Query">The current input text - both what shows in the input and what filters the list.</param>
/// <param name="Typed">Whether the input currently holds an active search query (the user has typed since the last open/select, or the query is controlled) rather than echoing the chosen value. Drives whether a single-select input shows the query or the selected label, and whether the list filters.</param>
/// <param name="IsFiltering">Whether a query is actively narrowing the list (filtering on, the user is typing, and the query is non-empty).</param>
/// <param name="AutoHighlight">Whether the first match is highlighted automatically (else nothing until an arrow).</param>
/// <param name="Loop">Whether arrow navigation wraps past the ends.</param>
/// <param name="QueryChanged">Raised by the input with the new text on every keystroke.</param>
/// <param name="Register">Registers (or updates) an item's searchable data so the root can filter it.</param>
/// <param name="Unregister">Drops an item from the registry when it leaves the tree.</param>
/// <param name="IsItemVisible">Whether the item with the given id currently matches the query.</param>
/// <param name="IsGroupVisible">Whether the group with the given id has any matching item (always true when not filtering).</param>
/// <param name="ResultCount">How many items currently match - drives the empty state.</param>
/// <param name="ListId">Id of the listbox element (the input's <c>aria-controls</c> and the list's <c>id</c>).</param>
/// <param name="InputId">Id of the input element (so the list module can drive its <c>aria-activedescendant</c>).</param>
/// <param name="AnchorId">Value of the anchor's <c>data-bz-combobox-anchor</c> hook; the content positions + dismisses against it.</param>
/// <param name="Label">Accessible label for the input and listbox, or <see langword="null"/>.</param>
/// <param name="Disabled">Whether the whole combobox is disabled.</param>
public sealed record ComboboxContext(
    string? Value,
    IReadOnlyList<string> Selected,
    bool Multiple,
    Func<string, bool> IsSelected,
    EventCallback<string> Select,
    EventCallback<string> Deselect,
    EventCallback ClearSelection,
    EventCallback FocusInput,
    bool Open,
    EventCallback RequestOpen,
    EventCallback RequestClose,
    EventCallback<bool> SetOpen,
    string Query,
    bool Typed,
    bool IsFiltering,
    bool AutoHighlight,
    bool Loop,
    EventCallback<string?> QueryChanged,
    Action<ComboboxItemRegistration> Register,
    Action<string> Unregister,
    Func<string, bool> IsItemVisible,
    Func<string, bool> IsGroupVisible,
    Func<int> ResultCount,
    string ListId,
    string InputId,
    string AnchorId,
    string? Label,
    bool Disabled)
{
    /// <summary>Whether anything is selected at all.</summary>
    public bool HasSelection => Selected.Count > 0;
}

/// <summary>
/// An item's searchable data, registered with the <see cref="BaseCombobox"/> root so it can decide whether
/// the item matches the current query and which group it belongs to.
/// </summary>
/// <param name="Id">The item's stable element id (also its <c>aria-activedescendant</c> target).</param>
/// <param name="Value">The value selected when the item is chosen, and the text the filter matches against.</param>
/// <param name="Keywords">Extra terms the filter also matches against, or <see langword="null"/>.</param>
/// <param name="GroupId">The id of the enclosing group, or <see langword="null"/> when the item is ungrouped.</param>
public sealed record ComboboxItemRegistration(
    string Id,
    string Value,
    IReadOnlyList<string>? Keywords,
    string? GroupId);

/// <summary>
/// Cascaded by a <see cref="BaseComboboxItem"/> to the <see cref="BaseComboboxItemIndicator"/> nested inside
/// it, so the indicator can show (or hide) its check based on whether the item is selected.
/// </summary>
/// <param name="Selected">Whether the enclosing item is currently selected.</param>
public sealed record ComboboxItemContext(bool Selected);

/// <summary>
/// Cascaded by a <see cref="BaseComboboxChip"/> to the <see cref="BaseComboboxChipRemove"/> nested inside it,
/// so the remove button knows which value to drop from the selection.
/// </summary>
/// <param name="Value">The value this chip represents.</param>
public sealed record ComboboxChipContext(string Value);
