using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// A handle to one imperatively shown dialog (opened via <see cref="IDialogStore.OpenAsync"/>). The host
/// renders <see cref="Content"/> while <see cref="IsOpen"/> is <see langword="true"/>; the dialog body
/// resolves <see cref="Result"/> by calling <see cref="Close(DialogResult)"/> or <see cref="Cancel"/>.
/// A shown component or template reaches it through a cascaded parameter. Do not construct directly.
/// </summary>
public sealed class DialogInstance
{
    private readonly TaskCompletionSource<DialogResult> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action _onChanged;
    private readonly Action<DialogInstance> _onExited;
    private bool _exited;
    private bool _preventDismiss;

    internal DialogInstance(
        string id,
        DialogOptions options,
        Func<DialogInstance, RenderFragment> contentFactory,
        Action onChanged,
        Action<DialogInstance> onExited)
    {
        Id = id;
        Options = options;
        _preventDismiss = options.PreventDismiss;
        _onChanged = onChanged;
        _onExited = onExited;
        // 'this' is fully assigned above, so the factory can capture the instance to close itself.
        Content = contentFactory(this);
    }

    /// <summary>A stable id for this dialog, used as the host's render key.</summary>
    public string Id { get; }

    /// <summary>The content rendered inside the dialog skin.</summary>
    public RenderFragment Content { get; }

    /// <summary>The options the dialog was opened with (a styled <c>UiDialogOptions</c> at the Ui layer).</summary>
    public DialogOptions Options { get; }

    /// <summary>
    /// Whether the dialog is open. The host binds the dialog's <c>Open</c> to this; closing flips it to
    /// <see langword="false"/>, which drives the exit animation before the host drops the instance.
    /// </summary>
    public bool IsOpen { get; private set; } = true;

    /// <summary>
    /// When <see langword="true"/>, implicit dismissal (Escape, backdrop, the close button) is blocked.
    /// Set it while a lengthy operation runs so the dialog can't be closed out from under it, then clear
    /// it (or just <see cref="Close(DialogResult)"/> when done). Seeded from <see cref="DialogOptions"/>;
    /// it does NOT block an explicit <see cref="Close(DialogResult)"/>/<see cref="Cancel"/>.
    /// </summary>
    public bool PreventDismiss
    {
        get => _preventDismiss;
        // Re-render the host so the change reaches the dialog's cascade (gating + the hidden corner X).
        set
        {
            if (_preventDismiss == value) return;
            _preventDismiss = value;
            _onChanged();
        }
    }

    /// <summary>Completes when the dialog closes, carrying the resolved or cancelled <see cref="DialogResult"/>.</summary>
    public Task<DialogResult> Result => _tcs.Task;

    /// <summary>
    /// Closes the dialog, resolving <see cref="Result"/> with <paramref name="result"/>. Idempotent - a
    /// second call (or a race against <see cref="Cancel"/>) is ignored, so the first outcome wins.
    /// </summary>
    public void Close(DialogResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!IsOpen) return;
        IsOpen = false;          // -> the controlled <BzDialog Open=false> begins its exit animation
        _tcs.TrySetResult(result); // the caller's await resumes now; removal waits for the animation
        _onChanged();            // re-render the host so the dialog picks up the closed state
    }

    /// <summary>Closes with a cancelled result (the dismissal outcome). Idempotent.</summary>
    public void Cancel() => Close(DialogResult.Cancel());

    /// <summary>
    /// Dismiss the dialog (Escape / backdrop / close button) unless <see cref="PreventDismiss"/> is set.
    /// The host routes every implicit dismissal through here. Returns whether the dialog was dismissed.
    /// </summary>
    public bool TryDismiss()
    {
        if (PreventDismiss) return false;
        Cancel();
        return true;
    }

    // Wired by the skin to the content's OnExitComplete: the exit animation has finished, so the host
    // can drop this instance. Idempotent per close.
    internal void NotifyExited()
    {
        if (_exited) return;
        _exited = true;
        _onExited(this);
    }

    // Teardown (scope/host disposal): resolve any awaiter so it never hangs. Deliberately resolves a
    // cancelled result rather than cancelling the Task - a TaskCanceledException would surface in an
    // awaiter that is itself being torn down.
    internal void Dispose() => _tcs.TrySetResult(DialogResult.Cancel());
}
