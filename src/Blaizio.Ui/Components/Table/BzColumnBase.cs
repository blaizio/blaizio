using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blaizio.Ui;

/// <summary>
/// Base for a <see cref="BzDataTable{TItem}"/> child column. Registers itself with the enclosing grid on
/// init (in document order, which becomes column order) and renders nothing of its own - the grid
/// reads the column back through <see cref="IGridColumn{TItem}"/> to build the header and cells. The
/// grid arranges its columns before they get that render's parameters (they are its child content),
/// so a column whose parameters changed asks the grid for one more render.
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
    /// A multi-key / tie-breaker sort for this column (e.g. <c>DataTableSort&lt;T&gt;.By(...).ThenBy(...)</c>).
    /// When set it overrides the column's default single-key sort.
    /// </summary>
    [Parameter] public DataTableSort<TItem>? Sort { get; set; }

    /// <summary>Whether the column starts visible. Set <see langword="false"/> to start it hidden (toggleable back on via the visibility menu). Defaults to <see langword="true"/>.</summary>
    [Parameter] public bool Visible { get; set; } = true;

    /// <summary>Cell text alignment: <c>start</c> (default), <c>center</c> or <c>end</c>.</summary>
    [Parameter] public string Align { get; set; } = "start";

    /// <summary>Extra classes for the header cell.</summary>
    [Parameter] public string? HeaderClass { get; set; }

    /// <summary>Extra classes for each body cell.</summary>
    [Parameter] public string? CellClass { get; set; }

    /// <summary>Custom header content; when unset the <see cref="Title"/> text is rendered.</summary>
    [Parameter] public RenderFragment? HeaderContent { get; set; }

    /// <summary>
    /// The column's starting width in pixels. Unset, the column sizes to its content, or takes the
    /// room left in the row with <see cref="Fill"/>. A width the user drags to (see the table's
    /// <c>ResizableColumns</c> and <c>ColumnWidths</c>) replaces it.
    /// </summary>
    [Parameter] public double? Width { get; set; }

    /// <summary>The narrowest the column gets in pixels, however it is sized: resizing, fitting or filling.</summary>
    [Parameter] public double? MinWidth { get; set; }

    /// <summary>The widest the column gets in pixels, however it is sized: resizing, fitting or filling.</summary>
    [Parameter] public double? MaxWidth { get; set; }

    /// <summary>
    /// Take the room the other columns leave in the row, shared with any other filling column and
    /// kept between <see cref="MinWidth"/> and <see cref="MaxWidth"/>. The other columns then keep
    /// their own widths instead of stretching.
    /// </summary>
    [Parameter] public bool Fill { get; set; }

    /// <summary>
    /// Keep the column in place while the rows scroll sideways: at the start edge or the end edge,
    /// which flip in a right-to-left page. Pinned columns sit before (or after) the others.
    /// </summary>
    [Parameter] public DataTablePin Pinned { get; set; }

    /// <summary>Whether a user can resize this column when the table's <c>ResizableColumns</c> is on. Defaults to <see langword="true"/>.</summary>
    [Parameter] public bool Resizable { get; set; } = true;

    /// <summary>
    /// Whether a user can move this column when the table's <c>ReorderableColumns</c> is on.
    /// Defaults to <see langword="true"/>. A column that stays put also keeps the others from
    /// crossing it.
    /// </summary>
    [Parameter] public bool Reorderable { get; set; } = true;

    /// <summary>
    /// Drop the cells' padding, so the cell's content spans it edge to edge: its root element fills
    /// the cell's full height, and a background on it fills the whole cell. The content then pads
    /// itself.
    /// </summary>
    [Parameter] public bool EdgeToEdge { get; set; }

    private bool _registered;

    // What the grid last read from this column: compared after each parameter set.
    private Shape _shape;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Grid is null)
            throw new InvalidOperationException($"{GetType().Name} must be placed inside a <BzDataTable>.");

        Grid.Register(this);
        _registered = true;
        // What the grid filtered and sorted by as it registered this column: a property column's
        // key, sorting and search text resolve in its OnParametersSet, which comes after.
        _shape = CurrentShape();
    }

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        var registering = !_registered;
        var task = base.SetParametersAsync(parameters);
        // The grid arranges and filters its columns before they get the render's parameters, so a
        // change it has not read asks it for one more render. Columns do no async lifecycle work:
        // the parameters are applied and resolved by now.
        if (_registered)
        {
            var shape = CurrentShape();
            if (shape != _shape)
            {
                var rows = !shape.SameRows(_shape);
                _shape = shape;
                Grid!.ColumnChanged(rows, registering);
            }
        }
        return task;
    }

    // The parameters the grid reads from a column, as far as they compare by value (templates and
    // accessors are new objects on every render of the page). The rows part is what the in-memory
    // filter and sort read too.
    private readonly record struct Shape(
        string Key, bool Sortable, bool Searchable, string? Format,
        string? Title, string Align, string? HeaderClass, string? CellClass,
        double? Width, double? MinWidth, double? MaxWidth, bool Fill, DataTablePin Pinned,
        bool Resizable, bool Reorderable, DataTableEditor? Editor, bool EdgeToEdge, bool EditActions)
    {
        public bool SameRows(Shape other) =>
            Key == other.Key && Sortable == other.Sortable && Searchable == other.Searchable && Format == other.Format;
    }

    private Shape CurrentShape()
    {
        IGridColumn<TItem> column = this;
        return new(column.Key, column.Sortable, column.Searchable, CellFormat, column.Title, Align, HeaderClass,
            CellClass, Width, MinWidth, MaxWidth, Fill, Pinned, Resizable, Reorderable, GridEditor?.Kind,
            EdgeToEdge, HoldsEditActions);
    }

    /// <summary>The format the cells and their search text are written in, for a column type that has one.</summary>
    private protected virtual string? CellFormat => null;

    string IGridColumn<TItem>.Key => ResolveKey();
    string? IGridColumn<TItem>.Title => ResolveTitle();
    bool IGridColumn<TItem>.Sortable => Sortable && (Sort is not null || CanSort);
    bool IGridColumn<TItem>.Searchable => HasText;
    bool IGridColumn<TItem>.DefaultHidden => !Visible;
    string IGridColumn<TItem>.Align => Align;
    string? IGridColumn<TItem>.HeaderClass => FrameClass is { } frame ? Tw.Merge(frame, HeaderClass) : HeaderClass;
    string? IGridColumn<TItem>.CellClass => FrameClass is { } frame ? Tw.Merge(frame, CellClass) : CellClass;
    RenderFragment? IGridColumn<TItem>.Header => HeaderFragment;
    double? IGridColumn<TItem>.Width => Width;
    double? IGridColumn<TItem>.MinWidth => MinWidth;
    double? IGridColumn<TItem>.MaxWidth => MaxWidth;
    bool IGridColumn<TItem>.Fill => Fill;
    DataTablePin IGridColumn<TItem>.Pinned => Pinned;
    bool IGridColumn<TItem>.Resizable => Resizable;
    bool IGridColumn<TItem>.Reorderable => Reorderable;
    bool IGridColumn<TItem>.EditActions => HoldsEditActions;
    bool IGridColumn<TItem>.RowEditsOnly => RowEditsOnly;
    bool IGridColumn<TItem>.EdgeToEdge => EdgeToEdge;
    RenderFragment IGridColumn<TItem>.RenderCell(TItem item, DataTableCellState state) => RenderCell(item, state);
    int IGridColumn<TItem>.Compare(TItem a, TItem b) =>
        Sort is { } sort ? sort.Compare(a, b) : CanSort ? CompareCore(a, b) : 0;
    string? IGridColumn<TItem>.GetText(TItem item) => GetTextCore(item);
    IGridEditor<TItem>? IGridColumn<TItem>.Editor => GridEditor;

    /// <summary>The column's editing surface; <see langword="null"/> (read-only) unless a column type opts in.</summary>
    internal virtual IGridEditor<TItem>? GridEditor => null;

    /// <summary>Whether the column's controls act on their row's edit (an edit actions column).</summary>
    internal virtual bool HoldsEditActions => false;

    /// <summary>Whether the column only shows while rows edit as a whole (Row, Dialog and Drawer modes).</summary>
    internal virtual bool RowEditsOnly => false;

    /// <summary>Classes a column type always gives its header and body cells, before HeaderClass / CellClass.</summary>
    internal virtual string? FrameClass => null;

    /// <summary>The header content: <see cref="HeaderContent"/>, unless a column type has a default of its own.</summary>
    internal virtual RenderFragment? HeaderFragment => HeaderContent;

    /// <summary>The key when neither <see cref="ColumnKey"/> nor a title gives one.</summary>
    internal virtual string? FallbackKey => null;

    /// <summary>The cell for one row, given its state: a column type with a state-aware template renders that.</summary>
    internal virtual RenderFragment RenderCell(TItem item, DataTableCellState state) => RenderCell(item);

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
        !string.IsNullOrEmpty(ColumnKey) ? ColumnKey! : ResolveTitle() ?? FallbackKey ?? GetHashCode().ToString();

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
