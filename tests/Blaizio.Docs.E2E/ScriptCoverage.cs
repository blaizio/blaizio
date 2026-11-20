using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Blaizio.Docs.E2E;

/// <summary>
/// Opt-in measurement (<c>BLAIZIO_E2E_SCRIPT_COVERAGE=1</c>): which functions of each Blaizio.Base
/// script module ran, and in which test. The browser's own profiler does the counting, so nothing
/// is instrumented and the modules are the ones the docs app ships. The result,
/// <c>ScriptCoverage/modules.json</c>, answers "which browser test reaches this module, if any".
/// The docs pages use many modules themselves (table of contents, theme, toasts), so a test only
/// counts for a module when it called a function that merely opening a page does not call: the
/// routing and axe tests, which open pages and do nothing else, set that baseline.
/// </summary>
internal static partial class ScriptCoverage
{
    public static bool Enabled => Environment.GetEnvironmentVariable("BLAIZIO_E2E_SCRIPT_COVERAGE") == "1";

    // module -> function key ("name@start-end") -> the tests that called it (empty = never called)
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>> s_modules = new();

    /// <summary>The test a context belongs to, from where <c>NewContextAsync</c> was called.</summary>
    public static string TestName(string file, string member) => $"{Path.GetFileNameWithoutExtension(file)}.{member}";

    /// <summary>
    /// Wraps <paramref name="context"/> so the profiler runs on every page it opens and is read
    /// one last time when the test closes the context. The wrapper forwards everything else.
    /// </summary>
    public static IBrowserContext Watch(IBrowserContext context, string test)
    {
        var proxy = DispatchProxy.Create<IBrowserContext, WatchedContext>();
        ((WatchedContext)(object)proxy).Start(context, test);
        return proxy;
    }

    /// <summary>The context a test holds while coverage is on: the real one, plus a last
    /// profiler reading before it closes (a closed page can no longer be asked).</summary>
    public class WatchedContext : DispatchProxy
    {
        private IBrowserContext _inner = default!;
        private readonly ConcurrentBag<PageWatch> _pages = [];

