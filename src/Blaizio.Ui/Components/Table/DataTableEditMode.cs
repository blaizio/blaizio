namespace Blaizio.Ui;

/// <summary>How an editable <see cref="BzDataTable{TItem}"/> edits: one cell at a time, a whole row in place, or a row as a form in a dialog or a drawer.</summary>
public enum DataTableEditMode
{
    /// <summary>
    /// Spreadsheet-style: one cell at a time. The arrows, Tab and Enter move the cursor (Enter goes
    /// down, back to the column a run of Tabs began in). A click selects a cell; a double-click or F2
    /// edits it in place, and typing replaces it (an arrow then commits and moves). Enter or Tab
    /// commits and moves on, Escape cancels. A bool cell flips with Space or a click on its box.
    /// </summary>
    Cell,

    /// <summary>
    /// The whole row at once, in place: Enter / F2 / double-click opens an editor in every editable
    /// cell of the row, Tab walks them, Enter commits the row as one change and Escape cancels it.
    /// </summary>
    Row,

    /// <summary>
    /// The whole row in a dialog: Enter / F2 / double-click opens a form with a field per editable
    /// column (hidden columns included). Save commits the row as one change; Cancel or Escape drops
    /// it, asking first when the form has changes.
    /// </summary>
    Dialog,

    /// <summary>
    /// The whole row in a drawer at the inline-end edge (full width on narrow screens): the dialog's
    /// form, with Previous and Next buttons that save and move to the neighbouring row.
    /// </summary>
    Drawer,
}

/// <summary>
/// How an edit started. Editors follow it: a pick list opens its list and a date field its calendar
/// when <see cref="DataTableCellEditor{TItem, TValue}.OpenPopup"/> says so, and typing replaces the
/// value with the typed text.
/// </summary>
public enum DataTableEditStart
{
    /// <summary>From code: <c>BeginEditAsync</c>.</summary>
    Code,

    /// <summary>A double-click, a tap on the active cell, or a press on a cell's list or calendar mark.</summary>
    Pointer,

    /// <summary>A key that edits in place: F2, Space or Alt+ArrowDown (and Enter in Row, Dialog and Drawer modes).</summary>
    Key,

    /// <summary>A character typed over the cell, or Backspace: the editor starts from the typed text alone.</summary>
    Typing,
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
