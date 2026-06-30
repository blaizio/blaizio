using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// State a <see cref="BaseDropdownMenuSub"/> cascades to its sub-trigger and sub-content. Mirrors
/// <see cref="DropdownMenuContext"/> but drives one nested level: the submenu's own open state and
/// anchor. It is a distinct type on purpose - items keep reading the root
/// <see cref="DropdownMenuContext"/> so selecting one closes the <i>whole</i> menu, not just this level.
/// </summary>
/// <param name="Open">Whether this submenu is open.</param>
/// <param name="FocusIntent">Where the sub-content should place focus when it mounts.</param>
/// <param name="ContentId">Id of the sub-content element (for the sub-trigger's <c>aria-controls</c>).</param>
/// <param name="TriggerId">Id of the sub-trigger (for the sub-content's <c>aria-labelledby</c>).</param>
/// <param name="AnchorId">Value of the sub-trigger's <c>data-bz-dropdown-menu-sub-anchor</c> hook; the sub-content positions against it.</param>
/// <param name="RequestOpen">Opens the submenu immediately with the given focus intent (click / ArrowRight / Enter), cancelling any pending hover-open.</param>
/// <param name="RequestClose">Closes just this submenu immediately (the inline-start arrow, or focus leaving it for a sibling - both driven from ts/menu.js).</param>
/// <param name="ScheduleOpen">Opens this submenu after a short hover delay, so brushing the pointer past the trigger doesn't flash it open.</param>
/// <param name="CancelOpen">Cancels a pending hover-open (the pointer left the trigger before the delay elapsed).</param>
public sealed record DropdownMenuSubContext(
    bool Open,
    MenuFocusIntent FocusIntent,
    string ContentId,
    string TriggerId,
    string AnchorId,
    EventCallback<MenuFocusIntent> RequestOpen,
    EventCallback RequestClose,
    EventCallback ScheduleOpen,
    EventCallback CancelOpen);
