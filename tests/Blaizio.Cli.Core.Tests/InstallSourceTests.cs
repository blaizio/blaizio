using System.Net.Http;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Resolution;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// An install that did not come from a registry name - a file, a URL, a repository address -
/// records where it came from, so <c>update</c> can go back there. Before this, every plain key
/// in the ledger was assumed to be a name on the default registry, and a direct install could
/// never be updated: the re-pull looked in a registry that had never heard of it.
/// </summary>
public class InstallSourceTests
{
    private static (AddService svc, TempDir dir, BlaizioConfig config) Project(IRegistryClient client)
    {
        var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        var project = ProjectContext.Discover(dir.Path);
        var config = new BlaizioConfig { Namespace = "Acme.Ui", Output = "Components/Ui" };
        return (new AddService(client, project, config, new DotnetCli(dir.Path)), dir, config);
    }

    [Fact]
    public async Task The_registry_client_stamps_a_direct_file_reference_as_the_source()
    {
        using var dir = new TempDir();
        dir.Write("editor.json", """{"name":"editor","type":"registry:ui","files":[]}""");
        var path = Path.Combine(dir.Path, "editor.json");
        var client = new RegistryClient(new HttpClient(), dir.Path);

        var item = await client.GetItemAsync(path);

        Assert.Equal("editor", item.Name);
        Assert.Equal(path, item.SourceReference);
        Assert.Null(item.SourceNamespace);
    }

    [Fact]
    public async Task A_plain_name_carries_no_source()
    {
        using var dir = new TempDir();
        dir.Write("button.json", """{"name":"button","type":"registry:ui","files":[]}""");
        var client = new RegistryClient(new HttpClient(), dir.Path);

        var item = await client.GetItemAsync("button");

        Assert.Null(item.SourceReference);
    }

    [Fact]
    public async Task Add_records_the_source_of_a_direct_install_and_update_style_re_pull_keeps_it()
    {
        var client = new FakeRegistryClient()
            .AddDirect("./registry/editor.json", new RegistryItem { Name = "editor", Type = ItemType.Ui });
        var (svc, dir, config) = Project(client);
        using (dir)
        {
            await svc.RunAsync(new AddRequest { Components = ["./registry/editor.json"], NoNuget = true });

            // Keyed by the item's own name - a direct install is not namespaced - with the
            // reference it was fetched by beside it.
            Assert.Equal("./registry/editor.json", config.Installed["editor"].Source);

            // What update does with it: re-pull by the recorded source, not the key.
            await svc.RunAsync(new AddRequest { Components = [config.Installed["editor"].Source!], Overwrite = true, NoNuget = true });
            Assert.Equal("./registry/editor.json", config.Installed["editor"].Source);
        }
    }

    [Fact]
    public async Task Re_adding_by_plain_name_from_the_default_registry_clears_the_source()
    {
        var client = new FakeRegistryClient()
            .AddDirect("./registry/editor.json", new RegistryItem { Name = "editor", Type = ItemType.Ui })
            .Add(new RegistryItem { Name = "editor", Type = ItemType.Ui });
        var (svc, dir, config) = Project(client);
        using (dir)
        {
            await svc.RunAsync(new AddRequest { Components = ["./registry/editor.json"], NoNuget = true });
            await svc.RunAsync(new AddRequest { Components = ["editor"], Overwrite = true, NoNuget = true });

            Assert.Null(config.Installed["editor"].Source);
        }
    }

    [Fact]
    public async Task Prune_spares_a_record_that_came_from_a_source()
    {
        var client = new FakeRegistryClient()
            .AddDirect("./registry/editor.json", new RegistryItem { Name = "editor", Type = ItemType.Ui })
            .Add(new RegistryItem { Name = "button", Type = ItemType.Ui });
        var (svc, dir, config) = Project(client);
        using (dir)
        {
            await svc.RunAsync(new AddRequest { Components = ["./registry/editor.json"], NoNuget = true });
            await svc.RunAsync(new AddRequest { Components = ["button"], Prune = true, NoNuget = true });

            Assert.True(config.Installed.ContainsKey("editor"));
        }
    }

    [Fact]
    public async Task Resolver_leaves_a_missing_root_out_when_asked_and_says_which()
    {
        var client = new FakeRegistryClient().Add(new RegistryItem { Name = "button", Type = ItemType.Ui });

        var graph = await new DependencyResolver(client).ResolveAsync(["button", "ghost"], skipMissing: true);

        Assert.Equal(["button"], graph.Items.Select(i => i.Name));
        var skipped = Assert.Single(graph.Skipped);
        Assert.Equal("ghost", skipped.Reference);
        Assert.Contains("ghost", skipped.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolver_still_fails_on_a_missing_root_by_default()
    {
        var client = new FakeRegistryClient().Add(new RegistryItem { Name = "button", Type = ItemType.Ui });

        var ex = await Assert.ThrowsAsync<RegistryException>(
            () => new DependencyResolver(client).ResolveAsync(["button", "ghost"]));

        Assert.Equal(RegistryFailure.NotFound, ex.Reason);
    }

    [Fact]
    public async Task Add_with_SkipMissing_installs_the_rest_and_reports_the_miss()
    {
        var client = new FakeRegistryClient().Add(new RegistryItem { Name = "button", Type = ItemType.Ui });
        var (svc, dir, config) = Project(client);
        using (dir)
        {
            var result = await svc.RunAsync(new AddRequest { Components = ["button", "ghost"], SkipMissing = true, NoNuget = true });

            Assert.Equal(["button"], result.Items);
            Assert.Equal("ghost", Assert.Single(result.Skipped).Reference);
            Assert.True(config.Installed.ContainsKey("button"));
            Assert.False(config.Installed.ContainsKey("ghost"));
        }
    }
}
