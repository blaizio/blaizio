using System.Globalization;
using System.Text;

namespace Blaizio.Ui;

/// <summary>
/// The chart's resolved geometry: plot rectangle, nice-number ticks, value/category scales, and
/// the per-kind draw primitives (bars, line/area paths, scatter points, hover strips, tooltip
/// anchor). Pure computation over the values <see cref="BzChart{TItem}"/> extracted from its data
/// and children - no component state, so it is constructible and testable on its own.
/// </summary>
internal sealed class ChartLayout<TItem>
{
    public IReadOnlyList<ChartSeries<TItem>> Series { get; }
    public double[][] Values { get; }
    public string[] Labels { get; }
    public string[] Colors { get; }
    public int Count { get; }

    public List<int> BarSeries { get; } = [];
    private readonly List<int> _lineSeries = [];
    private readonly List<int> _areaSeries = [];
    private readonly List<int> _scatterSeries = [];

    private readonly double _width;
    private readonly double _height;
    private readonly bool _stacked;
    private readonly bool _rtl;
    private readonly double _lo;
    private readonly double _hi;
    private readonly double[]? _x;
    private readonly double _xLo;
    private readonly double _xHi;

    public double PlotLeft { get; }
    public double PlotTop { get; }
    public double PlotRight { get; }
    public double PlotBottom { get; }
    public double PlotWidth => PlotRight - PlotLeft;
    public double PlotHeight => PlotBottom - PlotTop;
    public double BandWidth => PlotWidth / Count;

    public IReadOnlyList<double> Ticks { get; }
    public IReadOnlyList<double> XTicks { get; } = [];

    /// <summary>True when the chart plots against a numeric x axis (XValue) instead of category bands.</summary>
    public bool NumericX => _x is not null;

    public ChartLayout(
        List<ChartSeries<TItem>> series, double[][] values, string[] labels, string[] colors, int count, double[]? xValues,
        double width, double height, bool stacked, bool rtl,
        bool yAxis, bool xAxis, int yTickCount, int xTickCount)
    {
        (Series, Values, Labels, Colors, Count) = (series, values, labels, colors, count);
        (_width, _height, _stacked, _rtl) = (width, height, stacked, rtl);
        _x = xValues;

        for (var s = 0; s < series.Count; s++)
        {
            switch (series[s].Kind)
            {
                case ChartSeriesKind.Bar: BarSeries.Add(s); break;
                case ChartSeriesKind.Line: _lineSeries.Add(s); break;
                case ChartSeriesKind.Area: _areaSeries.Add(s); break;
                case ChartSeriesKind.Scatter: _scatterSeries.Add(s); break;
            }
        }

        // The value-axis gutter follows the reading direction: left in LTR, right in RTL.
        var axisGutter = yAxis ? 42.0 : 8.0;
        PlotLeft = _rtl ? 8 : axisGutter;
        PlotTop = 12;
        PlotRight = _width - (_rtl ? axisGutter : 8);
        PlotBottom = _height - (xAxis ? 26 : 8);

        var (min, max) = Domain();
        (_lo, _hi, var step) = Nice(min, max, yTickCount);
        var ticks = new List<double>();
        for (var t = _lo; t <= _hi + step / 2; t += step) ticks.Add(Math.Round(t, 10));
        Ticks = ticks;

        if (_x is not null)
        {
            (_xLo, _xHi, var xStep) = Nice(_x.Min(), _x.Max(), xTickCount);
            var xTicks = new List<double>();
            for (var t = _xLo; t <= _xHi + xStep / 2; t += xStep) xTicks.Add(Math.Round(t, 10));
            XTicks = xTicks;
        }
    }

    private (double Min, double Max) Domain()
    {
        double min = 0, max = double.MinValue;

        for (var i = 0; i < Count; i++)
        {
            double barPos = 0, barNeg = 0, areaPos = 0, areaNeg = 0;
            foreach (var s in BarSeries) Accumulate(Values[s][i], ref barPos, ref barNeg, ref min, ref max);
            foreach (var s in _areaSeries) Accumulate(Values[s][i], ref areaPos, ref areaNeg, ref min, ref max);
            foreach (var s in _lineSeries)
            {
                min = Math.Min(min, Values[s][i]);
                max = Math.Max(max, Values[s][i]);
            }
            foreach (var s in _scatterSeries)
            {
                min = Math.Min(min, Values[s][i]);
                max = Math.Max(max, Values[s][i]);
            }

            if (_stacked)
            {
                max = Math.Max(max, Math.Max(barPos, areaPos));
                min = Math.Min(min, Math.Min(barNeg, areaNeg));
            }
        }

        if (max <= min) max = min + 1;
        return (min, max);
    }

