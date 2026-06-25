using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// State a <see cref="BaseDialog"/> cascades to its trigger, overlay, content, title,
/// description, and close button: the open state, modality, the ARIA wiring ids, and the
/// state setter.
/// </summary>
/// <param name="Open">Whether the dialog is open.</param>
/// <param name="Modal">Whether the dialog blocks interaction with the rest of the page.</param>
/// <param name="PreventDismiss">When true, Escape and outside-click do not close the dialog (an explicit close still does).</param>
/// <param name="ContentId">Id of the content element (for <c>aria-controls</c>).</param>
/// <param name="TitleId">Id of the title element (for <c>aria-labelledby</c>).</param>
/// <param name="DescriptionId">Id of the description element (for <c>aria-describedby</c>).</param>
/// <param name="SetOpen">Invoked by trigger/close/overlay/Escape to change the state.</param>
public sealed record DialogContext(
    bool Open,
    bool Modal,
    bool PreventDismiss,
    string ContentId,
    string TitleId,
    string DescriptionId,
    EventCallback<bool> SetOpen);
