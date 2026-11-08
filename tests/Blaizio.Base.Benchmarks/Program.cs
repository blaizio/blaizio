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
// Editing: an editable table renders like a read-only one (the editors, the keyboard and the
// cursor cost nothing until used), and opening + committing a cell is two passes over the body.
Measure("DataTable 1000 x 3 property columns", () => RenderPropertyTable(1000, editable: false), allocCeilingMb: 8);
Measure("DataTable 1000 x 3 property columns (editable)", () => RenderPropertyTable(1000, editable: true), allocCeilingMb: 8);
Measure("DataTable 1000 x 3 (editable): open + commit a cell", () => EditPropertyTable(1000), allocCeilingMb: 20);
// Column features: a pinned, sized, resizable and movable table renders like a plain one (the
// widths live in a colgroup; a pinned cell is two constant attributes).
Measure("DataTable 1000 x 3 (pinned, sized, movable columns)", () => RenderDataTable(1000, columnFeatures: true), allocCeilingMb: 8);
// Syntax highlighting: the tokenizer alone, a highlighted block's first render, and output
// streaming in a line at a time (each update must re-render only the new line).
Measure("SyntaxText C# 600 lines", () => SyntaxText.Tokenize(CSharpSample(50), "csharp"), allocCeilingMb: 1);
Measure("CodeBlock C# 600 lines highlighted", () => RenderCodeBlock(CSharpSample(50), "csharp"), allocCeilingMb: 25);
Measure("CodeBlock C# 600 lines numbered, plain", () => RenderCodeBlock(CSharpSample(50), null), allocCeilingMb: 20);
Measure("CodeBlock C# 120 lines streamed line by line", () => StreamCodeBlock(CSharpSample(10)), allocCeilingMb: 85, iterations: 3);

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

