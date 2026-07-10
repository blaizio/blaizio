using Microsoft.AspNetCore.Components;

namespace Blaizio.Ui;

/// <summary>How a chart series is drawn.</summary>
internal enum ChartSeriesKind
{
    Bar,
    Line,
    Area,
    Scatter,
}

/// <summary>One registered series of a <see cref="BzChart{TItem}"/> - name, accessor, paint, and shape.</summary>
internal sealed class ChartSeries<TItem>
{
    public required string Name { get; set; }
    public required Func<TItem, double> Value { get; set; }
    public string? Color { get; set; }
    public ChartSeriesKind Kind { get; set; }
    public bool Curved { get; set; }
}

/// <summary>
/// The cascade the series children (<see cref="BzChartBarSeries{TItem}"/> et al.) register with.
/// Implemented by every root that plots named series over the items - <see cref="BzChart{TItem}"/>
/// and <see cref="BzRadarChart{TItem}"/> - so the same series markup composes into either.
/// </summary>
internal interface IChartSeriesHost<TItem>
{
    void RegisterSeries(object owner, ChartSeries<TItem> definition);
    void UnregisterSeries(object owner);
}

/// <summary>
/// Non-generic surface of the chart root that the option children (axes, grid, tooltip, legend)
/// talk to, so they stay non-generic while the series parts infer TItem from the cascade.
/// </summary>
internal interface IChartRoot
{
    void SetXAxis(object owner, bool visible, int tickCount);
    void SetYAxis(object owner, bool visible, int tickCount, Func<double, string>? formatter);
    void SetGrid(object owner, bool visible, bool vertical);
    void SetTooltip(object owner, bool visible, RenderFragment<ChartTooltipData>? content);
    void SetLegend(object owner, bool visible, ChartLegendPosition position);
    void ClearOption(object owner);
}

/// <summary>Where a <see cref="BzChartLegend"/> renders relative to the plot.</summary>
public enum ChartLegendPosition
{
    /// <summary>Below the plot.</summary>
    Bottom,

    /// <summary>Above the plot.</summary>
    Top,
}

/// <summary>Everything a chart tooltip shows for the hovered category - the label plus one row per series.</summary>
/// <param name="Label">The hovered category's label (x value).</param>
/// <param name="Rows">One entry per visible series, in declaration order.</param>
public sealed record ChartTooltipData(string Label, IReadOnlyList<ChartTooltipRow> Rows);

/// <summary>One series row inside a chart tooltip.</summary>
/// <param name="Name">The series name.</param>
/// <param name="Color">The series paint (a CSS color, typically <c>var(--chart-n)</c>).</param>
/// <param name="Value">The raw value.</param>
/// <param name="Formatted">The value through the chart's formatter.</param>
public sealed record ChartTooltipRow(string Name, string Color, double Value, string Formatted);
