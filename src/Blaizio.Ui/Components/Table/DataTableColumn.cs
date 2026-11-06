using Microsoft.AspNetCore.Components;

namespace Blaizio.Ui;

/// <summary>
/// A column defined in data, for the <see cref="BzDataTable{TItem}.Columns"/> list - the data-driven
/// alternative to the child <see cref="BzPropertyColumn{TItem,TProp}"/> / <see cref="BzTemplateColumn{TItem}"/>
/// components. Set <see cref="SortBy"/> to make the column sortable and <see cref="Text"/> to include it
/// in search; both are plain accessors over the row.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public sealed class DataTableColumn<TItem>
{
    /// <summary>Header label and visibility-menu label.</summary>
    public string? Title { get; set; }

    /// <summary>Stable id for sort + visibility state. Defaults to <see cref="Title"/> when unset.</summary>
    public string? Key { get; set; }

    /// <summary>Renders the cell for a row. Required.</summary>
    public required RenderFragment<TItem> Cell { get; set; }

    /// <summary>Custom header content; when unset the <see cref="Title"/> text is rendered.</summary>
    public RenderFragment? Header { get; set; }

    /// <summary>Sort key accessor. When set, the column is sortable.</summary>
    public Func<TItem, object?>? SortBy { get; set; }

    /// <summary>A multi-key / tie-breaker sort. When set it overrides <see cref="SortBy"/>.</summary>
    public DataTableSort<TItem>? Sort { get; set; }

    /// <summary>Search-text accessor. When set, the column is included in the search filter.</summary>
    public Func<TItem, string?>? Text { get; set; }

    /// <summary>Text alignment: <c>start</c> (default), <c>center</c> or <c>end</c>.</summary>
    public string Align { get; set; } = "start";

    /// <summary>Start the column hidden (toggleable on via the visibility menu).</summary>
    public bool Hidden { get; set; }

    /// <summary>Extra classes for the header cell.</summary>
    public string? HeaderClass { get; set; }

    /// <summary>Extra classes for each body cell.</summary>
    public string? CellClass { get; set; }

    /// <summary>The starting width in pixels; unset, the column sizes to its content (or fills, see <see cref="Fill"/>).</summary>
    public double? Width { get; set; }

    /// <summary>The narrowest the column gets in pixels.</summary>
    public double? MinWidth { get; set; }

    /// <summary>The widest the column gets in pixels.</summary>
    public double? MaxWidth { get; set; }

    /// <summary>Take the room the other columns leave in the row.</summary>
    public bool Fill { get; set; }

    /// <summary>Keep the column in place while the rows scroll sideways, at the start or the end edge.</summary>
    public DataTablePin Pinned { get; set; }

    /// <summary>Whether a user can resize the column when the table's <c>ResizableColumns</c> is on. Defaults to <see langword="true"/>.</summary>
    public bool Resizable { get; set; } = true;

    /// <summary>Whether a user can move the column when the table's <c>ReorderableColumns</c> is on. Defaults to <see langword="true"/>.</summary>
    public bool Reorderable { get; set; } = true;
}

/// <summary>Adapts a data-defined <see cref="DataTableColumn{TItem}"/> to the grid's internal column shape.</summary>
internal sealed class DataTableColumnAdapter<TItem>(DataTableColumn<TItem> def, int ordinal) : IGridColumn<TItem>
{
    public string Key => !string.IsNullOrEmpty(def.Key) ? def.Key! : def.Title ?? $"col-{ordinal}";
    public string? Title => def.Title;
    public bool Sortable => def.Sort is not null || def.SortBy is not null;
    public bool Searchable => def.Text is not null;
    public bool DefaultHidden => def.Hidden;
    public string Align => def.Align;
    public string? HeaderClass => def.HeaderClass;
    public string? CellClass => def.CellClass;
    public RenderFragment? Header => def.Header;
    public double? Width => def.Width;
    public double? MinWidth => def.MinWidth;
    public double? MaxWidth => def.MaxWidth;
    public bool Fill => def.Fill;
    public DataTablePin Pinned => def.Pinned;
    public bool Resizable => def.Resizable;
    public bool Reorderable => def.Reorderable;
    public RenderFragment RenderCell(TItem item) => def.Cell(item);
    public string? GetText(TItem item) => def.Text?.Invoke(item);
    // Columns defined as data are read-only; editing needs a typed BzPropertyColumn.
    public IGridEditor<TItem>? Editor => null;

    public int Compare(TItem a, TItem b) =>
        def.Sort is { } sort ? sort.Compare(a, b)
        : def.SortBy is { } by ? Comparer<object?>.Default.Compare(by(a), by(b))
        : 0;
}
