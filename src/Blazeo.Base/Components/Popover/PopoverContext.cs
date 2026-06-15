using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// State a <see cref="BzPopover"/> cascades to its trigger, content, and close button: the open
/// state, the ARIA content id, the anchor hook the content positions against, and the state setter.
/// </summary>
/// <param name="Open">Whether the popover is open.</param>
/// <param name="ContentId">Id of the content element (for the trigger's <c>aria-controls</c>).</param>
/// <param name="AnchorId">Value of the trigger's <c>data-bz-popover-anchor</c> hook; the content resolves it to position against the trigger.</param>
/// <param name="SetOpen">Invoked by trigger / close button / Escape / outside-dismiss to change the state.</param>
public sealed record PopoverContext(
    bool Open,
    string ContentId,
    string AnchorId,
    EventCallback<bool> SetOpen);
