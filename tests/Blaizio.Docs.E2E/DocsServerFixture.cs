using System.Diagnostics;
using System.Text;
using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>The suite's opt-in gate and knobs (see README.md).</summary>
public static class E2E
{
    /// <summary>Whether the suite actually runs. Off (the default) skips every test so a plain
    /// solution-wide <c>dotnet test</c> never boots a server or a browser.</summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("BLAIZIO_E2E") == "1";

    /// <summary>Whether the visual-regression run re-records its baselines instead of comparing.</summary>
    public static bool UpdateBaselines => Environment.GetEnvironmentVariable("BLAIZIO_E2E_UPDATE") == "1";

    /// <summary>The repo root (walk up to Blaizio.slnx), resolved once per process.</summary>
    public static string RepoRoot => s_repoRoot.Value;

    private static readonly Lazy<string> s_repoRoot = new(() =>
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Blaizio.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Blaizio.slnx not found above the test assembly.");
    });
}

/// <summary>An xunit fact that is SKIPPED (not silently green) when the suite's opt-in gate is off.</summary>
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (!E2E.Enabled) Skip = "BLAIZIO_E2E is not set (see tests/Blaizio.Docs.E2E/README.md)";
    }
}

/// <summary>The theory twin of <see cref="E2EFactAttribute"/>.</summary>
public sealed class E2ETheoryAttribute : TheoryAttribute
{
    public E2ETheoryAttribute()
    {
        if (!E2E.Enabled) Skip = "BLAIZIO_E2E is not set (see tests/Blaizio.Docs.E2E/README.md)";
    }
}

/// <summary>
/// One docs app + one Chromium for the whole collection: boots the docs project on a fixed port
/// (its own build chain packs Base and refreshes Components/Ui first, so the first boot is the
/// slow one), installs/launches headless Chromium concurrently with the server wait, and hands
/// tests preconfigured contexts whose init script seeds the blaizio-style/-dir/-theme
/// localStorage keys the pre-paint boot script reads - a page therefore loads ALREADY in the
/// requested skin/direction/scheme, no flicker, no post-load toggling.
/// </summary>
public sealed class DocsServerFixture : IAsyncLifetime
{
    public const string BaseUrl = "http://127.0.0.1:5237";

    private Process? _server;
    private readonly StringBuilder _serverLog = new();
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        if (!E2E.Enabled) return;

        var docsProj = Path.Combine(E2E.RepoRoot, "docs", "Blaizio.Docs", "Blaizio.Docs.csproj");

        _server = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{docsProj}\" --no-launch-profile --urls {BaseUrl}",
            WorkingDirectory = E2E.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            Environment = { ["ASPNETCORE_ENVIRONMENT"] = "Development" },
        })!;
        DataReceivedEventHandler log = (_, e) => { if (e.Data is not null) lock (_serverLog) _serverLog.AppendLine(e.Data); };
        _server.OutputDataReceived += log;
        _server.ErrorDataReceived += log;
        _server.BeginOutputReadLine();
        _server.BeginErrorReadLine();

        // The browser side is independent of the server boot - overlap them so the (idempotent)
        // Chromium install and launch ride inside the server's build/start window.
        var browserTask = Task.Run(async () =>
        {
            Microsoft.Playwright.Program.Main(["install", "chromium"]);
            _playwright = await Playwright.CreateAsync();
            return await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        });

        await WaitForServerAsync();
        Browser = await browserTask;
    }

    private async Task WaitForServerAsync()
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(5); // first boot packs + rebuilds
        while (DateTime.UtcNow < deadline)
        {
            if (_server is { HasExited: true })
                throw new InvalidOperationException($"Docs server exited early.\n{_serverLog}");
            try
            {
                var response = await http.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Docs server did not start within 5 minutes.\n{_serverLog}");
    }

    /// <summary>
    /// A fresh context whose first paint already has the requested skin, direction and scheme:
    /// the init script writes the same localStorage keys ts/theme.ts persists, which the docs
    /// boot script applies before Blazor starts.
    /// </summary>
    public async Task<IBrowserContext> NewContextAsync(
        string style = "ember", string dir = "ltr", string theme = "light", ViewportSize? viewport = null)
    {
        var context = await Browser.NewContextAsync(new()
        {
            ViewportSize = viewport ?? new ViewportSize { Width = 1280, Height = 800 },
            ColorScheme = theme == "dark" ? ColorScheme.Dark : ColorScheme.Light,
            ReducedMotion = ReducedMotion.Reduce,
        });
        await context.AddInitScriptAsync(
            $"localStorage.setItem('blaizio-style', '{style}');" +
            $"localStorage.setItem('blaizio-dir', '{dir}');" +
            $"localStorage.setItem('blaizio-theme', '{theme}');");
        return context;
    }

    /// <summary>The selector whose presence means Blazor has rendered <paramref name="route"/>:
    /// every docs page leads with an h1 except /themes, a full-bleed composer.</summary>
    public static string ReadySelectorFor(string route) =>
        route.TrimStart('/') == "themes" ? "main" : "h1";

    /// <summary>Navigate an EXISTING page (so callers can attach listeners first) and wait until
    /// Blazor has rendered the route's ready selector.</summary>
    public static async Task OpenAsync(IPage page, string path)
    {
        await page.GotoAsync($"{BaseUrl}/{path.TrimStart('/')}");
        await page.Locator(ReadySelectorFor(path)).First.WaitForAsync(new() { Timeout = 30_000 });
    }

    /// <summary>New page + <see cref="OpenAsync(IPage, string)"/>.</summary>
    public static async Task<IPage> OpenAsync(IBrowserContext context, string path)
    {
        var page = await context.NewPageAsync();
        await OpenAsync(page, path);
        return page;
    }

    /// <summary>The currently open dialog-family surface (dialogs, sheets); pages keep other
    /// dialog surfaces mounted while closed, so plain First would match a hidden one.</summary>
    public static ILocator VisibleDialog(IPage page) => page.Locator("[role=dialog]:visible").First;

    /// <summary>
    /// Wait for the focus scope to land inside the open dialog surface. Every Blaizio
    /// dialog-family surface moves focus in asynchronously after opening - an Escape pressed
    /// before that lands on the page body and never reaches the surface's keydown handler.
    /// </summary>
    public static Task WaitForDialogFocusAsync(IPage page) =>
        page.WaitForFunctionAsync("() => document.activeElement?.closest('[role=dialog]') != null");

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        if (_server is { HasExited: false })
        {
            _server.Kill(entireProcessTree: true);
            _server.WaitForExit(10_000);
        }
        _server?.Dispose();
    }
}

[CollectionDefinition("docs-e2e")]
public sealed class DocsE2ECollection : ICollectionFixture<DocsServerFixture>;
