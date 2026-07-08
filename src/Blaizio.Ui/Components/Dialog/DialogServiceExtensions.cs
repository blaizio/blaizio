using Blaizio;

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
        this IDialogService dialogs, string title, string? description = null, UiDialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        var parameters = new Dictionary<string, object?>
        {
            [nameof(BzConfirmDialog.Title)] = title,
            [nameof(BzConfirmDialog.Description)] = description,
        };
        // The alert skin is non-dismissable by backdrop; Escape still cancels (-> false).
        var result = await dialogs.ShowAsync<BzConfirmDialog>(parameters, (options ?? new UiDialogOptions()) with { Alert = true });
        return result is { Cancelled: false, Value: true };
    }
}
