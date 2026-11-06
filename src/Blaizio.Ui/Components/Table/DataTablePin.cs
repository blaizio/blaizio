namespace Blaizio.Ui;

/// <summary>Where a <see cref="BzDataTable{TItem}"/> column stays while the rows scroll sideways.</summary>
public enum DataTablePin
{
    /// <summary>Scrolls with the rest of the row (the default).</summary>
    None,

    /// <summary>
    /// Stays at the start edge, with the other start columns: the left in a left-to-right page, the
    /// right in a right-to-left one. A table with row selection keeps the checkbox column there too.
    /// </summary>
    Start,

    /// <summary>Stays at the end edge, with the other end columns: the right in a left-to-right page, the left in a right-to-left one.</summary>
    End,
}
