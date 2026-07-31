using System.Diagnostics;
using System.Text;
using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>The suite's opt-in gate and knobs (see README.md).</summary>
public static class E2E
{
    /// <summary>Whether the suite actually runs. Off (the default) turns every test into a no-op
    /// so a plain solution-wide <c>dotnet test</c> never boots a server or a browser.</summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("BLAIZIO_E2E") == "1";

    /// <summary>Whether the visual-regression run re-records its baselines instead of comparing.</summary>
    public static bool UpdateBaselines => Environment.GetEnvironmentVariable("BLAIZIO_E2E_UPDATE") == "1";
}

/// <summary>
/// One docs app + one Chromium for the whole collection: boots the docs project on a fixed port
/// (its own build chain packs Base and refreshes Components/Ui first, so the first boot is the
/// slow one), installs/launches headless Chromium, and hands tests preconfigured contexts whose
/// init script seeds the blaizio-style/-dir/-theme localStorage keys the pre-paint boot script
/// reads - a page therefore loads ALREADY in the requested skin/direction/scheme, no flicker,
/// no post-load toggling.
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

        var repoRoot = FindRepoRoot();
        var docsProj = Path.Combine(repoRoot, "docs", "Blaizio.Docs", "Blaizio.Docs.csproj");

        _server = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{docsProj}\" --no-launch-profile --urls {BaseUrl}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            Environment = { ["ASPNETCORE_ENVIRONMENT"] = "Development" },
        })!;
        _server.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (_serverLog) _serverLog.AppendLine(e.Data); };
        _server.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (_serverLog) _serverLog.AppendLine(e.Data); };
        _server.BeginOutputReadLine();
        _server.BeginErrorReadLine();

        await WaitForServerAsync();

        // Idempotent: downloads Chromium on the first machine run, no-ops afterwards.
        Microsoft.Playwright.Program.Main(["install", "chromium"]);

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
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

    /// <summary>Navigate and wait until Blazor has rendered <paramref name="readySelector"/>
    /// (the page h1 by default; /create has no h1, so its tests wait on <c>main</c>).</summary>
    public static async Task<IPage> OpenAsync(IBrowserContext context, string path, string readySelector = "h1")
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/{path.TrimStart('/')}");
        await page.Locator(readySelector).First.WaitForAsync(new() { Timeout = 30_000 });
        return page;
    }

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

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Blaizio.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Blaizio.slnx not found above the test assembly.");
    }
}

[CollectionDefinition("docs-e2e")]
public sealed class DocsE2ECollection : ICollectionFixture<DocsServerFixture>;
