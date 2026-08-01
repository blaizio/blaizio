using System.Diagnostics;
using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;

// Timed bUnit render harness for the audit's perf hotspots (audit batch 8). Not BenchmarkDotNet -
// the point is a cheap, repeatable before/after signal for consolidation work, not microsecond
// rigor. Run with `dotnet run -c Release --project tests/Blaizio.Base.Benchmarks`; results land in
// docs/benchmarks.md by hand.

var results = new List<(string Name, double MedianMs, double AllocMb)>();
var breaches = new List<string>();
// Wall-clock varies by machine; allocations are deterministic per runtime version. With
// BLAIZIO_BENCH_ASSERT=1 (CI) a scenario allocating over its ceiling fails the run - the
// ceilings sit ~50-100% above the medians recorded in docs/benchmarks.md, so only a real
// regression (a new per-item component, dictionary or merge) trips them.
var assertMode = Environment.GetEnvironmentVariable("BLAIZIO_BENCH_ASSERT") == "1";

Measure("Combobox 100 options (legacy indicator child)", () => RenderCombobox(100, fragment: false), allocCeilingMb: 3);
Measure("Combobox 1000 options (legacy indicator child)", () => RenderCombobox(1000, fragment: false), allocCeilingMb: 30);
Measure("Combobox 100 options (SelectedIndicator fragment)", () => RenderCombobox(100, fragment: true), allocCeilingMb: 3);
Measure("Combobox 1000 options (SelectedIndicator fragment)", () => RenderCombobox(1000, fragment: true), allocCeilingMb: 21);
Measure("Calendar month", RenderCalendar, allocCeilingMb: 4);
Measure("Tree 1000 nodes (all expanded)", () => RenderTree(virtualize: false), allocCeilingMb: 110);
Measure("Tree 1000 nodes (Virtualize)", () => RenderTree(virtualize: true), allocCeilingMb: 4);
Measure("DataTable 100 rows x 3 cols", () => RenderDataTable(100), allocCeilingMb: 2);
Measure("DataTable 1000 rows x 3 cols", () => RenderDataTable(1000), allocCeilingMb: 8);
Measure("DataTable 10000 rows x 3 cols", () => RenderDataTable(10_000), allocCeilingMb: 60);
// Bisection scenarios for the (former) 10k superlinearity: a raw-markup table isolates harness
// cost (bUnit render + AngleSharp parse) from BzDataTable's own work, and the two mitigations
// show what the escape hatches actually buy.
Measure("Plain markup table 1000 rows x 3 cols", () => RenderPlainTable(1000), allocCeilingMb: 2);
Measure("Plain markup table 10000 rows x 3 cols", () => RenderPlainTable(10_000), allocCeilingMb: 12);
Measure("DataTable 10000 x 3 (PageSize 50)", () => RenderDataTable(10_000, pageSize: 50), allocCeilingMb: 5);
Measure("DataTable 10000 x 3 (Virtualize)", () => RenderDataTable(10_000, virtualize: true), allocCeilingMb: 5);

Console.WriteLine();
Console.WriteLine("| Scenario | Median render (ms) | Allocated (MB) |");
Console.WriteLine("|----------|-------------------:|---------------:|");
foreach (var (name, ms, mb) in results)
    Console.WriteLine($"| {name} | {ms:0.0} | {mb:0.0} |");

if (breaches.Count > 0)
{
    Console.WriteLine();
    foreach (var breach in breaches)
        Console.WriteLine($"ALLOCATION CEILING EXCEEDED: {breach}");
    if (assertMode) return 1;
}

return 0;

void Measure(string name, Action render, double allocCeilingMb, int warmup = 1, int iterations = 5)
{
    for (var i = 0; i < warmup; i++) render();

    var times = new List<double>(iterations);
    var allocs = new List<double>(iterations);
    for (var i = 0; i < iterations; i++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        render();
        sw.Stop();
        times.Add(sw.Elapsed.TotalMilliseconds);
        allocs.Add((GC.GetAllocatedBytesForCurrentThread() - allocBefore) / (1024.0 * 1024.0));
    }

    times.Sort();
    allocs.Sort();
    var median = times[times.Count / 2];
    var alloc = allocs[allocs.Count / 2];
    results.Add((name, median, alloc));
    if (alloc > allocCeilingMb)
        breaches.Add($"{name}: {alloc:0.0} MB > ceiling {allocCeilingMb:0.0} MB");
    Console.WriteLine($"{name}: {median:0.0} ms, {alloc:0.0} MB");
}

static BunitContext NewContext()
{
    var ctx = new BunitContext();
    ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    return ctx;
}

// ---- scenarios ----

