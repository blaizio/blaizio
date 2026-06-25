using Microsoft.AspNetCore.Components;

namespace Blazeo.Ui;

/// <summary>
/// The shape a <see cref="BzDataTable{TItem}"/> renders each column against, regardless of whether the
/// column came from a child <see cref="BzPropertyColumn{TItem,TProp}"/> / <see cref="BzTemplateColumn{TItem}"/>
/// component or from a <see cref="ColumnDef{TItem}"/> in the <c>Columns</c> list. The grid reads these
/// live, so a column-parameter change reflects without re-registration.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
internal interface IGridColumn<TItem>
{
    /// <summary>Stable id used for sort + visibility state and as the React-style key.</summary>
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

    /// <summary>Renders the cell for one row.</summary>
    RenderFragment RenderCell(TItem item);

    /// <summary>Ascending comparison of two rows by this column (only meaningful when <see cref="Sortable"/>).</summary>
    int Compare(TItem a, TItem b);

    /// <summary>The row's searchable text for this column, or <see langword="null"/> when the column is not searchable.</summary>
    string? GetText(TItem item);
}
