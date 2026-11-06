using Microsoft.AspNetCore.Components;

namespace Blaizio.Ui;

/// <summary>
/// The shape a <see cref="BzDataTable{TItem}"/> renders each column against, regardless of whether the
/// column came from a child <see cref="BzPropertyColumn{TItem,TProp}"/> / <see cref="BzTemplateColumn{TItem}"/>
/// component or from a <see cref="DataTableColumn{TItem}"/> in the <c>Columns</c> list. The grid reads these
/// live, so a column-parameter change reflects without re-registration.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
internal interface IGridColumn<TItem>
{
    /// <summary>Stable id used for sort + visibility state and as the render key.</summary>
    string Key { get; }

    /// <summary>Human label for the header and the column-visibility menu.</summary>
    string? Title { get; }

    /// <summary>Whether the header offers a sort toggle (a comparison is available).</summary>
    bool Sortable { get; }

    /// <summary>Whether the column contributes text to the search filter.</summary>
    bool Searchable { get; }

    /// <summary>Whether the column starts hidden (toggleable back on via the visibility menu).</summary>
    bool DefaultHidden { get; }

    /// <summary>Text alignment: <c>start</c>, <c>center</c> or <c>end</c>.</summary>
    string Align { get; }

    /// <summary>Extra classes for the header cell.</summary>
    string? HeaderClass { get; }

    /// <summary>Extra classes for each body cell.</summary>
    string? CellClass { get; }

    /// <summary>Custom header content, or <see langword="null"/> to render <see cref="Title"/>.</summary>
    RenderFragment? Header { get; }

    /// <summary>The starting width in pixels, or <see langword="null"/> to size to the content (or to fill).</summary>
    double? Width { get; }

    /// <summary>The narrowest the column gets in pixels, or <see langword="null"/> for the table's floor.</summary>
    double? MinWidth { get; }

    /// <summary>The widest the column gets in pixels, or <see langword="null"/> for no ceiling.</summary>
    double? MaxWidth { get; }

    /// <summary>Whether the column takes the room the other columns leave in the row.</summary>
    bool Fill { get; }

    /// <summary>Where the column stays while the rows scroll sideways.</summary>
    DataTablePin Pinned { get; }

    /// <summary>Whether a user may resize the column (when the table's <c>ResizableColumns</c> is on).</summary>
    bool Resizable { get; }

    /// <summary>Whether a user may move the column (when the table's <c>ReorderableColumns</c> is on).</summary>
    bool Reorderable { get; }

    /// <summary>Renders the cell for one row.</summary>
    RenderFragment RenderCell(TItem item);

    /// <summary>Ascending comparison of two rows by this column (only meaningful when <see cref="Sortable"/>).</summary>
    int Compare(TItem a, TItem b);

    /// <summary>The row's searchable text for this column, or <see langword="null"/> when the column is not searchable.</summary>
    string? GetText(TItem item);

    /// <summary>The column's editing surface, or <see langword="null"/> when the column is read-only.</summary>
    IGridEditor<TItem>? Editor { get; }
}

/// <summary>
/// The editing half of an editable column, boxed to <see cref="object"/> so the grid can drive
/// columns of any value type: read, parse, validate and write through the column's
/// <see cref="CellBinding{TItem, TValue}"/>, and render its editor - the built-in control or the
/// column's own template.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
internal interface IGridEditor<TItem>
{
    /// <summary>
    /// The built-in control the column edits with, resolved from its <c>Editor</c> parameter and the
    /// value's type; <see cref="DataTableEditor.Auto"/> when only a template can edit it.
    /// </summary>
    DataTableEditor Kind { get; }

    /// <summary>Whether the column gives its own <c>EditorContent</c>, which replaces the built-in editor.</summary>
    bool HasTemplate { get; }

    /// <summary>Whether the column renders its cells with its own template (a bool column then keeps it instead of showing a checkbox).</summary>
    bool HasDisplayTemplate { get; }

    /// <summary>Whether a committed value can be assigned to the row (a settable member on a class).</summary>
    bool CanWrite { get; }

    /// <summary>The column's value type.</summary>
    Type ValueType { get; }

    /// <summary>Whether an empty value is legal: a nullable value type, or a reference type.</summary>
    bool AllowsNull { get; }

    /// <summary>The row's current value.</summary>
    object? Read(TItem item);

    /// <summary>A value as editing text: the raw value in the current culture, no display format.</summary>
    string Format(object? value);

    /// <summary>Parses editing text; on failure <paramref name="error"/> names <paramref name="displayName"/>.</summary>
    bool TryParse(string? text, string displayName, out object? value, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error);

    /// <summary>The member's DataAnnotations, then the column's own Validate. Null when valid.</summary>
    string? Validate(TItem item, object? value, string displayName);

    /// <summary>Assigns the value to the row (only when <see cref="CanWrite"/>).</summary>
    void Write(TItem item, object? value);

    /// <summary>The value a cleared cell takes, when the type has one.</summary>
    bool TryGetClearValue(out object? value);

    /// <summary>Change detection (a null and an empty string are the same text).</summary>
    bool ValuesEqual(object? a, object? b);

    /// <summary>
    /// A pick list's value for typed text: the option whose label is the text, or with
    /// <paramref name="prefix"/> the first that starts with it (what typing over a cell picks).
    /// False when nothing matches or the column is no pick list.
    /// </summary>
    bool TryMatchOption(string text, bool prefix, out object? value);

    /// <summary>The built-in control (<see cref="Kind"/>) bound to the open edit's draft.</summary>
    RenderFragment RenderControl(DataTableEditorRequest request);

    /// <summary>The column's editor template bound to a draft (only when <see cref="HasTemplate"/>).</summary>
    RenderFragment RenderTemplate(
        TItem item,
        string columnKey,
        string? title,
        string? error,
        bool inDialog,
        IReadOnlyDictionary<string, object> attributes,
        Func<object?> read,
        Action<object?> write,
        Func<Task> commit,
        Func<Task> cancel);
}
