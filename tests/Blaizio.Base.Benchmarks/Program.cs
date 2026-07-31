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

Measure("Combobox 100 options (legacy indicator child)", () => RenderCombobox(100, fragment: false));
Measure("Combobox 1000 options (legacy indicator child)", () => RenderCombobox(1000, fragment: false));
Measure("Combobox 100 options (SelectedIndicator fragment)", () => RenderCombobox(100, fragment: true));
Measure("Combobox 1000 options (SelectedIndicator fragment)", () => RenderCombobox(1000, fragment: true));
Measure("Calendar month", RenderCalendar);
Measure("Tree 1000 nodes (all expanded)", RenderTree);
Measure("DataTable 100 rows x 3 cols", () => RenderDataTable(100));
Measure("DataTable 1000 rows x 3 cols", () => RenderDataTable(1000));
Measure("DataTable 10000 rows x 3 cols", () => RenderDataTable(10_000));

Console.WriteLine();
Console.WriteLine("| Scenario | Median render (ms) | Allocated (MB) |");
Console.WriteLine("|----------|-------------------:|---------------:|");
foreach (var (name, ms, mb) in results)
    Console.WriteLine($"| {name} | {ms:0.0} | {mb:0.0} |");

return;

void Measure(string name, Action render, int warmup = 1, int iterations = 5)
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

static void RenderTree()
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
        .Add(x => x.DefaultExpandedValues, expanded));
    if (cut.FindAll("[role=treeitem]").Count != 1000)
        throw new InvalidOperationException("tree scenario rendered wrong node count");
}

static void RenderDataTable(int rows)
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
    var cut = ctx.Render<BzDataTable<Row>>(p => p
        .Add(x => x.Items, data)
        .Add(x => x.Columns, columns));
    if (!cut.Markup.Contains("Name 0", StringComparison.Ordinal))
        throw new InvalidOperationException("table scenario rendered no rows");
}

internal sealed record Node(string Id, List<Node> Children);

internal sealed record Row(string Name, string Email, int Age);
