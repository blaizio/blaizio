using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// State a <see cref="BzDropdownMenuSub"/> cascades to its sub-trigger and sub-content. Mirrors
/// <see cref="DropdownMenuContext"/> but drives one nested level: the submenu's own open state and
/// anchor. It is a distinct type on purpose - items keep reading the root
/// <see cref="DropdownMenuContext"/> so selecting one closes the <i>whole</i> menu, not just this level.
/// </summary>
/// <param name="Open">Whether this submenu is open.</param>
/// <param name="FocusIntent">Where the sub-content should place focus when it mounts.</param>
/// <param name="ContentId">Id of the sub-content element (for the sub-trigger's <c>aria-controls</c>).</param>
/// <param name="TriggerId">Id of the sub-trigger (for the sub-content's <c>aria-labelledby</c>).</param>
/// <param name="AnchorId">Value of the sub-trigger's <c>data-bz-dropdown-menu-sub-anchor</c> hook; the sub-content positions against it.</param>
/// <param name="RequestOpen">Opens the submenu with the given focus intent, cancelling any pending close (hover / ArrowRight / Enter).</param>
/// <param name="RequestClose">Closes just this submenu immediately (ArrowLeft).</param>
/// <param name="ScheduleClose">Closes this submenu after a short grace delay (pointer left the trigger or content), so moving across the gap between them doesn't dismiss it.</param>
public sealed record DropdownMenuSubContext(
    bool Open,
    MenuFocusIntent FocusIntent,
    string ContentId,
    string TriggerId,
    string AnchorId,
    EventCallback<MenuFocusIntent> RequestOpen,
    EventCallback RequestClose,
    EventCallback ScheduleClose);
