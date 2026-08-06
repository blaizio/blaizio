using Spectre.Console.Testing;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// <c>name@version</c> through the real command surface against the local registry fixture
/// (whose <c>button</c> is versioned 1.0.0 and everything else unversioned).
/// </summary>
[Collection("console")]
public class VersionPinningCommandTests
{
    private static async Task<(int ExitCode, string Stdout)> RunAsync(params string[] args)
    {
        var tester = new CommandAppTester();
        tester.Configure(CliApp.Configure);
        using var stdout = new StdoutCapture();
        var result = await tester.RunAsync(args);
        return (result.ExitCode, stdout.Text);
    }

    private static async Task InitAsync(TempDir dir, string registry)
        => await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

    [Fact]
    public async Task Add_with_a_pin_records_it_and_a_plain_readd_unpins()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await InitAsync(dir, registry);

        var (exit, _) = await RunAsync("add", "button@1.0.0", "--json", "-c", dir.Path);
        Assert.Equal(0, exit);
        var config = File.ReadAllText(dir.Combine("blaizio.json"));
        Assert.Contains("\"pin\": \"1.0.0\"", config);
        Assert.Contains("\"version\": \"1.0.0\"", config);

        (exit, _) = await RunAsync("add", "button", "--overwrite", "--force-overwrite", "--json", "-c", dir.Path);
        Assert.Equal(0, exit);
        config = File.ReadAllText(dir.Combine("blaizio.json"));
        Assert.DoesNotContain("\"pin\"", config);
    }

    [Fact]
    public async Task Add_with_a_pin_the_registry_cannot_serve_is_a_registry_error()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await InitAsync(dir, registry);

        var (exit, _) = await RunAsync("add", "button@9.9.9", "--json", "-c", dir.Path);

        Assert.Equal(2, exit);
        Assert.False(File.Exists(dir.Combine("Components", "Ui", "Button", "BzButton.razor")));
    }

    [Fact]
    public async Task Add_with_a_pin_against_an_unversioned_item_is_a_registry_error()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await InitAsync(dir, registry);

        var (exit, _) = await RunAsync("add", "card@1.0.0", "--json", "-c", dir.Path);

        Assert.Equal(2, exit);
    }

    [Fact]
    public async Task Update_repulls_a_pinned_item_at_its_pin()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await InitAsync(dir, registry);
        await RunAsync("add", "button@1.0.0", "--json", "-c", dir.Path);

        var (exit, _) = await RunAsync("update", "-y", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        var config = File.ReadAllText(dir.Combine("blaizio.json"));
        Assert.Contains("\"pin\": \"1.0.0\"", config); // the pin survived the re-pull
    }
}
