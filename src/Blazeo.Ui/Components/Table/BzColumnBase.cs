using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazeo.Ui;

/// <summary>
/// Base for a <see cref="BzDataTable{TItem}"/> child column. Registers itself with the enclosing grid on
/// init (in document order, which becomes column order) and renders nothing of its own - the grid
/// reads the column back through <see cref="IGridColumn{TItem}"/> to build the header and cells, so a
/// parameter change here reflects live without re-registering.
/// </summary>
/// <typeparam name="TItem">The row data type, cascaded from the <see cref="BzDataTable{TItem}"/>.</typeparam>
public abstract class BzColumnBase<TItem> : ComponentBase, IGridColumn<TItem>, IDisposable
{
    [CascadingParameter] internal BzDataTable<TItem>? Grid { get; set; }

    /// <summary>Header label and column-visibility-menu label.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Stable id for this column's sort + visibility state. Defaults to the title (or property name).</summary>
    [Parameter] public string? ColumnKey { get; set; }

    /// <summary>Offer a sort toggle on the header (requires a comparison to be available).</summary>
    [Parameter] public bool Sortable { get; set; }

    /// <summary>
    /// A multi-key / tie-breaker sort for this column (e.g. <c>GridSort&lt;T&gt;.By(...).ThenBy(...)</c>).
    /// When set it overrides the column's default single-key sort.
    /// </summary>
    [Parameter] public GridSort<TItem>? Sort { get; set; }

    /// <summary>Start the column hidden; toggle it back on via the visibility menu.</summary>
    [Parameter] public bool Hidden { get; set; }

    /// <summary>Cell text alignment: <c>start</c> (default), <c>center</c> or <c>end</c>.</summary>
    [Parameter] public string Align { get; set; } = "start";

    /// <summary>Extra classes for the header cell.</summary>
    [Parameter] public string? HeaderClass { get; set; }

    /// <summary>Extra classes for each body cell.</summary>
    [Parameter] public string? CellClass { get; set; }

    /// <summary>Custom header content; when unset the <see cref="Title"/> text is rendered.</summary>
    [Parameter] public RenderFragment? HeaderContent { get; set; }

    private bool _registered;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Grid is null)
            throw new InvalidOperationException($"{GetType().Name} must be placed inside a <BzDataTable>.");

        Grid.Register(this);
        _registered = true;
    }

    string IGridColumn<TItem>.Key => ResolveKey();
    string? IGridColumn<TItem>.Title => ResolveTitle();
    bool IGridColumn<TItem>.Sortable => Sortable && (Sort is not null || CanSort);
    bool IGridColumn<TItem>.Searchable => HasText;
    bool IGridColumn<TItem>.DefaultHidden => Hidden;
    string IGridColumn<TItem>.Align => Align;
    string? IGridColumn<TItem>.HeaderClass => HeaderClass;
    string? IGridColumn<TItem>.CellClass => CellClass;
    RenderFragment? IGridColumn<TItem>.Header => HeaderContent;
    RenderFragment IGridColumn<TItem>.RenderCell(TItem item) => RenderCell(item);
    int IGridColumn<TItem>.Compare(TItem a, TItem b) =>
        Sort is { } sort ? sort.Compare(a, b) : CanSort ? CompareCore(a, b) : 0;
    string? IGridColumn<TItem>.GetText(TItem item) => GetTextCore(item);

    /// <summary>The cell content for one row.</summary>
    protected abstract RenderFragment RenderCell(TItem item);

    /// <summary>Whether a comparison is actually available (e.g. a property or a SortBy accessor was given).</summary>
    protected abstract bool CanSort { get; }

    /// <summary>Ascending comparison of two rows by this column.</summary>
    protected abstract int CompareCore(TItem a, TItem b);

    /// <summary>The row's searchable text for this column, or <see langword="null"/> when it has none.</summary>
    protected abstract string? GetTextCore(TItem item);

    /// <summary>Whether this column can contribute search text at all.</summary>
    protected abstract bool HasText { get; }

    /// <summary>The display title - overridden by <see cref="BzPropertyColumn{TItem,TProp}"/> to fall back to the member name.</summary>
    protected virtual string? ResolveTitle() => Title;

    private string ResolveKey() =>
        !string.IsNullOrEmpty(ColumnKey) ? ColumnKey! : ResolveTitle() ?? GetHashCode().ToString();

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // A column emits no markup; the grid renders the table from the registered columns.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_registered) Grid?.Unregister(this);
    }
}
