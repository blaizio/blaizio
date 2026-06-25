using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// State a <see cref="BaseDropdownMenu"/> cascades to its trigger, content, and every item: the open
/// state, how focus should land on open, the ARIA ids wiring trigger to content, the anchor hook the
/// content positions + dismisses against, and the open/close requests.
/// </summary>
/// <param name="Open">Whether the menu is open.</param>
/// <param name="FocusIntent">Where the content should place focus when it mounts.</param>
/// <param name="ContentId">Id of the content element (for the trigger's <c>aria-controls</c> and the content's <c>id</c>).</param>
/// <param name="TriggerId">Id of the trigger (for the content's <c>aria-labelledby</c>).</param>
/// <param name="AnchorId">Value of the trigger's <c>data-bz-dropdown-menu-anchor</c> hook; the content resolves it to position against the trigger.</param>
/// <param name="RequestOpen">Opens the menu with the given focus intent (trigger click / Arrow keys).</param>
/// <param name="RequestClose">Closes the whole menu (item select / Escape / Tab / outside press).</param>
public sealed record DropdownMenuContext(
    bool Open,
    MenuFocusIntent FocusIntent,
    string ContentId,
    string TriggerId,
    string AnchorId,
    EventCallback<MenuFocusIntent> RequestOpen,
    EventCallback RequestClose);