static void RenderDataTable(int rows, int? pageSize = null, bool virtualize = false, bool columnFeatures = false)
{
    var data = Enumerable.Range(0, rows)
        .Select(i => new Row($"Name {i}", $"user{i}@example.com", i % 100))
        .ToList();
    IReadOnlyList<DataTableColumn<Row>> columns =
    [
        new() { Title = "Name", Cell = r => b => b.AddContent(0, r.Name), SortBy = r => r.Name,
                Pinned = columnFeatures ? DataTablePin.Start : DataTablePin.None, Width = columnFeatures ? 160 : null },
        new() { Title = "Email", Cell = r => b => b.AddContent(0, r.Email), Text = r => r.Email, Fill = columnFeatures },
        new() { Title = "Age", Cell = r => b => b.AddContent(0, r.Age), SortBy = r => r.Age },
    ];

    using var ctx = NewContext();
    var cut = ctx.Render<BzDataTable<Row>>(p =>
    {
        p.Add(x => x.Items, data).Add(x => x.Columns, columns);
        if (pageSize is { } size) p.Add(x => x.PageSize, size);
        if (virtualize) p.Add(x => x.Virtualize, true);
        if (columnFeatures) p.Add(x => x.ResizableColumns, true).Add(x => x.ReorderableColumns, true);
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

static string CSharpSample(int repeat) => string.Concat(Enumerable.Repeat("""
    /// <summary>Loads an order and its lines.</summary>
    public async Task<Order?> FindAsync(int id, CancellationToken token = default)
    {
        // Cached orders skip the database.
        if (_cache.TryGetValue(id, out var cached)) return cached;
        var order = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, token);
        _logger.LogInformation($"Loaded order {id} with {order?.Lines.Count ?? 0} lines");
        return order is null ? null : _cache[id] = order with { LoadedAt = DateTime.UtcNow };
    }

    [Parameter] public string Title { get; set; } = "Orders";

    """.ReplaceLineEndings("\n"), repeat));

static void RenderCodeBlock(string code, string? language)
{
    using var ctx = NewContext();
    var cut = ctx.Render<BzCodeBlockContent>(p => p
        .Add(x => x.Code, code)
        .Add(x => x.Language, language)
        .Add(x => x.LineNumbers, true));
    if (cut.FindAll("[data-slot=code-block-line]").Count < 100)
        throw new InvalidOperationException("code block scenario rendered too few lines");
}

static void StreamCodeBlock(string code)
{
    var lines = code.Split('\n');
    using var ctx = NewContext();
    var cut = ctx.Render<BzCodeBlockContent>(p => p.Add(x => x.Code, lines[0]).Add(x => x.Language, "csharp"));
    for (var i = 2; i <= lines.Length; i++)
    {
        var prefix = string.Join('\n', lines, 0, i);
        cut.Render(p => p.Add(x => x.Code, prefix).Add(x => x.Language, "csharp"));
    }
    // The first line rendered once, however many lines streamed in after it (the last line of each
    // update was the one growing, so only the newest lines ever render twice).
    var rendered = cut.FindComponents<BzCodeBlockLine>();
    if (rendered[0].RenderCount != 1 || rendered.Count(l => l.RenderCount > 1) > 1)
        throw new InvalidOperationException(
            $"streaming re-rendered unchanged lines: first {rendered[0].RenderCount}x, {rendered.Count(l => l.RenderCount > 1)} lines more than once");

    // Switching the text and language: a line that turned empty shows nothing of its old text.
    cut.Render(p => p.Add(x => x.Code, "{\n\n}").Add(x => x.Language, "json"));
    var lineTexts = cut.FindAll("[data-slot=code-block-line-content]").Select(l => l.TextContent).ToList();
    if (lineTexts.Count != 3 || lineTexts[1] != "\n")
        throw new InvalidOperationException($"a line that turned empty kept old content: '{lineTexts.ElementAtOrDefault(1)}'");
}

static IRenderedComponent<BzDataTable<EditRow>> RenderPropertyTableCore(BunitContext ctx, int rows, bool editable)
{
    var data = Enumerable.Range(0, rows)
        .Select(i => new EditRow { Name = $"Name {i}", Email = $"user{i}@example.com", Age = i % 100 })
        .ToList();
    RenderFragment columns = b =>
    {
        b.OpenComponent<BzPropertyColumn<EditRow, string>>(0);
        b.AddComponentParameter(1, "Property", (System.Linq.Expressions.Expression<Func<EditRow, string>>)(r => r.Name));
        b.AddComponentParameter(2, "Editable", editable);
        b.CloseComponent();
        b.OpenComponent<BzPropertyColumn<EditRow, string>>(3);
        b.AddComponentParameter(4, "Property", (System.Linq.Expressions.Expression<Func<EditRow, string>>)(r => r.Email));
        b.AddComponentParameter(5, "Editable", editable);
        b.CloseComponent();
        b.OpenComponent<BzPropertyColumn<EditRow, int>>(6);
        b.AddComponentParameter(7, "Property", (System.Linq.Expressions.Expression<Func<EditRow, int>>)(r => r.Age));
        b.AddComponentParameter(8, "Editable", editable);
        b.CloseComponent();
    };
    var cut = ctx.Render<BzDataTable<EditRow>>(p => p.Add(x => x.Items, data).Add(x => x.ChildContent, columns));
    if (!cut.Markup.Contains("Name 0", StringComparison.Ordinal))
        throw new InvalidOperationException("property table scenario rendered no rows");
    if (editable != cut.Markup.Contains("role=\"grid\"", StringComparison.Ordinal))
        throw new InvalidOperationException("property table scenario rendered the wrong role");
    return cut;
}

static void RenderPropertyTable(int rows, bool editable)
{
    using var ctx = NewContext();
    RenderPropertyTableCore(ctx, rows, editable);
}

static void EditPropertyTable(int rows)
{
    using var ctx = NewContext();
    var cut = RenderPropertyTableCore(ctx, rows, editable: true);
    var table = cut.Instance;
    cut.InvokeAsync(() => table.BeginEdit(0, 0, "N", "typing", false)).GetAwaiter().GetResult();
    cut.InvokeAsync(() => table.EndEdit(true, new Dictionary<string, string?> { ["Name"] = "Renamed" }, false)).GetAwaiter().GetResult();
    if (!cut.Markup.Contains("Renamed", StringComparison.Ordinal))
        throw new InvalidOperationException("edit scenario did not commit");
}

internal sealed record Node(string Id, List<Node> Children);

internal sealed record Row(string Name, string Email, int Age);

internal sealed class EditRow
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
}
