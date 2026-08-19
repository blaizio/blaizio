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
        // The alert skin is non-dismissable by backdrop; Escape still cancels (-> false).
        var result = await dialogs.ShowAsync<BzConfirmDialog>(parameters, (options ?? new BzDialogOptions()) with { Alert = true });
        return result is { Cancelled: false, Value: true };
    }

    /// <summary>
    /// Shows <typeparamref name="TComponent"/> in the Sheet skin, sliding in from <paramref name="side"/>.
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
        return dialogs.ShowAsync<TComponent>(parameters, WithSheet(options, side));
    }

    /// <summary>
    /// Shows an inline template in the Sheet skin, sliding in from <paramref name="side"/>. The template
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
        return dialogs.ShowAsync(template, WithSheet(options, side));
    }

    private static BzDialogOptions WithSheet(BzDialogOptions? options, PanelSide side) =>
        (options ?? new BzDialogOptions()) with { SheetSide = options?.SheetSide ?? side };
}
