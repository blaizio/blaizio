namespace Blaizio;

/// <summary>
/// A cancelable event raised when a dropdown-menu item is selected (by click, Enter, or Space).
/// The menu closes after a selection by default; call <see cref="PreventDefault"/> from an
/// <c>Select</c> handler to keep it open - e.g. so a checkbox or radio item can be toggled
/// several times without the menu dismissing.
/// </summary>
public sealed class MenuSelectEventArgs
{
    /// <summary>Whether <see cref="PreventDefault"/> has been called.</summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>Keeps the menu open after this selection.</summary>
    public void PreventDefault() => DefaultPrevented = true;
}