static void RenderCombobox(int options, bool fragment)
{
    using var ctx = NewContext();
    RenderFragment<bool> indicator = selected => b =>
    {
        if (selected) b.AddContent(0, "*");
    };
    RenderFragment items = b =>
    {
        for (var i = 0; i < options; i++)
        {
            b.OpenRegion(i);
            b.OpenComponent<BaseComboboxItem>(0);
            b.AddComponentParameter(1, nameof(BaseComboboxItem.Value), $"option-{i}");
            var index = i;
            if (fragment)
            {
                b.AddComponentParameter(2, nameof(BaseComboboxItem.ChildContent),
                    (RenderFragment)(x => x.AddContent(0, $"Option {index}")));
                b.AddComponentParameter(3, nameof(BaseComboboxItem.SelectedIndicator), indicator);
            }
            else
            {
                b.AddComponentParameter(2, nameof(BaseComboboxItem.ChildContent), (RenderFragment)(x =>
                {
                    x.AddContent(0, $"Option {index}");
                    x.OpenComponent<BaseComboboxItemIndicator>(1);
                    x.AddComponentParameter(2, nameof(BaseComboboxItemIndicator.ChildContent),
                        (RenderFragment)(ind => ind.AddContent(0, "*")));
                    x.CloseComponent();
                }));
            }
            b.CloseComponent();
            b.CloseRegion();
        }
    };
    RenderFragment body = b =>
    {
        b.OpenComponent<BaseComboboxInput>(0);
        b.CloseComponent();
        b.OpenComponent<BaseComboboxContent>(1);
        b.AddComponentParameter(2, nameof(BaseComboboxContent.ChildContent), (RenderFragment)(c =>
        {
            c.OpenComponent<BaseComboboxList>(0);
            c.AddComponentParameter(1, nameof(BaseComboboxList.ChildContent), items);
            c.CloseComponent();
        }));
        b.CloseComponent();
    };
    var cut = ctx.Render<BaseCombobox>(p => p
        .Add(x => x.DefaultOpen, true)
        .AddChildContent(body));
    if (cut.FindAll("[role=option]").Count != options)
        throw new InvalidOperationException("combobox scenario rendered wrong option count");
}

static void RenderCalendar()
{
    using var ctx = NewContext();
    var cut = ctx.Render<BzCalendar>(p => p
        .Add(x => x.DefaultMonth, new DateOnly(2026, 7, 1)));
    if (cut.FindAll("[data-slot=calendar-day]").Count < 28)
        throw new InvalidOperationException("calendar scenario rendered no days");
}

static void RenderTree(bool virtualize)
{
    // 100 roots x 9 children = 1000 nodes, all expanded so every node renders.
    var roots = Enumerable.Range(0, 100).Select(r => new Node(
        $"root-{r}",
        [.. Enumerable.Range(0, 9).Select(c => new Node($"node-{r}-{c}", []))])).ToList();
    var expanded = roots.Select(r => r.Id).ToArray();

    using var ctx = NewContext();
    var cut = ctx.Render<BaseTree<Node>>(p => p
        .Add(x => x.Items, roots)
        .Add(x => x.ValueSelector, n => n.Id)
        .Add(x => x.TextSelector, n => n.Id)
        .Add(x => x.ChildrenSelector, n => n.Children)
        .Add(x => x.DefaultExpandedValues, expanded)
        .Add(x => x.Virtualize, virtualize));
    var count = cut.FindAll("[role=treeitem]").Count;
    if (virtualize ? count is 0 or 1000 : count != 1000)
        throw new InvalidOperationException("tree scenario rendered wrong node count");
}

static void RenderDataTable(int rows, int? pageSize = null, bool virtualize = false)
{
    var data = Enumerable.Range(0, rows)
        .Select(i => new Row($"Name {i}", $"user{i}@example.com", i % 100))
        .ToList();
    IReadOnlyList<DataTableColumn<Row>> columns =
    [
        new() { Title = "Name", Cell = r => b => b.AddContent(0, r.Name), SortBy = r => r.Name },
        new() { Title = "Email", Cell = r => b => b.AddContent(0, r.Email), Text = r => r.Email },
        new() { Title = "Age", Cell = r => b => b.AddContent(0, r.Age), SortBy = r => r.Age },
    ];

    using var ctx = NewContext();
    var cut = ctx.Render<BzDataTable<Row>>(p =>
    {
        p.Add(x => x.Items, data).Add(x => x.Columns, columns);
        if (pageSize is { } size) p.Add(x => x.PageSize, size);
        if (virtualize) p.Add(x => x.Virtualize, true);
    });
    if (!cut.Markup.Contains("Name 0", StringComparison.Ordinal))
        throw new InvalidOperationException("table scenario rendered no rows");
}

static void RenderPlainTable(int rows)
{
    using var ctx = NewContext();
    RenderFragment frag = b =>
    {
        b.OpenElement(0, "table");
        b.OpenElement(1, "tbody");
        for (var i = 0; i < rows; i++)
        {
            b.OpenRegion(2);
            b.OpenElement(0, "tr");
            b.OpenElement(1, "td");
            b.AddContent(2, "Name ");
            b.AddContent(3, i);
            b.CloseElement();
            b.OpenElement(4, "td");
            b.AddContent(5, "user@example.com");
            b.CloseElement();
            b.OpenElement(6, "td");
            b.AddContent(7, i % 100);
            b.CloseElement();
            b.CloseElement();
            b.CloseRegion();
        }
        b.CloseElement();
        b.CloseElement();
    };
    var cut = ctx.Render(frag);
    if (!cut.Markup.Contains("Name 0", StringComparison.Ordinal))
        throw new InvalidOperationException("plain table scenario rendered no rows");
}

internal sealed record Node(string Id, List<Node> Children);

internal sealed record Row(string Name, string Email, int Age);
