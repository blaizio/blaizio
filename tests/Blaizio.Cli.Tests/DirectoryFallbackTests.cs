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
    public DirectoryFallbackTests() => Environment.SetEnvironmentVariable("BLAIZIO_DIRECTORY", null);

    public void Dispose() => Environment.SetEnvironmentVariable("BLAIZIO_DIRECTORY", null);

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
}
