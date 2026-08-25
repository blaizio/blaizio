using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// <c>blaizio new</c> with a registry reference: the fixture's <c>starter</c> template scaffolds
/// through the same engine as the built-ins, then the init pipeline wires the project and adds
/// the template's component set.
/// </summary>
[Collection("console")]
public class RegistryTemplateCommandTests
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
    public async Task New_scaffolds_a_registry_template_and_adds_its_component_set()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, _) = await RunAsync(
            "new", "starter", "-n", "Contoso", "-y", "-s", "--tailwind", "none",
            "--registry", registry, "-c", dir.Path);

        Assert.Equal(0, exit);
        // Scaffolded files, tokens substituted.
        Assert.Equal("@page \"/\"\n<h1>Contoso</h1>\n", File.ReadAllText(dir.Combine("Pages", "Home.razor")).ReplaceLineEndings("\n"));
        Assert.Contains("Contoso", File.ReadAllText(dir.Combine("wwwroot", "index.html")));
        // The template's component set came along, recorded like any add.
        Assert.True(File.Exists(dir.Combine("Components", "Ui", "Button", "BzButton.razor")));
        Assert.Contains("\"button\"", File.ReadAllText(dir.Combine("blaizio.json")));
    }

    [Fact]
    public async Task New_refuses_an_item_that_is_not_a_template()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);

        var (exit, _) = await RunAsync(
            "new", "card", "-y", "-s", "--tailwind", "none", "--registry", registry, "-c", dir.Path);

        Assert.Equal(1, exit);
        Assert.False(File.Exists(dir.Combine("blaizio.json")));
    }

    [Fact]
    public async Task Add_all_leaves_templates_alone()
    {
        using var dir = new TempDir();
        var registry = LocalRegistry.Create(dir);
        await RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", dir.Path);

        var (exit, stdout) = await RunAsync("add", "--all", "--json", "-c", dir.Path);

        Assert.Equal(0, exit);
        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        var items = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => e.GetString()).ToArray();
        Assert.DoesNotContain("starter", items);
        Assert.Contains("card", items);
    }
}
