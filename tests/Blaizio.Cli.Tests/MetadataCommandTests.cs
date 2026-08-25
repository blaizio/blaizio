using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// The item metadata fields through the real command surface: category filtering in search, and
/// the docs note surfacing when an item installs (fixture card carries author/categories/docs).
/// </summary>
[Collection("console")]
public class MetadataCommandTests
{
    private static async Task<(int ExitCode, string Stdout)> RunAsync(params string[] args)
    {
        var tester = new CommandAppTester();
        tester.Configure(CliApp.Configure);
        using var stdout = new StdoutCapture();
        var result = await tester.RunAsync(args);
        return (result.ExitCode, stdout.Text);
    }

    [Fact]
    public async Task Search_filters_by_category()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, stdout) = await RunAsync("search", "--category", "data", "--json", "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var items = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()).ToArray();
        Assert.Equal(["card"], items);
    }

    [Fact]
    public async Task Add_surfaces_the_items_docs_note()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("add", "card", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var note = doc.RootElement.GetProperty("docsNotes").EnumerateArray().Single();
        Assert.Equal("card", note.GetProperty("item").GetString());
        Assert.Equal("Cards pair well with buttons.", note.GetProperty("note").GetString());
    }

    [Fact]
    public async Task View_prints_author_categories_and_note()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        using var ansi = new AnsiCapture();
        var tester = new CommandAppTester();
        tester.Configure(CliApp.Configure);
        var result = await tester.RunAsync(["view", "card", "--registry", registry, "-c", dir.Path]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Fixture Author", ansi.Text);
        Assert.Contains("data", ansi.Text);
        Assert.Contains("Cards pair well with buttons.", ansi.Text);
    }
}