    private void Accumulate(double value, ref double pos, ref double neg, ref double min, ref double max)
    {
        if (value >= 0) pos += value; else neg += value;
        if (!_stacked)
        {
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
    }

    private static (double Lo, double Hi, double Step) Nice(double min, double max, int tickCount)
    {
        var raw = (max - min) / Math.Max(1, tickCount);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var normalized = raw / magnitude;
        var step = magnitude * (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10);
        return (Math.Floor(min / step) * step, Math.Ceiling(max / step) * step, step);
    }

    public double Y(double value) => PlotBottom - PlotHeight * (value - _lo) / (_hi - _lo);

    /// <summary>Category bands flow with the reading direction - from the right edge under RTL.</summary>
    public double BandStart(int index) => _rtl
        ? PlotRight - BandWidth * (index + 1)
        : PlotLeft + BandWidth * index;
    public double BandCenter(int index) => BandStart(index) + BandWidth / 2;

    /// <summary>Maps a numeric x value into the plot, mirrored under RTL.</summary>
    public double MapX(double value)
    {
        var fraction = _xHi == _xLo ? 0.5 : (value - _xLo) / (_xHi - _xLo);
        return _rtl ? PlotRight - PlotWidth * fraction : PlotLeft + PlotWidth * fraction;
    }

    /// <summary>Item i's x position - its numeric value on a numeric axis, its band center otherwise.</summary>
    public double XPos(int index) => _x is not null ? MapX(_x[index]) : BandCenter(index);

    public readonly record struct HitStrip(double X, double W, int Index);

    /// <summary>
    /// Hover targets: category bands, or - on a numeric axis - one strip per item reaching
    /// halfway to its sorted neighbours, so the nearest point always wins.
    /// </summary>
    public IEnumerable<HitStrip> HitStrips
    {
        get
        {
            if (_x is null)
            {
                for (var i = 0; i < Count; i++) yield return new HitStrip(BandStart(i), BandWidth, i);
                yield break;
            }

            var order = Enumerable.Range(0, Count).OrderBy(i => XPos(i)).ToArray();
            for (var k = 0; k < order.Length; k++)
            {
                var x = XPos(order[k]);
                var left = k == 0 ? PlotLeft : (XPos(order[k - 1]) + x) / 2;
                var right = k == order.Length - 1 ? PlotRight : (x + XPos(order[k + 1])) / 2;
                if (right > left) yield return new HitStrip(left, right - left, order[k]);
            }
        }
    }

    /// <summary>Show every n-th x label so long series don't collide.</summary>
    public int LabelStep => Math.Max(1, (int)Math.Ceiling(Count / 10.0));

    public readonly record struct BarRect(double X, double Y, double W, double H, string Color, int Index);
    public readonly record struct SeriesPath(string Stroke, string Fill, string Color, int Index, bool Gradient);
    public readonly record struct Dot(double X, double Y, string Color);

    public IEnumerable<BarRect> Bars
    {
        get
        {
            if (BarSeries.Count == 0) yield break;

            if (_stacked)
            {
                var barWidth = Math.Min(BandWidth * 0.6, 40);
                for (var i = 0; i < Count; i++)
                {
                    double pos = 0, neg = 0;
                    var x = BandCenter(i) - barWidth / 2;
                    foreach (var s in BarSeries)
                    {
                        var value = Values[s][i];
                        var from = value >= 0 ? pos : neg;
                        var to = from + value;
                        if (value >= 0) pos = to; else neg = to;
                        yield return Rect(x, barWidth, from, to, Colors[s], i);
                    }
                }
                yield break;
            }

            var slot = Math.Min(BandWidth * 0.7 / BarSeries.Count, 36);
            for (var i = 0; i < Count; i++)
            {
                var groupStart = BandCenter(i) - slot * BarSeries.Count / 2;
                for (var g = 0; g < BarSeries.Count; g++)
                {
                    var s = BarSeries[g];
                    yield return Rect(groupStart + slot * g + slot * 0.06, slot * 0.88, 0, Values[s][i], Colors[s], i);
                }
            }
        }
    }

    private BarRect Rect(double x, double width, double from, double to, string color, int index)
    {
        var y1 = Y(Math.Max(from, to));
        var y2 = Y(Math.Min(from, to));
        return new BarRect(x, y1, width, Math.Max(y2 - y1, 0), color, index);
    }

    public IEnumerable<SeriesPath> LinePaths
    {
        get
        {
            foreach (var s in _lineSeries)
            {
                var points = PointsFor(s, stackedBase: null);
                yield return new SeriesPath(Series[s].Curved ? MonotonePath(points) : LinearPath(points), "", Colors[s], s, false);
            }
        }
    }

    public IEnumerable<SeriesPath> AreaPaths
    {
        get
        {
            var cumulative = new double[Count];
            foreach (var s in _areaSeries)
            {
                double[]? baseline = null;
                if (_stacked)
                {
                    baseline = (double[])cumulative.Clone();
                    for (var i = 0; i < Count; i++) cumulative[i] += Values[s][i];
                }

                var points = PointsFor(s, baseline);
                var curved = Series[s].Curved;
                var top = curved ? MonotonePath(points) : LinearPath(points);

                var fill = new StringBuilder(top);
                if (baseline is null)
                {
                    fill.Append($" L {F(points[^1].X)} {F(Y(0))} L {F(points[0].X)} {F(Y(0))} Z");
                }
                else
                {
                    // Close along the layer below, traversed backwards. Curved layers curve the
                    // base too - Fritsch-Carlson is symmetric under reversal, so this base is
                    // exactly the previous layer's top and the stack shows no seams.
                    var basePoints = new List<(double X, double Y)>(Count);
                    for (var i = Count - 1; i >= 0; i--) basePoints.Add((XPos(i), Y(baseline[i])));

                    var basePath = curved ? MonotonePath(basePoints) : LinearPath(basePoints);
                    // Swap the base path's leading "M x y" for an "L x y" so it joins the outline.
                    fill.Append(" L").Append(basePath.AsSpan(1)).Append(" Z");
                }

                yield return new SeriesPath(top, fill.ToString(), Colors[s], s, Series[s].Gradient);
            }
        }
    }

    public readonly record struct Point(double X, double Y, string Color, int Index);

    /// <summary>Every scatter-series marker, always visible (unlike the hover dots).</summary>
    public IEnumerable<Point> ScatterPoints
    {
        get
        {
            foreach (var s in _scatterSeries)
            {
                for (var i = 0; i < Count; i++)
                    yield return new Point(XPos(i), Y(Values[s][i]), Colors[s], i);
            }
        }
    }

    public IEnumerable<Dot> DotsAt(int index)
    {
        var cumulative = 0d;
        for (var s = 0; s < Series.Count; s++)
        {
            if (Series[s].Kind is ChartSeriesKind.Bar or ChartSeriesKind.Scatter) continue;
            var value = Values[s][index];
            if (Series[s].Kind == ChartSeriesKind.Area && _stacked)
            {
                cumulative += value;
                value = cumulative;
            }
            yield return new Dot(XPos(index), Y(value), Colors[s]);
        }
    }

    public (double LeftPct, double TopPct) TooltipAnchor(int index)
    {
        var top = double.MaxValue;
        double barPos = 0, areaPos = 0;
        for (var s = 0; s < Series.Count; s++)
        {
            var value = Values[s][index];
            if (_stacked)
            {
                if (Series[s].Kind == ChartSeriesKind.Bar && value >= 0) { barPos += value; value = barPos; }
                else if (Series[s].Kind == ChartSeriesKind.Area) { areaPos += value; value = areaPos; }
            }
            top = Math.Min(top, Y(Math.Max(value, 0)));
        }

        if (top == double.MaxValue) top = PlotTop;
        return (Math.Clamp(XPos(index) / _width * 100, 8, 92), Math.Clamp(top / _height * 100, 4, 96));
    }

    private List<(double X, double Y)> PointsFor(int seriesIndex, double[]? stackedBase)
    {
        var points = new List<(double, double)>(Count);
        for (var i = 0; i < Count; i++)
        {
            var value = Values[seriesIndex][i] + (stackedBase?[i] ?? 0);
            points.Add((XPos(i), Y(value)));
        }
        return points;
    }

    private static string LinearPath(List<(double X, double Y)> points)
    {
        var path = new StringBuilder($"M {F(points[0].X)} {F(points[0].Y)}");
        for (var i = 1; i < points.Count; i++) path.Append($" L {F(points[i].X)} {F(points[i].Y)}");
        return path.ToString();
    }

    /// <summary>Fritsch-Carlson monotone cubic - smooth but never overshoots the data.</summary>
    private static string MonotonePath(List<(double X, double Y)> points)
    {
        var n = points.Count;
        if (n < 2) return n == 1 ? $"M {F(points[0].X)} {F(points[0].Y)}" : "";
        if (n == 2) return LinearPath(points);

        var slopes = new double[n - 1];
        for (var i = 0; i < n - 1; i++)
        {
            var dx = points[i + 1].X - points[i].X;
            slopes[i] = dx == 0 ? 0 : (points[i + 1].Y - points[i].Y) / dx;
        }

        var tangents = new double[n];
        tangents[0] = slopes[0];
        tangents[n - 1] = slopes[^1];
        for (var i = 1; i < n - 1; i++)
            tangents[i] = slopes[i - 1] * slopes[i] <= 0 ? 0 : (slopes[i - 1] + slopes[i]) / 2;

        for (var i = 0; i < n - 1; i++)
        {
            if (slopes[i] == 0)
            {
                tangents[i] = 0;
                tangents[i + 1] = 0;
                continue;
            }
            var a = tangents[i] / slopes[i];
            var b = tangents[i + 1] / slopes[i];
            var h = a * a + b * b;
            if (h > 9)
            {
                var t = 3 / Math.Sqrt(h);
                tangents[i] = t * a * slopes[i];
                tangents[i + 1] = t * b * slopes[i];
            }
        }

        var path = new StringBuilder($"M {F(points[0].X)} {F(points[0].Y)}");
        for (var i = 0; i < n - 1; i++)
        {
            var dx = (points[i + 1].X - points[i].X) / 3;
            path.Append($" C {F(points[i].X + dx)} {F(points[i].Y + tangents[i] * dx)}, {F(points[i + 1].X - dx)} {F(points[i + 1].Y - tangents[i + 1] * dx)}, {F(points[i + 1].X)} {F(points[i + 1].Y)}");
        }
        return path.ToString();
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
