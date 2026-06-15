using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// State a <see cref="BzTooltip"/> cascades to its trigger and content: the open state, the ARIA
/// content id, the anchor hook the content positions against, and the open/close intents the
/// trigger raises on hover/focus.
/// </summary>
/// <param name="Open">Whether the tooltip is open.</param>
/// <param name="Disabled">Whether the tooltip is disabled - it never opens.</param>
/// <param name="ContentId">Id of the content element (for the trigger's <c>aria-describedby</c>).</param>
/// <param name="AnchorId">Value of the trigger's <c>data-bz-tooltip-anchor</c> hook; the content resolves it to position against the trigger.</param>
/// <param name="OpenDelayed">Raised when the pointer enters the trigger - opens after the delay.</param>
/// <param name="OpenImmediate">Raised when the trigger gains focus - opens at once.</param>
/// <param name="Close">Raised on pointer-leave / blur / Escape - closes.</param>
public sealed record TooltipContext(
    bool Open,
    bool Disabled,
    string ContentId,
    string AnchorId,
    EventCallback OpenDelayed,
    EventCallback OpenImmediate,
    EventCallback Close);
