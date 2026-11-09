namespace Blaizio.Ui;

/// <summary>What a key press did to a column, as a <see cref="DataTableColumnAnnouncement"/> reports it.</summary>
public enum DataTableColumnAnnouncementKind
{
    /// <summary>The column took a new width (Alt+Shift+Left / Right, or Alt+Shift+Enter to fit).</summary>
    Resized,

    /// <summary>The column moved one place (Ctrl+Shift+Left / Right).</summary>
    Moved,

    /// <summary>The column could not move further that way: the end of its group, or a column that stays put.</summary>
    CannotMove,
}

/// <summary>
/// What a <see cref="BzDataTable{TItem}"/>'s <c>ColumnAnnouncement</c> receives: a column a key
/// press just resized or moved, for what screen readers hear in the reader's language.
/// </summary>
public sealed class DataTableColumnAnnouncement
{
    internal DataTableColumnAnnouncement(DataTableColumnAnnouncementKind kind, string column, int width, int position, int count)
    {
        Kind = kind;
        Column = column;
        Width = width;
        Position = position;
        Count = count;
    }

    /// <summary>What happened to the column.</summary>
    public DataTableColumnAnnouncementKind Kind { get; }

    /// <summary>The column's title (its key when it has none).</summary>
    public string Column { get; }

    /// <summary>The column's new width in pixels (<see cref="DataTableColumnAnnouncementKind.Resized"/>).</summary>
    public int Width { get; }

    /// <summary>The column's new place among the columns in view, counting from 1 (<see cref="DataTableColumnAnnouncementKind.Moved"/>).</summary>
    public int Position { get; }

    /// <summary>How many columns are in view, the selection column left out (<see cref="DataTableColumnAnnouncementKind.Moved"/>).</summary>
    public int Count { get; }

    /// <summary>The English announcement, for example <c>Name moved to column 2 of 5</c>.</summary>
    public string Message => Kind switch
    {
        DataTableColumnAnnouncementKind.Moved => $"{Column} moved to column {Position} of {Count}",
        DataTableColumnAnnouncementKind.CannotMove => $"{Column} cannot move further",
        _ => $"{Column}: {Width} pixels",
    };
}
