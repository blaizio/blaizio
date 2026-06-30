namespace Blaizio;

/// <summary>
/// The mechanism-level options for an imperatively shown dialog - only what the headless host needs.
/// The styled layer extends this with skin choices (size, close button, ...); see the Ui
/// <c>UiDialogOptions</c>.
/// </summary>
public record DialogOptions
{
    /// <summary>Modal (the default): traps focus, locks page scroll, renders the overlay.</summary>
    public bool Modal { get; init; } = true;

    /// <summary>Pressing the backdrop dismisses the dialog. Defaults to <see langword="true"/>.</summary>
    public bool DismissOnOutsideClick { get; init; } = true;

    /// <summary>
    /// Start with implicit dismissal (Escape, backdrop, close button) blocked. Usually left
    /// <see langword="false"/> and toggled at runtime via <see cref="DialogInstance.PreventDismiss"/> -
    /// e.g. while a component runs a lengthy operation.
    /// </summary>
    public bool PreventDismiss { get; init; }
}
