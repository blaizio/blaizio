using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Resolution;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// Version pinning end to end: <c>Id@Version</c> NuGet declarations, <c>name@version</c> item
/// references, the recorded pin, and the pin riding into dependency resolution.
/// </summary>
public class VersionPinningTests
{
    // ---- NugetDependency ----

    [Theory]
    [InlineData("TailwindMerge.NET", "TailwindMerge.NET", null)]
    [InlineData("TailwindMerge.NET@1.4.0", "TailwindMerge.NET", "1.4.0")]
    [InlineData("My.Pkg@2.0.0-preview.1", "My.Pkg", "2.0.0-preview.1")]
    public void Nuget_reference_parses(string raw, string id, string? version)
    {
        var dep = NugetDependency.Parse(raw);
        Assert.Equal(id, dep.Id);
        Assert.Equal(version, dep.Version);
        Assert.Equal(raw, dep.ToString());
    }

    [Theory]
    [InlineData("@1.4.0")]
    [InlineData("Package@")]
    public void Nuget_reference_rejects_empty_halves(string raw)
        => Assert.Throws<InvalidOperationException>(() => NugetDependency.Parse(raw));

    // ---- reference version splitting ----

    [Theory]
    [InlineData("button@1.2.0", "button", "1.2.0")]
    [InlineData("@acme/button@1.2.0", "@acme/button", "1.2.0")]
    public void Reference_split_finds_a_pin(string reference, string name, string version)
    {
        Assert.True(ItemReference.TrySplitVersion(reference, out var n, out var v));
        Assert.Equal(name, n);
        Assert.Equal(version, v);
    }

    [Theory]
    [InlineData("button")]
    [InlineData("@acme/button")]
    [InlineData("https://acme.dev/r/button@2.json")]
    [InlineData("./items/button.json")]
    [InlineData("button@")]
    public void Reference_split_leaves_everything_else_alone(string reference)
        => Assert.False(ItemReference.TrySplitVersion(reference, out _, out _));

    // ---- resolver: nuget reconciliation ----

    private static RegistryItem Item(string name, string? version = null, string[]? deps = null, string[]? nuget = null) => new()
    {
        Name = name,
        Version = version,
        RegistryDependencies = deps ?? [],
        NugetDependencies = nuget ?? [],
    };

    [Fact]
    public async Task A_pinned_nuget_declaration_beats_a_floating_one()
    {
        var client = new FakeRegistryClient()
            .Add(Item("card", deps: ["button"], nuget: ["Blaizio.Base"]))
            .Add(Item("button", nuget: ["Blaizio.Base@1.0.0"]));

        var graph = await new DependencyResolver(client).ResolveAsync(["card"]);

        var dep = Assert.Single(graph.NugetPackages);
        Assert.Equal("Blaizio.Base", dep.Id);
        Assert.Equal("1.0.0", dep.Version);
    }

