namespace Blaizio.Ui;

/// <summary>How a <see cref="BzTree{TItem}"/> draws the guide lines connecting each branch to its children.</summary>
public enum TreeGuides
{
    /// <summary>No guide lines - indentation alone conveys the hierarchy.</summary>
    None,

    /// <summary>A straight vertical line along each branch's children (the default).</summary>
    Lines,

    /// <summary>Full connectors: the vertical line hangs under the parent's chevron and curves into a
    /// horizontal run toward every child, ending just short of its row. Uses a wider default indent
    /// (1.75rem) to make room for the run; keep any explicit <c>Indent</c> at or above that.</summary>
    Connectors,
}
