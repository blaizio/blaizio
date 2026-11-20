using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <inheritdoc cref="IDialogService"/>
public sealed class DialogService(IDialogStore store, BlaizioOptions defaults) : IDialogService
{
    /// <inheritdoc/>
    public DialogOptions Defaults => defaults.DialogDefaults;

    /// <inheritdoc/>
    public Task<DialogResult> ShowAsync(RenderFragment<DialogInstance> template, DialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        return store.OpenAsync(instance => template(instance), options ?? defaults.DialogDefaults);
    }

    /// <inheritdoc/>
    public Task<DialogResult> ShowAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(
        IReadOnlyDictionary<string, object?>? parameters = null,
        DialogOptions? options = null) where TComponent : IComponent =>
        store.OpenAsync(_ => BuildComponent<TComponent>(parameters), options ?? defaults.DialogDefaults);

    // Renders TComponent with a runtime parameter dictionary. Every entry is added under one
    // sequence number, which is how a parameter set that varies between renders is diffed by name
    // (what DynamicComponent does inside). The component is opened by its own type, so the trimmer
    // keeps its parameters; it sits directly under the dialog and receives the cascaded
    // DialogInstance.
    private static RenderFragment BuildComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(
        IReadOnlyDictionary<string, object?>? parameters)
        where TComponent : IComponent => builder =>
    {
        builder.OpenComponent<TComponent>(0);
        if (parameters is { Count: > 0 })
        {
            foreach (var parameter in parameters)
            {
                if (parameter.Value is not null)
                    builder.AddComponentParameter(1, parameter.Key, parameter.Value);
            }
        }
        builder.CloseComponent();
    };
}
