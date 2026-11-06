namespace Blaizio.Ui;

/// <summary>How an editable <see cref="BzDataTable{TItem}"/> edits: one cell at a time, a whole row in place, or a row in a dialog.</summary>
public enum DataTableEditMode
{
    /// <summary>
    /// Spreadsheet-style: one cell at a time. The arrows, Tab and Enter move the cursor (Enter goes
    /// down, back to the column a run of Tabs began in). Type to replace a cell, after which an
    /// arrow commits and moves; F2 or a double-click edits it in place. Enter or Tab commits and
    /// moves on, Escape cancels. A bool cell flips with Space or a click.
    /// </summary>
    Cell,

    /// <summary>
    /// The whole row at once, in place: Enter / F2 / double-click opens an editor in every editable
    /// cell of the row, Tab walks them, Enter commits the row as one change and Escape cancels it.
    /// </summary>
    Row,

    /// <summary>
    /// The whole row in a dialog: Enter / F2 / double-click opens a form with a field per editable
    /// column (hidden columns included). Save commits the row as one change; Cancel or Escape drops it.
    /// </summary>
    Dialog,
}

/// <summary>What produced a <see cref="DataTableEditEventArgs{TItem}"/>.</summary>
public enum DataTableEditReason
{
    /// <summary>An editor committed: a cell, a row, a dialog or a toggled checkbox.</summary>
    Edit,

    /// <summary>Text pasted into the table, one cell or a tab-separated block.</summary>
    Paste,

    /// <summary>Delete cleared a cell.</summary>
    Clear,
}
