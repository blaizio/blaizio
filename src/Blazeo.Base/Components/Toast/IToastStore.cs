namespace Blazeo;

/// <summary>
/// The per-scope registry of live toasts behind the imperative toast API. A host component (the Ui
/// <c>ToastProvider</c> over <see cref="BaseToastProvider"/>) renders <see cref="Toasts"/> and re-renders on
/// <see cref="Changed"/>. Registered by <c>AddBlazeoBase</c>; the styled <c>IToastService</c> wraps it.
/// </summary>
public interface IToastStore
{
    /// <summary>The live toasts, oldest first (newest renders in front of the stack).</summary>
    IReadOnlyList<ToastInstance> Toasts { get; }

    /// <summary>Raised whenever the toast list, a toast's data, or its dismiss state changes.</summary>
    event Action? Changed;

    /// <summary>
    /// Shows a toast (or, when <paramref name="id"/> matches an existing one, replaces its data in place -
    /// the basis of promise/update flows). Returns the toast's id so it can be updated or dismissed later.
    /// </summary>
    string Show(ToastData data, string? id = null);

    /// <summary>Replaces an existing toast's data, keeping its place in the stack. No-op if it is gone.</summary>
    void Update(string id, ToastData data);

    /// <summary>
    /// Requests dismissal: of the toast with <paramref name="id"/>, or of every toast when it is
    /// <see langword="null"/>. The exit animation plays before the toast is actually removed.
    /// </summary>
    void Dismiss(string? id = null);

    /// <summary>
    /// Called by the host once the engine has finished animating a toast out: fires the matching
    /// callback (<see cref="ToastData.OnAutoClose"/> when <paramref name="autoClose"/>, else
    /// <see cref="ToastData.OnDismiss"/>) and drops the toast. Not part of the public imperative API.
    /// </summary>
    void NotifyRemoved(string id, bool autoClose);
}
