using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Resolution;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class DependencyResolverTests
{
    private static RegistryItem Item(string name, string[]? deps = null, string[]? nuget = null) => new()
    {
        Name = name,
        RegistryDependencies = deps ?? [],
        NugetDependencies = nuget ?? [],
    };

    [Fact]
    public async Task Emits_dependencies_before_dependents()
    {
        var client = new FakeRegistryClient()
            .Add(Item("button", deps: ["utils"]))
            .Add(Item("utils"));

        var graph = await new DependencyResolver(client).ResolveAsync(["button"]);

        Assert.Equal(["utils", "button"], graph.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Deduplicates_a_shared_dependency_in_a_diamond()
    {
        var client = new FakeRegistryClient()
            .Add(Item("dialog", deps: ["portal", "button"]))
            .Add(Item("button", deps: ["utils"]))
            .Add(Item("portal", deps: ["utils"]))
            .Add(Item("utils"));

        var graph = await new DependencyResolver(client).ResolveAsync(["dialog"]);

        Assert.Single(graph.Items, i => i.Name == "utils");
        // utils must precede both of its dependents.
        var order = graph.Items.Select(i => i.Name).ToList();
        Assert.True(order.IndexOf("utils") < order.IndexOf("button"));
        Assert.True(order.IndexOf("utils") < order.IndexOf("portal"));
    }

    [Fact]
    public async Task Tolerates_a_dependency_cycle()
    {
        var client = new FakeRegistryClient()
            .Add(Item("a", deps: ["b"]))
            .Add(Item("b", deps: ["a"]));

        var graph = await new DependencyResolver(client).ResolveAsync(["a"]);

        Assert.Equal(2, graph.Items.Count);
        Assert.Contains(graph.Items, i => i.Name == "a");
        Assert.Contains(graph.Items, i => i.Name == "b");
    }

    [Fact]
    public async Task Unions_and_deduplicates_nuget_packages()
    {
        var client = new FakeRegistryClient()
            .Add(Item("button", deps: ["utils"], nuget: ["Blaizio.Base", "TailwindMerge.NET"]))
            .Add(Item("utils", nuget: ["Blaizio.Base"]));

        var graph = await new DependencyResolver(client).ResolveAsync(["button"]);

        Assert.Equal(["Blaizio.Base", "TailwindMerge.NET"], graph.NugetPackages.Select(d => d.ToString()).OrderBy(x => x));
    }

    [Fact]
    public async Task Fetches_each_item_once()
    {
        var client = new FakeRegistryClient()
            .Add(Item("a", deps: ["c"]))
            .Add(Item("b", deps: ["c"]))
            .Add(Item("c"));

        await new DependencyResolver(client).ResolveAsync(["a", "b"]);

        Assert.Equal(3, client.FetchCount);
    }
}
