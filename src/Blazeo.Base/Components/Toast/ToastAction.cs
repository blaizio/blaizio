namespace Blazeo;

/// <summary>
/// A button rendered inside a toast: the primary <c>Action</c> (e.g. "Undo") or the secondary
/// <c>Cancel</c>. Clicking it runs <see cref="OnClick"/> and then dismisses the toast (unless the
/// handler is the cancel of a non-dismissible toast).
/// </summary>
/// <param name="Label">The button text.</param>
/// <param name="OnClick">Invoked when the button is clicked, before the toast is dismissed.</param>
public sealed record ToastAction(string Label, Action OnClick);
