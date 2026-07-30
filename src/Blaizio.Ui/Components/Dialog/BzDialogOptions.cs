using Blaizio;

namespace Blaizio.Ui;

/// <summary>
/// Styled-layer options for an imperatively shown dialog - extends the headless <see cref="DialogOptions"/>
/// with skin choices. Pass to <see cref="IDialogService"/> methods.
/// </summary>
public record BzDialogOptions : DialogOptions
{
    /// <summary>Max-width preset for the alert skin. Defaults to <see cref="AlertDialogSize.Default"/>.</summary>
    public AlertDialogSize Size { get; init; } = AlertDialogSize.Default;

    /// <summary>Show the corner close button (non-alert dialogs only). Defaults to <see langword="true"/>.</summary>
    public bool ShowCloseButton { get; init; } = true;

    /// <summary>Extra classes merged onto the dialog window (e.g. a wider <c>sm:max-w-lg</c>, or a theme-pin class). Non-alert dialogs.</summary>
    public string? ContentClass { get; init; }

    /// <summary>Render the alert-dialog skin (non-dismissable, no corner close). Set by <see cref="DialogServiceExtensions.ConfirmAsync"/>.</summary>
    public bool Alert { get; init; }

    /// <summary>The confirm button label (alert skin). Defaults to "Continue".</summary>
    public string ConfirmText { get; init; } = "Continue";

    /// <summary>The cancel button label (alert skin). Defaults to "Cancel".</summary>
    public string CancelText { get; init; } = "Cancel";

    /// <summary>The confirm button variant. Use <see cref="ButtonVariant.Destructive"/> for deletes.</summary>
    public ButtonVariant ConfirmVariant { get; init; } = ButtonVariant.Default;
}
