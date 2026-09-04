using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// The community-directory fallback: an unrecorded <c>@namespace</c> resolves through the
/// reviewed directory (here a local file via <c>BLAIZIO_DIRECTORY</c>), gets recorded into
/// <c>blaizio.json</c>, and the install proceeds - while an unlisted namespace keeps the
/// ordinary "unknown registry" failure.
/// </summary>
[Collection("console")]
public class DirectoryFallbackTests : IDisposable
{
    public DirectoryFallbackTests() => OfflineDirectory.Reset();

    public void Dispose() => OfflineDirectory.Reset();

    private static async Task<(int ExitCode, string Stdout)> RunAsync(params string[] args)
    {
        var tester = new CommandAppTester();
        tester.Configure(CliApp.Configure);
        using var stdout = new StdoutCapture();
        var result = await tester.RunAsync(args);
        return (result.ExitCode, stdout.Text);
    }

    private static string WriteDirectory(TempDir dir, string registryPath)
    {
        var listing = $$"""
            [
              {
                "name": "@acme",
                "homepage": "https://acme.test",
                "url": {{System.Text.Json.JsonSerializer.Serialize(registryPath)}},
                "description": "Acme's components."
              }
            ]
            """;
        dir.Write("directory.json", listing);
        return dir.Combine("directory.json");
    }

    [Fact]
    public async Task An_unrecorded_namespace_resolves_through_the_directory_and_is_recorded()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        var secondary = LocalRegistry.CreateSecondary(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        Environment.SetEnvironmentVariable("BLAIZIO_DIRECTORY", WriteDirectory(dir, secondary));

        var (exit, _) = await RunAsync("add", "@acme/tag", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(dir.Combine("Components", "Ui", "Acme", "Tag", "BzTag.razor")));
        var config = File.ReadAllText(dir.Combine("blaizio.json"));
        Assert.Contains("\"@acme\"", config);
    }

    [Fact]
    public async Task An_unlisted_namespace_keeps_the_unknown_registry_error()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);
        Environment.SetEnvironmentVariable("BLAIZIO_DIRECTORY", WriteDirectory(dir, dir.Combine("r2")));

        var (exit, _) = await RunAsync("add", "@nobody/tag", "--json", "-c", dir.Path);

        Assert.NotEqual(0, exit);
        Assert.DoesNotContain("\"@nobody\"", File.ReadAllText(dir.Combine("blaizio.json")));
    }

    /// <summary>
    /// The CI failure this guards: a directory host that accepts the TCP connection and never
    /// sends a byte. HttpClient reports its timeout as a cancellation; the lookup must treat
    /// that as "not listed" (exit 2 from the unknown-registry error), never as Ctrl+C (130).
    /// </summary>
    [Fact]
    public async Task A_directory_host_that_never_answers_is_treated_as_unlisted()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        using var stop = new CancellationTokenSource();
        var accepting = Task.Run(async () =>
        {
            var held = new List<System.Net.Sockets.TcpClient>();
            try
            {
                while (!stop.IsCancellationRequested)
                    held.Add(await listener.AcceptTcpClientAsync(stop.Token)); // accept, then stay silent
            }
            catch (OperationCanceledException) { }
            foreach (var c in held) c.Dispose();
        });
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        Environment.SetEnvironmentVariable("BLAIZIO_DIRECTORY", $"http://127.0.0.1:{port}/registries.json");

        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var (exit, _) = await RunAsync("add", "@nobody/tag", "--json", "-c", dir.Path);

            Assert.Equal(2, exit);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), $"lookup took {clock.Elapsed}");
        }
        finally
        {
            stop.Cancel();
            listener.Stop();
            await accepting;
        }
    }
}
