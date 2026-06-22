namespace Blazeo;

/// <summary>
/// A live toast in the <see cref="IToastStore"/>: a stable <see cref="Id"/> plus its current
/// <see cref="Data"/>. The host renders these; the layout/animation engine (ts/toast.ts) owns the
/// on-screen lifecycle and reports back when one has finished animating out. Do not construct directly.
/// </summary>
public sealed class ToastInstance
{
    internal ToastInstance(string id, ToastData data)
    {
        Id = id;
        Data = data;
    }

    /// <summary>A stable id, used as the host's render key and to target updates / dismissals.</summary>
    public string Id { get; }

    /// <summary>The toast's current content and behaviour. Replaced by <see cref="IToastStore.Update"/>.</summary>
    public ToastData Data { get; internal set; }

    /// <summary>
    /// Set when something asks to dismiss this toast (<see cref="IToastStore.Dismiss"/>). The host
    /// forwards it to the engine so the exit animation plays; the instance is only dropped once the
    /// engine reports back. User-initiated dismissals (swipe / close button) start in the engine and
    /// never set this.
    /// </summary>
    public bool DismissRequested { get; internal set; }
}
