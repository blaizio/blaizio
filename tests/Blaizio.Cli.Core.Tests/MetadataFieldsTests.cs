using System.Text.Json;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Resolution;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// The item metadata fields: author/categories/docs/meta ride the wire untouched, docs notes
/// surface in the add result, and devDependencies install privately.
/// </summary>
public class MetadataFieldsTests
{
    [Fact]
    public void The_metadata_fields_round_trip_the_wire()
    {
        const string json = """
            {
              "name": "tag",
              "version": "1.0.0",
              "author": "Jane Doe <jane@acme.dev>",
              "categories": ["forms", "data"],
              "docs": "Wire the provider first.",
              "meta": { "gallery": { "order": 3 }, "flag": true },
              "devDependencies": ["Acme.Analyzers@1.0.0"]
            }
            """;

        var item = JsonSerializer.Deserialize(json, CoreJson.Default.RegistryItem)!;
        Assert.Equal("Jane Doe <jane@acme.dev>", item.Author);
        Assert.Equal(["forms", "data"], item.Categories);
        Assert.Equal("Wire the provider first.", item.Docs);
        Assert.Equal(["Acme.Analyzers@1.0.0"], item.DevDependencies);
        Assert.Equal(3, item.Meta!["gallery"].GetProperty("order").GetInt32());

        var back = JsonSerializer.Serialize(item, CoreJson.Default.RegistryItem);
        Assert.Contains("\"gallery\"", back);
        Assert.Contains("\"devDependencies\"", back);
    }

    [Fact]
    public void Unset_metadata_fields_stay_off_the_wire()
    {
        var back = JsonSerializer.Serialize(new RegistryItem { Name = "x" }, CoreJson.Default.RegistryItem);
        Assert.DoesNotContain("author", back);
        Assert.DoesNotContain("categories", back);
        Assert.DoesNotContain("docs", back);
        Assert.DoesNotContain("meta", back);
        Assert.DoesNotContain("devDependencies", back);
    }

    // ---- resolver: dev dependencies ----

    private static RegistryItem Item(string name, string[]? deps = null, string[]? nuget = null, string[]? dev = null) => new()
    {
        Name = name,
        RegistryDependencies = deps ?? [],
        NugetDependencies = nuget ?? [],
        DevDependencies = dev,
    };

    [Fact]
    public async Task Dev_dependencies_union_separately_from_runtime_ones()
    {
        var client = new FakeRegistryClient()
            .Add(Item("card", deps: ["button"], nuget: ["Blaizio.Base"], dev: ["Acme.Analyzers"]))
            .Add(Item("button", dev: ["Acme.Analyzers", "Acme.Gen@1.0.0"]));

        var graph = await new DependencyResolver(client).ResolveAsync(["card"]);

        Assert.Equal(["Blaizio.Base"], graph.NugetPackages.Select(d => d.ToString()));
        Assert.Equal(["Acme.Analyzers", "Acme.Gen@1.0.0"],
            graph.DevNugetPackages.Select(d => d.ToString()).OrderBy(x => x));
    }

    [Fact]
    public async Task A_runtime_dependency_wins_over_a_dev_declaration()
    {
        var client = new FakeRegistryClient()
            .Add(Item("a", deps: ["b"], nuget: ["Shared.Pkg"]))
            .Add(Item("b", dev: ["Shared.Pkg"]));

        var graph = await new DependencyResolver(client).ResolveAsync(["a"]);

        Assert.Equal(["Shared.Pkg"], graph.NugetPackages.Select(d => d.Id));
        Assert.Empty(graph.DevNugetPackages);
    }

    [Fact]
    public async Task Disagreeing_pins_across_runtime_and_dev_lists_fail_loudly()
    {
        var client = new FakeRegistryClient()
            .Add(Item("a", deps: ["b"], nuget: ["Shared.Pkg@1.0.0"]))
            .Add(Item("b", dev: ["Shared.Pkg@2.0.0"]));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new DependencyResolver(client).ResolveAsync(["a"]));
    }

    // ---- private assets marking ----

    [Fact]
    public void MarkPrivateAssets_touches_only_the_asked_references()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Acme.Analyzers" Version="1.0.0" />
                <PackageReference Include="Blaizio.Base" Version="0.1.0" />
                <PackageReference Include="Already.Private" Version="1.0.0" PrivateAssets="all" />
              </ItemGroup>
            </Project>
            """);

        new DotnetCli(dir.Path).MarkPrivateAssets(["Acme.Analyzers", "Already.Private", "Not.Referenced"]);

        var csproj = dir.Read("App.csproj");
        Assert.Contains("""Include="Acme.Analyzers" Version="1.0.0" PrivateAssets="all" """.TrimEnd(), csproj);
        Assert.Contains("""Include="Blaizio.Base" Version="0.1.0" />""", csproj);
        Assert.DoesNotContain("Not.Referenced", csproj);
    }

    // ---- add result surfaces ----

    [Fact]
    public async Task Add_surfaces_docs_notes_and_dev_packages()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        var project = ProjectContext.Discover(dir.Path);
        var config = new BlaizioConfig { Namespace = "Acme.Ui" };
        var client = new FakeRegistryClient().Add(new RegistryItem
        {
            Name = "chart",
            Docs = "Register IChartService in Program.cs.",
            DevDependencies = ["Acme.Analyzers@1.0.0"],
            Files = [new RegistryFile { Path = "Ui/Chart/Chart.razor", Content = "c" }],
        });
        var svc = new AddService(client, project, config, new DotnetCli(dir.Path));

        var result = await svc.RunAsync(new AddRequest { Components = ["chart"], DryRun = true });

        var note = Assert.Single(result.DocsNotes);
        Assert.Equal("chart", note.Item);
        Assert.Equal("Register IChartService in Program.cs.", note.Note);
        Assert.Equal(["Acme.Analyzers@1.0.0"], result.DevNugetPackages);
    }
}
