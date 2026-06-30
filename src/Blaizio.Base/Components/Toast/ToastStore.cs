namespace Blaizio;

/// <inheritdoc cref="IToastStore"/>
public sealed class ToastStore : IToastStore, IDisposable
{
    private readonly List<ToastInstance> _instances = [];

    /// <inheritdoc/>
    public IReadOnlyList<ToastInstance> Toasts => _instances;

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    public string Show(ToastData data, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        id ??= Identifier.Create("toast");
        var existing = Find(id);
        if (existing is not null)
        {
            // Same id ⇒ update in place (promise resolve, or an explicit re-show). Clear any pending
            // dismissal so a resolved promise toast doesn't animate out from under its new content.
            existing.Data = data;
            existing.DismissRequested = false;
        }
        else
        {
            _instances.Add(new ToastInstance(id, data));
        }

        Changed?.Invoke();
        return id;
    }

    /// <inheritdoc/>
    public void Update(string id, ToastData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (Find(id) is not { } instance) return;
        instance.Data = data;
        Changed?.Invoke();
    }

    /// <inheritdoc/>
    public void Dismiss(string? id = null)
    {
        if (id is null)
        {
            foreach (var instance in _instances) instance.DismissRequested = true;
        }
        else if (Find(id) is { } instance)
        {
            instance.DismissRequested = true;
        }

        Changed?.Invoke();
    }

    /// <inheritdoc/>
    public void NotifyRemoved(string id, bool autoClose)
    {
        if (Find(id) is not { } instance) return;

        // Fire the lifecycle callback before dropping the toast, so a handler can still read its data.
        (autoClose ? instance.Data.OnAutoClose : instance.Data.OnDismiss)?.Invoke();

        _instances.Remove(instance);
        Changed?.Invoke();
    }

    private ToastInstance? Find(string id) => _instances.Find(t => t.Id == id);

    /// <summary>Drops every toast on scope/host teardown.</summary>
    public void Dispose()
    {
        _instances.Clear();
        Changed = null;
    }
}
