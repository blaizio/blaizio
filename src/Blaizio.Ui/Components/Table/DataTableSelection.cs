namespace Blaizio.Ui;

/// <summary>
/// What a <see cref="BzDataTable{TItem}"/>'s <c>SelectionSummaryContent</c> receives: how many of
/// the rows in view are selected, for a summary worded in the reader's language.
/// </summary>
public sealed class DataTableSelection
{
    internal DataTableSelection(int selected, int total)
    {
        Selected = selected;
        Total = total;
    }

    /// <summary>The selected rows among those in view.</summary>
    public int Selected { get; }

    /// <summary>The rows in view: every row the search leaves, or the current page's rows when paging.</summary>
    public int Total { get; }
}
