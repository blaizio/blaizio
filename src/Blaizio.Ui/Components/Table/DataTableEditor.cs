namespace Blaizio.Ui;

/// <summary>
/// The control an editable <see cref="BzPropertyColumn{TItem,TProp}"/> edits with: in its cells, in
/// an open row and as a dialog field. <see cref="Auto"/> picks one from the property's type; set
/// another to choose, for example a <see cref="Switch"/> for a bool or a <see cref="Select"/> over a
/// fixed list of strings.
/// </summary>
public enum DataTableEditor
{
    /// <summary>
    /// Picked from the property's type: a bool gets a <see cref="Checkbox"/>, an enum a
    /// <see cref="Select"/>, a number a <see cref="Number"/> field, a <c>DateOnly</c> or
    /// <c>DateTime</c> a <see cref="Date"/> field, a <c>TimeOnly</c> a <see cref="Time"/> field,
    /// and anything else that parses from text (strings, ids, flags enums) a <see cref="Text"/> box.
    /// </summary>
    Auto,

    /// <summary>A text box (BzInputText). Fits any type that parses from text in the current culture.</summary>
    Text,

    /// <summary>A number field (BzInputNumber): <c>int</c>, <c>long</c>, <c>short</c>, <c>float</c>, <c>double</c> and <c>decimal</c>.</summary>
    Number,

    /// <summary>A pick list (BzSelect): an enum's values, or the column's <c>Options</c> for any other type.</summary>
    Select,

    /// <summary>A checkbox (BzCheckbox) for a <c>bool</c>. In Cell mode it flips in place.</summary>
    Checkbox,

    /// <summary>A switch (BzSwitch) for a <c>bool</c>. In Cell mode it flips in place.</summary>
    Switch,

    /// <summary>A date field (BzInputDate) with a calendar: <c>DateOnly</c>, or <c>DateTime</c> keeping its time of day.</summary>
    Date,

    /// <summary>A time field (BzInputTime): <c>TimeOnly</c>.</summary>
    Time,
}