        internal void Start(IBrowserContext inner, string test)
        {
            _inner = inner;
            inner.Page += (_, page) => _pages.Add(new PageWatch(inner, page, test));
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IAsyncDisposable.DisposeAsync))
                return new ValueTask(FlushThenAsync(() => _inner.DisposeAsync().AsTask()));
            if (targetMethod.Name == nameof(IBrowserContext.CloseAsync))
                return FlushThenAsync(() => (Task)targetMethod.Invoke(_inner, args)!);
            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException e) when (e.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        private async Task FlushThenAsync(Func<Task> close)
        {
            foreach (var page in _pages) await page.StopAsync();
            await close();
        }
    }

    private sealed class PageWatch
    {
        private readonly IPage _page;
        private readonly string _test;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;
        private ICDPSession? _session;

        public PageWatch(IBrowserContext context, IPage page, string test)
        {
            _page = page;
            _test = test;
            _loop = RunAsync(context);
        }

        // Readings are also taken as the test runs, a second apart (a reading holds the page up for a
        // moment): a page the test closes or navigates itself takes its counts with it.
        private async Task RunAsync(IBrowserContext context)
        {
            try
            {
                _session = await context.NewCDPSessionAsync(_page);
                await _session.SendAsync("Profiler.enable");
                await _session.SendAsync("Profiler.startPreciseCoverage",
                    new Dictionary<string, object> { ["callCount"] = true, ["detailed"] = false });
                while (!_page.IsClosed && !_stop.IsCancellationRequested)
                {
                    await Task.Delay(1000, _stop.Token);
                    await TakeAsync();
                }
            }
            catch (Exception e) when (e is PlaywrightException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
            {
                // The page went away between two readings, or the test is closing its context.
            }
        }

        private async Task TakeAsync()
        {
            if (_session is null || _page.IsClosed) return;
            await _gate.WaitAsync();
            try
            {
                if (await _session.SendAsync("Profiler.takePreciseCoverage") is { } taken) Record(taken, _test);
            }
            catch (Exception e) when (e is PlaywrightException or ObjectDisposedException or InvalidOperationException)
            {
                // Closed in the meantime.
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task StopAsync()
        {
            _stop.Cancel();
            await _loop;
            await TakeAsync();
        }
    }

    private static void Record(JsonElement taken, string test)
    {
        foreach (var script in taken.GetProperty("result").EnumerateArray())
        {
            var match = ModuleUrl().Match(script.GetProperty("url").GetString() ?? "");
            if (!match.Success) continue;
            var functions = s_modules.GetOrAdd(match.Groups[1].Value, _ => new());
            foreach (var function in script.GetProperty("functions").EnumerateArray())
            {
                var range = function.GetProperty("ranges")[0];
                var start = range.GetProperty("startOffset").GetInt32();
                // The module body itself: it runs on import, which says nothing about its logic.
                if (start == 0 && function.GetProperty("functionName").GetString() is "") continue;
                var key = $"{function.GetProperty("functionName").GetString()}@{start}-{range.GetProperty("endOffset").GetInt32()}";
                var tests = functions.GetOrAdd(key, _ => new());
                if (range.GetProperty("count").GetInt32() > 0) tests.TryAdd(test, 0);
            }
        }
    }

    // Tests that open a page and do nothing to it: what they call is what any page load calls.
    private static bool OnlyOpensPages(string test) =>
        test.StartsWith("RoutingTests.", StringComparison.Ordinal)
        || test.StartsWith("AxeTests.", StringComparison.Ordinal)
        || test.StartsWith("VisualRegressionTests.", StringComparison.Ordinal);

    /// <summary>Writes the result next to the project: <c>modules.json</c>, one entry per module.</summary>
    public static async Task WriteAsync()
    {
        var chunkOwners = ChunkOwners();
        // A module whose code the bundler moved into a shared chunk is measured under its own name.
        var byModule = s_modules
            .Where(m => !m.Value.IsEmpty)
            .GroupBy(m => chunkOwners.GetValueOrDefault(m.Key, m.Key), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        var modules = byModule.Select(g =>
        {
            var functions = g.SelectMany(m => m.Value.Select(f => (Key: m.Key + ":" + f.Key, Tests: f.Value.Keys.ToArray()))).ToList();
            var onLoad = functions.Where(f => f.Tests.Any(OnlyOpensPages)).Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
            var tests = functions.SelectMany(f => f.Tests).Where(t => !OnlyOpensPages(t)).Distinct().Order(StringComparer.Ordinal)
                .Select(t => new
                {
                    test = t,
                    called = functions.Count(f => f.Tests.Contains(t)),
                    beyondPageLoad = functions.Count(f => f.Tests.Contains(t) && !onLoad.Contains(f.Key)),
                })
                .ToList();
            return new
            {
                module = g.Key,
                functions = functions.Count,
                called = functions.Count(f => f.Tests.Length > 0),
                calledOnPageLoad = onLoad.Count,
                // The tests that reach the module: they called something a page load does not.
                reachedBy = tests.Where(t => t.beyondPageLoad > 0).ToList(),
                loadedBy = tests.Where(t => t.beyondPageLoad == 0).Select(t => t.test).ToList(),
            };
        });

        var dir = Path.Combine(E2E.RepoRoot, "tests", "Blaizio.Docs.E2E", "ScriptCoverage");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "modules.json"),
            JsonSerializer.Serialize(modules, new JsonSerializerOptions { WriteIndented = true }));

        // The same data before it was counted, for questions the summary does not answer (what a
        // module's reach was without a given test): module -> function -> the tests that called it.
        var detail = byModule.ToDictionary(g => g.Key, g => g
            .SelectMany(m => m.Value.Select(f => (Key: m.Key + ":" + f.Key, Tests: f.Value.Keys.Order(StringComparer.Ordinal).ToArray())))
            .ToDictionary(f => f.Key, f => f.Tests));
        await File.WriteAllTextAsync(Path.Combine(dir, "functions.json"), JsonSerializer.Serialize(detail));
    }

    /// <summary>Shared chunk name to the module it holds, read off the built files: an entry
    /// module that only re-exports from one chunk gave its whole code to that chunk.</summary>
    private static Dictionary<string, string> ChunkOwners()
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var dist = Path.Combine(E2E.RepoRoot, "src", "Blaizio.Base", "wwwroot", "dist");
        if (!Directory.Exists(dist)) return owners;
        foreach (var file in Directory.EnumerateFiles(dist, "*.js"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith("chunk-", StringComparison.Ordinal)) continue;
            var source = File.ReadAllText(file);
            if (source.Contains("function", StringComparison.Ordinal) || source.Contains("=>", StringComparison.Ordinal)) continue;
            // import{a,b}from"./chunk-X.js";import"./chunk-Y.js";export{...}: the chunk it takes names from.
            if (ReExport().Match(source) is { Success: true } match) owners.TryAdd(match.Groups[1].Value, name);
        }
        return owners;
    }

    // dist/<name>.js or dist/<name>.<fingerprint>.js
    [GeneratedRegex(@"/_content/blaizio\.base/dist/([A-Za-z0-9_-]+)(?:\.[a-z0-9]{6,})?\.js(?:\?|$)")]
    private static partial Regex ModuleUrl();

    [GeneratedRegex(@"import\{[^}]+\}from""\./(chunk-[A-Z0-9]+)\.js""")]
    private static partial Regex ReExport();
}
