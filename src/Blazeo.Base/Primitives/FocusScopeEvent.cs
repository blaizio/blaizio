namespace Blazeo;

/// <summary>
/// A cancelable event raised by <see cref="BzFocusScope"/> when it is about to auto-focus
/// (on mount) or restore focus (on unmount). Call <see cref="PreventDefault"/> to take over
/// focus handling yourself - e.g. to focus a specific control instead of the first tabbable.
/// </summary>
public sealed class FocusScopeEvent
{
    /// <summary>Whether <see cref="PreventDefault"/> has been called.</summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>Suppresses the scope's default focus behaviour for this event.</summary>
    public void PreventDefault() => DefaultPrevented = true;
}
