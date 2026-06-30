using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// State a <see cref="BaseCommand"/> cascades to its input, list, groups, and items: the current search
/// text, whether a filter is currently narrowing the list, the ARIA ids wiring input to listbox, and
/// the callbacks items use to register themselves and ask whether they (or their group) currently
/// match. Focus stays in the input - the highlighted option is virtual (<c>aria-activedescendant</c>),
/// owned DOM-side by <c>ts/command.js</c>; C# owns only the search and which items match.
/// </summary>
/// <param name="Search">The current search text.</param>
/// <param name="IsFiltering">Whether a search is actively narrowing the list (filtering on and search non-empty).</param>
/// <param name="Loop">Whether arrow navigation wraps past the ends.</param>
/// <param name="Label">Accessible label for the input and listbox, or <see langword="null"/>.</param>
/// <param name="ListId">Id of the listbox element (the input's <c>aria-controls</c> and the list's <c>id</c>).</param>
/// <param name="InputId">Id of the input element (so the list module can drive its <c>aria-activedescendant</c>).</param>
/// <param name="SearchChanged">Raised by the input with the new text on every keystroke.</param>
/// <param name="Register">Registers (or updates) an item's searchable data so the root can filter it.</param>
/// <param name="Unregister">Drops an item from the registry when it leaves the tree.</param>
/// <param name="IsItemVisible">Whether the item with the given id currently matches the search.</param>
/// <param name="IsGroupVisible">Whether the group with the given id has any matching item (always true when not filtering).</param>
/// <param name="ResultCount">How many items currently match - drives the empty state.</param>
public sealed record CommandContext(
    string Search,
    bool IsFiltering,
    bool Loop,
    string? Label,
    string ListId,
    string InputId,
    EventCallback<string?> SearchChanged,
    Action<CommandItemRegistration> Register,
    Action<string> Unregister,
    Func<string, bool> IsItemVisible,
    Func<string, bool> IsGroupVisible,
    Func<int> ResultCount);

/// <summary>
/// An item's searchable data, registered with the <see cref="BaseCommand"/> root so it can decide
/// whether the item matches the current search and which group it belongs to.
/// </summary>
/// <param name="Id">The item's stable element id (also its <c>aria-activedescendant</c> target).</param>
/// <param name="Value">The text the filter matches against (and the payload passed to the item's select callback).</param>
/// <param name="Keywords">Extra terms the filter also matches against, or <see langword="null"/>.</param>
/// <param name="GroupId">The id of the enclosing group, or <see langword="null"/> when the item is ungrouped.</param>
public sealed record CommandItemRegistration(
    string Id,
    string Value,
    IReadOnlyList<string>? Keywords,
    string? GroupId);
