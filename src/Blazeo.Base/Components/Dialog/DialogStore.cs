using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <inheritdoc cref="IDialogStore"/>
public sealed class DialogStore : IDialogStore, IDisposable
{
    private readonly List<DialogInstance> _instances = [];

    /// <inheritdoc/>
    public IReadOnlyList<DialogInstance> Instances => _instances;

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    public Task<DialogResult> OpenAsync(Func<DialogInstance, RenderFragment> contentFactory, DialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(contentFactory);
        ArgumentNullException.ThrowIfNull(options);

        var instance = new DialogInstance(
            Identifier.Create("dialog"),
            options,
            contentFactory,
            () => Changed?.Invoke(),
            Remove);

        _instances.Add(instance);
        Changed?.Invoke();
        return instance.Result;
    }

    // Driven by DialogInstance.NotifyExited once the exit animation has played - never on Close, so the
    // animation is not cut off.
    private void Remove(DialogInstance instance)
    {
        if (_instances.Remove(instance)) Changed?.Invoke();
    }

    /// <summary>Resolves any still-open dialogs as cancelled so awaiters do not hang, then clears.</summary>
    public void Dispose()
    {
        var pending = _instances.ToArray();
        _instances.Clear();
        foreach (var instance in pending) instance.Dispose();
        Changed = null;
    }
}
