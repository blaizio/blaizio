using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// State a <see cref="BaseHoverCard"/> cascades to its trigger and content: the open state, the content
/// id, the anchor hook the content positions against, and the open/close intents raised on
/// hover/focus (plus an immediate dismiss for Escape / an outside pointer-down).
/// </summary>
/// <param name="Open">Whether the hover card is open.</param>
/// <param name="ContentId">Id of the content element.</param>
/// <param name="AnchorId">Value of the trigger's <c>data-bz-hover-card-anchor</c> hook; the content resolves it to position against the trigger.</param>
/// <param name="OpenDelayed">Raised when the pointer enters / focus lands on the trigger or content - opens after the open delay (and cancels a pending close, so moving onto the card holds it open).</param>
/// <param name="CloseDelayed">Raised when the pointer leaves / focus blurs the trigger or content - closes after the close delay, long enough to cross the gap between them.</param>
/// <param name="Dismiss">Raised by Escape or an outside pointer-down - closes at once.</param>
public sealed record HoverCardContext(
    bool Open,
    string ContentId,
    string AnchorId,
    EventCallback OpenDelayed,
    EventCallback CloseDelayed,
    EventCallback Dismiss);
