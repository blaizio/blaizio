using Blazeo;
using Microsoft.AspNetCore.Components;

namespace Blazeo.Ui;

/// <summary>
/// The imperative dialog API: open dialogs from C# (event handlers, async flows) instead of markup.
/// Requires a single <c>&lt;DialogProvider /&gt;</c> at the app root and <c>AddBlazeoUi()</c> in DI.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a confirmation (alert) dialog. Returns <see langword="true"/> if the user confirmed, or
    /// <see langword="false"/> if they cancelled or dismissed it (Escape).
    /// </summary>
    Task<bool> ConfirmAsync(string title, string? description = null, UiDialogOptions? options = null);

    /// <summary>
    /// Shows <typeparamref name="TComponent"/> as the dialog body, optionally passing it
    /// <paramref name="parameters"/> by name. The component reads its <see cref="DialogInstance"/> from a
    /// cascaded parameter to close itself with a result.
    /// </summary>
    Task<DialogResult> ShowAsync<TComponent>(IReadOnlyDictionary<string, object?>? parameters = null, UiDialogOptions? options = null)
        where TComponent : IComponent;

    /// <summary>
    /// Shows an inline template as the dialog body; the template receives the <see cref="DialogInstance"/>
    /// so it can close itself.
    /// </summary>
    Task<DialogResult> ShowAsync(RenderFragment<DialogInstance> template, UiDialogOptions? options = null);
}
