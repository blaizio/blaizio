using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// The imperative dialog API: open dialogs from C# (event handlers, async flows) instead of markup.
/// Requires a single <c>&lt;DialogProvider /&gt;</c> at the app root and <c>AddBlaizioBase()</c> in DI.
/// The styled layer adds sugar on top (e.g. <c>ConfirmAsync</c>) as extension methods.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows <typeparamref name="TComponent"/> as the dialog body, optionally passing it
    /// <paramref name="parameters"/> by name. The component reads its <see cref="DialogInstance"/> from a
    /// cascaded parameter to close itself with a result.
    /// </summary>
    Task<DialogResult> ShowAsync<TComponent>(IReadOnlyDictionary<string, object?>? parameters = null, DialogOptions? options = null)
        where TComponent : IComponent;

    /// <summary>
    /// Shows an inline template as the dialog body; the template receives the <see cref="DialogInstance"/>
    /// so it can close itself.
    /// </summary>
    Task<DialogResult> ShowAsync(RenderFragment<DialogInstance> template, DialogOptions? options = null);
}
