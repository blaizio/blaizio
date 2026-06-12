using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// State a <see cref="BlazeCollapsible"/> cascades to its trigger and content: the open state,
/// the content id (for <c>aria-controls</c>), the disabled state, and the toggle callback.
/// </summary>
/// <param name="Open">Whether the content is expanded.</param>
/// <param name="Disabled">Whether toggling is blocked.</param>
/// <param name="ContentId">Id of the content element.</param>
/// <param name="Toggle">Invoked by the trigger to flip the state.</param>
public sealed record CollapsibleContext(bool Open, bool Disabled, string ContentId, EventCallback Toggle);
