using System.Text.Json;
using Blaizio.Cli.Core.Projects;
using Spectre.Console.Testing;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// Running project commands from a solution root: the projects underneath are discovered, one
/// runs silently, several fan out (or are refused under --json with the -c hint).
/// </summary>
[Collection("console")]
public class MultiProjectTests
{
    private static CommandAppTester App()
    {
        var tester = new CommandAppTester();
        tester.Configure(CliApp.Configure);
        return tester;
    }

    /// <summary>A root holding two wired projects (and one unrelated folder) beneath src/.</summary>
    private static async Task<(TempDir Root, string Registry)> TwoProjectsAsync()
    {
        var root = new TempDir();
        var registry = LocalRegistry.Create(root);
        foreach (var name in new[] { "App", "App.Docs" })
        {
            root.Write($"src/{name}/{name}.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            var result = await App().RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", root.Combine("src", name));
            Assert.Equal(0, result.ExitCode);
        }
        root.Write("src/Other/Other.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        root.Write("App.slnx", "<Solution />");
        return (root, registry);
    }

    [Fact]
    public void Discovery_finds_wired_projects_only_and_prunes_build_output()
    {
        using var root = new TempDir();
        root.Write("src/A/blaizio.json", "{}");
        root.Write("src/B/blaizio.json", "{}");
        root.Write("src/B/Components/Ui/blaizio.json", "{}"); // inside a project: never a second project
        root.Write("src/C/C.csproj", "<Project />");            // a csproj alone is not a Blaizio project
        root.Write("src/A/bin/Debug/blaizio.json", "{}");
        root.Write("node_modules/x/blaizio.json", "{}");
        root.Write("artifacts/y/blaizio.json", "{}");

        var found = ProjectDiscovery.FindProjects(root.Path);

        Assert.Equal(["src/A", "src/B"], found.Select(p => ProjectDiscovery.Label(root.Path, p)));
    }

    [Fact]
    public void A_folder_with_a_csproj_is_its_own_root_even_unwired()
    {
        using var root = new TempDir();
        root.Write("App.csproj", "<Project />");
        Assert.True(ProjectDiscovery.IsProjectRoot(root.Path));
        Assert.False(ProjectDiscovery.IsProject(root.Path));
    }

    [Fact]
    public async Task Info_from_the_root_fans_out_over_every_project_under_yes()
    {
        var (root, _) = await TwoProjectsAsync();
        using (root)
        {
            using var ansi = new AnsiCapture();
            var result = await App().RunAsync("info", "-c", root.Path);

            Assert.Equal(0, result.ExitCode);
            var text = ansi.Text;
            Assert.Contains("src/App", text);
            Assert.Contains("src/App.Docs", text);
            // Each project rendered its own info block, then the summary ticked both.
            Assert.Equal(2, CountOf(text, "component namespace"));
        }
    }

    [Fact]
    public async Task Json_refuses_several_projects_and_names_them()
    {
        var (root, _) = await TwoProjectsAsync();
        using (root)
        {
            using var stdout = new StdoutCapture();
            using var ansi = new AnsiCapture();
            var result = await App().RunAsync("add", "--json", "-c", root.Path);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal("", stdout.Text.Trim());
        }
    }

    [Fact]
    public async Task Add_from_the_root_installs_into_every_selected_project()
    {
        var (root, registry) = await TwoProjectsAsync();
        using (root)
        {
            using var ansi = new AnsiCapture();
            var result = await App().RunAsync("add", "card", "-y", "-s", "--registry", registry, "-c", root.Path);

            Assert.Equal(0, result.ExitCode);
            foreach (var name in new[] { "App", "App.Docs" })
            {
                var config = File.ReadAllText(root.Combine("src", name, "blaizio.json"));
                Assert.Contains("\"card\"", config);
                Assert.True(File.Exists(root.Combine("src", name, "Components", "Ui", "Card", "BzCard.razor")), $"{name} did not get BzCard.razor");
            }
            Assert.False(File.Exists(root.Combine("src", "Other", "blaizio.json")));
        }
    }

    [Fact]
    public async Task One_project_under_the_root_runs_without_a_prompt()
    {
        using var root = new TempDir();
        var registry = LocalRegistry.Create(root);
        root.Write("src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await App().RunAsync("add", "-y", "--tailwind", "none", "-s", "--registry", registry, "-c", root.Combine("src", "App"));

        using var stdout = new StdoutCapture();
        var result = await App().RunAsync("info", "--json", "-c", root.Path);

        Assert.Equal(0, result.ExitCode);
        var json = JsonDocument.Parse(stdout.Text);
        Assert.EndsWith("App", json.RootElement.GetProperty("projectDir").GetString()!.TrimEnd('/', '\\'));
    }

    private static int CountOf(string text, string needle)
    {
        var count = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