    [Fact]
    public async Task Conflicting_nuget_pins_fail_loudly()
    {
        var client = new FakeRegistryClient()
            .Add(Item("a", deps: ["b"], nuget: ["Pkg@1.0.0"]))
            .Add(Item("b", nuget: ["Pkg@2.0.0"]));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new DependencyResolver(client).ResolveAsync(["a"]));
        Assert.Contains("Pkg", ex.Message);
        Assert.Contains("1.0.0", ex.Message);
        Assert.Contains("2.0.0", ex.Message);
    }

    // ---- resolver: item pins ----

    [Fact]
    public async Task A_dependency_landing_on_a_pinned_name_fetches_the_pin()
    {
        var client = new FakeRegistryClient()
            .Add(Item("card", deps: ["button"]))
            .Add(Item("button", version: "1.2.0"));

        var resolver = new DependencyResolver(client, new Dictionary<string, string> { ["button"] = "1.2.0" });
        var graph = await resolver.ResolveAsync(["card"]);

        var button = Assert.Single(graph.Items, i => i.Name == "button");
        Assert.Equal("1.2.0", button.RequestedVersion);
    }

    [Fact]
    public async Task A_requested_plain_name_ignores_the_recorded_pin()
    {
        var client = new FakeRegistryClient().Add(Item("button", version: "2.0.0"));

        var resolver = new DependencyResolver(client, new Dictionary<string, string> { ["button"] = "1.2.0" });
        var graph = await resolver.ResolveAsync(["button"]);

        // add button = "give me current" - the pin map must not rewrite what the user typed.
        Assert.Null(Assert.Single(graph.Items).RequestedVersion);
    }

    // ---- registry client: serving and refusing pinned requests ----

    private static RegistryClient LocalClient(TempDir dir) => new(new HttpClient(), dir.Combine("r"));

    [Fact]
    public async Task The_client_serves_a_pin_the_registry_matches_and_stamps_it()
    {
        using var dir = new TempDir();
        dir.Write("r/button.json", """{ "name": "button", "version": "1.2.0" }""");

        var item = await LocalClient(dir).GetItemAsync("button@1.2.0");

        Assert.Equal("1.2.0", item.Version);
        Assert.Equal("1.2.0", item.RequestedVersion);
    }

    [Fact]
    public async Task The_client_refuses_a_pin_the_registry_cannot_serve()
    {
        using var dir = new TempDir();
        dir.Write("r/button.json", """{ "name": "button", "version": "2.0.0" }""");

        var ex = await Assert.ThrowsAsync<RegistryException>(
            () => LocalClient(dir).GetItemAsync("button@1.2.0"));
        Assert.Contains("2.0.0", ex.Message);
    }

    [Fact]
    public async Task The_client_refuses_a_pin_against_an_unversioned_registry()
    {
        using var dir = new TempDir();
        dir.Write("r/button.json", """{ "name": "button" }""");

        var ex = await Assert.ThrowsAsync<RegistryException>(
            () => LocalClient(dir).GetItemAsync("button@1.2.0"));
        Assert.Contains("does not version", ex.Message);
    }

    // ---- add service: the install record ----

    [Fact]
    public async Task Add_records_the_version_and_the_pin_and_a_plain_readd_unpins()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        var project = ProjectContext.Discover(dir.Path);
        var config = new BlaizioConfig { Namespace = "Acme.Ui" };
        var client = new FakeRegistryClient().Add(new RegistryItem
        {
            Name = "button",
            Version = "1.2.0",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "namespace Blaizio.Ui;" }],
        });
        var svc = new AddService(client, project, config, new DotnetCli(dir.Path));

        await svc.RunAsync(new AddRequest { Components = ["button@1.2.0"], NoNuget = true });
        Assert.Equal("1.2.0", config.Installed["button"].Version);
        Assert.Equal("1.2.0", config.Installed["button"].Pin);

        await svc.RunAsync(new AddRequest { Components = ["button"], Overwrite = true, Force = true, NoNuget = true });
        Assert.Equal("1.2.0", config.Installed["button"].Version);
        Assert.Null(config.Installed["button"].Pin);
    }

    [Fact]
    public async Task Adding_a_dependent_item_keeps_the_dependencys_pin()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        var project = ProjectContext.Discover(dir.Path);
        var config = new BlaizioConfig { Namespace = "Acme.Ui" };
        var client = new FakeRegistryClient()
            .Add(new RegistryItem
            {
                Name = "button",
                Version = "1.2.0",
                Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "b" }],
            })
            .Add(new RegistryItem
            {
                Name = "card",
                RegistryDependencies = ["button"],
                Files = [new RegistryFile { Path = "Ui/Card/Card.razor", Content = "c" }],
            });
        var svc = new AddService(client, project, config, new DotnetCli(dir.Path));

        await svc.RunAsync(new AddRequest { Components = ["button@1.2.0"], NoNuget = true });
        await svc.RunAsync(new AddRequest { Components = ["card"], NoNuget = true });

        Assert.Equal("1.2.0", config.Installed["button"].Pin); // card's dep pull kept the pin
        Assert.NotNull(config.Installed["card"]);
        Assert.Null(config.Installed["card"].Pin);
    }
}
