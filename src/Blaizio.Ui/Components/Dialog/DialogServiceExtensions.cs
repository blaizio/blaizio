using Blaizio;
using Microsoft.AspNetCore.Components;

namespace Blaizio.Ui;

/// <summary>
/// Styled sugar over the headless <see cref="IDialogService"/> - lives in the copied layer (no DI
/// registration needed) because it renders styled components.
/// </summary>
public static class DialogServiceExtensions
{
    /// <summary>
    /// Shows a confirmation (alert) dialog. Returns <see langword="true"/> if the user confirmed, or
    /// <see langword="false"/> if they cancelled or dismissed it (Escape).
    /// </summary>
    public static async Task<bool> ConfirmAsync(
        this IDialogService dialogs, string title, string? description = null, BzDialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        var parameters = new Dictionary<string, object?>
        {
            [nameof(BzConfirmDialog.Title)] = title,
            [nameof(BzConfirmDialog.Description)] = description,
        };
        // The alert presentation is non-dismissable by backdrop; Escape still cancels (-> false).
        var result = await dialogs.ShowAsync<BzConfirmDialog>(parameters, (options ?? DefaultsOf(dialogs)) with { Alert = true });
        return result is { Cancelled: false, Value: true };
    }

    /// <summary>
    /// Shows <typeparamref name="TComponent"/> in the Sheet presentation, sliding in from <paramref name="side"/>.
    /// Same contract as <see cref="IDialogService.ShowAsync{TComponent}"/>: the component reads its
    /// cascaded <see cref="DialogInstance"/> to close itself with a result. An explicit
    /// <c>options.SheetSide</c> wins over <paramref name="side"/>.
    /// </summary>
    public static Task<DialogResult> ShowSheetAsync<TComponent>(
        this IDialogService dialogs,
        IReadOnlyDictionary<string, object?>? parameters = null,
        PanelSide side = PanelSide.End,
        BzDialogOptions? options = null) where TComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        return dialogs.ShowAsync<TComponent>(parameters, WithSheet(dialogs, options, side));
    }

    /// <summary>
    /// Shows an inline template in the Sheet presentation, sliding in from <paramref name="side"/>. The template
    /// receives the <see cref="DialogInstance"/> so it can close itself. An explicit
    /// <c>options.SheetSide</c> wins over <paramref name="side"/>.
    /// </summary>
    public static Task<DialogResult> ShowSheetAsync(
        this IDialogService dialogs,
        RenderFragment<DialogInstance> template,
        PanelSide side = PanelSide.End,
        BzDialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        return dialogs.ShowAsync(template, WithSheet(dialogs, options, side));
    }

    private static BzDialogOptions WithSheet(IDialogService dialogs, BzDialogOptions? options, PanelSide side) =>
        (options ?? DefaultsOf(dialogs)) with { SheetSide = options?.SheetSide ?? side };

    // These methods always pass options (they set Alert / SheetSide), so the service's own
    // "no options given" fallback never runs for them: start from the app-wide defaults here. A
    // plain DialogOptions default still carries the mechanism-level settings.
    private static BzDialogOptions DefaultsOf(IDialogService dialogs) =>
        dialogs.Defaults as BzDialogOptions ?? new BzDialogOptions
        {
            Modal = dialogs.Defaults.Modal,
            DismissOnOutsideClick = dialogs.Defaults.DismissOnOutsideClick,
            PreventDismiss = dialogs.Defaults.PreventDismiss,
        };
}
