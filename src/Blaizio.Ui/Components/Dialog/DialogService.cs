using Blaizio;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blaizio.Ui;

/// <inheritdoc cref="IDialogService"/>
public sealed class DialogService(IDialogStore store) : IDialogService
{
    /// <inheritdoc/>
    public Task<DialogResult> ShowAsync(RenderFragment<DialogInstance> template, UiDialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        return store.OpenAsync(instance => template(instance), options ?? new UiDialogOptions());
    }

    /// <inheritdoc/>
    public Task<DialogResult> ShowAsync<TComponent>(
        IReadOnlyDictionary<string, object?>? parameters = null,
        UiDialogOptions? options = null) where TComponent : IComponent =>
        store.OpenAsync(_ => BuildComponent<TComponent>(parameters), options ?? new UiDialogOptions());

    /// <inheritdoc/>
    public async Task<bool> ConfirmAsync(string title, string? description = null, UiDialogOptions? options = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(BzConfirmDialog.Title)] = title,
            [nameof(BzConfirmDialog.Description)] = description,
        };
        // The alert skin is non-dismissable by backdrop; Escape still cancels (-> false).
        var result = await ShowAsync<BzConfirmDialog>(parameters, (options ?? new UiDialogOptions()) with { Alert = true });
        return result is { Cancelled: false, Value: true };
    }

    // Renders TComponent via DynamicComponent so a runtime parameter dictionary diffs correctly. The
    // cascaded DialogInstance flows straight through DynamicComponent to the component.
    private static RenderFragment BuildComponent<TComponent>(IReadOnlyDictionary<string, object?>? parameters)
        where TComponent : IComponent => builder =>
    {
        builder.OpenComponent<DynamicComponent>(0);
        builder.AddComponentParameter(1, nameof(DynamicComponent.Type), typeof(TComponent));
        if (parameters is { Count: > 0 })
        {
            var resolved = parameters
                .Where(p => p.Value is not null)
                .ToDictionary(p => p.Key, p => p.Value!);
            if (resolved.Count > 0)
                builder.AddComponentParameter(2, nameof(DynamicComponent.Parameters), resolved);
        }
        builder.CloseComponent();
    };
}
