namespace Blaizio.Ui;

/// <summary>How a select listbox lets you scroll a list taller than its viewport.</summary>
public enum SelectScrollMode
{
    /// <summary>A thin scrollbar on the listbox - the default.</summary>
    Scrollbar,

    /// <summary>
    /// An up chevron at the top and a down chevron at the bottom (no scrollbar). Each hides when there's
    /// nothing to scroll that way, and hovering one auto-scrolls the list - the native select feel.
    /// </summary>
    Buttons,
}
