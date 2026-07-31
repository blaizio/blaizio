using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Writing;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class AddServiceTests
{
    private static (AddService svc, TempDir dir) Build(FakeRegistryClient client)
    {
        var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        var project = ProjectContext.Discover(dir.Path);
        var config = new BlaizioConfig { Namespace = "Acme.Ui", Output = "Components/Ui" };
        var svc = new AddService(client, project, config, new DotnetCli(dir.Path));
        return (svc, dir);
    }

    private static FakeRegistryClient TwoItems() => new FakeRegistryClient()
        .Add(new RegistryItem
        {
            Name = "button",
            RegistryDependencies = ["utils"],
            NugetDependencies = ["Blaizio.Base"],
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "namespace Blaizio.Ui.Button;" }],
        })
        .Add(new RegistryItem
        {
            Name = "utils",
            Files = [new RegistryFile { Path = "Lib/Cn.cs", Content = "namespace Blaizio.Ui;" }],
        });

    [Fact]
    public async Task DryRun_plans_the_whole_graph_and_writes_nothing()
    {
        var (svc, dir) = Build(TwoItems());
        using (dir)
        {
            var result = await svc.RunAsync(new AddRequest { Components = ["button"], DryRun = true });

            Assert.Equal(["utils", "button"], result.Items);
            Assert.True(result.DryRun);
            Assert.All(result.Files, f => Assert.Equal(WriteAction.Planned, f.Action));
            Assert.False(dir.Exists("Components/Ui/Button/Button.razor"));
            Assert.False(dir.Exists("_Imports.razor"));
        }
    }

    [Fact]
    public async Task NoDeps_writes_only_the_requested_item_and_rewrites_the_namespace()
    {
        var (svc, dir) = Build(TwoItems());
        using (dir)
        {
            var result = await svc.RunAsync(new AddRequest { Components = ["button"], NoDeps = true });

            Assert.Equal(["button"], result.Items);
            Assert.Empty(result.NugetPackages);
            Assert.True(dir.Exists("Components/Ui/Button/Button.razor"));
            Assert.False(dir.Exists("Components/Ui/Cn.cs"));
            Assert.Equal("namespace Acme.Ui.Button;", dir.Read("Components/Ui/Button/Button.razor"));
        }
    }

    [Fact]
    public async Task Records_each_items_dependencies_for_the_offline_remove_guard()
    {
        var (svc, dir) = Build(TwoItems());
        using (dir)
        {
            await svc.RunAsync(new AddRequest { Components = ["button"], NoNuget = true });

            var config = await ConfigStore.RequireAsync(dir.Path);
            Assert.Equal(["utils"], config.Installed["button"].Dependencies);
            Assert.Equal([], config.Installed["utils"].Dependencies);
        }
    }

    [Fact]
    public async Task Updates_imports_after_a_real_write()
    {
        var (svc, dir) = Build(TwoItems());
        using (dir)
        {
            var result = await svc.RunAsync(new AddRequest { Components = ["button"], NoDeps = true });

            Assert.True(result.ImportsUpdated);
            var imports = dir.Read("_Imports.razor");
            Assert.Contains("@using Acme.Ui", imports);   // styled layer
            Assert.Contains("@using Blaizio", imports);   // headless Base layer
        }
    }

    [Fact]
    public async Task Writes_a_global_using_so_copied_cs_files_resolve_the_base_namespace()
    {
        var (svc, dir) = Build(TwoItems());
        using (dir)
        {
            await svc.RunAsync(new AddRequest { Components = ["button"], NoDeps = true });
            Assert.Contains("global using Blaizio;", dir.Read("Components/Ui/Blaizio.GlobalUsings.g.cs"));
        }
    }

    [Fact]
    public async Task Namespace_override_beats_config()
    {
        var (svc, dir) = Build(TwoItems());
        using (dir)
        {
            await svc.RunAsync(new AddRequest { Components = ["button"], NoDeps = true, NamespaceOverride = "Other.Ns" });
            Assert.Equal("namespace Other.Ns.Button;", dir.Read("Components/Ui/Button/Button.razor"));
        }
    }

    [Fact]
    public async Task Prune_deletes_orphans_but_keeps_shipped_files_and_the_global_usings()
    {
        var (svc, dir) = Build(TwoItems());
        using (dir)
        {
            // A previous add's leftovers: a file the registry no longer ships and its now-empty-to-be folder.
            dir.Write("Components/Ui/Button/IButtonService.cs", "old");
            dir.Write("Components/Ui/Toast/ToastService.cs", "old");

            var result = await svc.RunAsync(new AddRequest { Components = ["button", "utils"], Prune = true, NoNuget = true });

            Assert.False(dir.Exists("Components/Ui/Button/IButtonService.cs"));
            Assert.False(dir.Exists("Components/Ui/Toast/ToastService.cs"));
            Assert.False(Directory.Exists(dir.Combine("Components/Ui/Toast")));
            Assert.True(dir.Exists("Components/Ui/Button/Button.razor"));
            Assert.True(dir.Exists("Components/Ui/Cn.cs"));
            Assert.True(dir.Exists("Components/Ui/Blaizio.GlobalUsings.g.cs"));

            var deleted = result.Files.Where(f => f.Action == WriteAction.Deleted).Select(f => f.Path).ToList();
            Assert.Equal(2, deleted.Count);
            Assert.Contains(deleted, p => p.EndsWith("IButtonService.cs"));
            Assert.Contains(deleted, p => p.EndsWith("ToastService.cs"));
        }
    }

    [Fact]
    public async Task Prune_dry_run_reports_deletions_without_touching_disk()
    {
        var (svc, dir) = Build(TwoItems());
        using (dir)
        {
            dir.Write("Components/Ui/Toast/ToastService.cs", "old");

            var result = await svc.RunAsync(new AddRequest { Components = ["button", "utils"], Prune = true, DryRun = true });

            Assert.True(dir.Exists("Components/Ui/Toast/ToastService.cs"));
            Assert.Contains(result.Files, f => f.Action == WriteAction.Deleted && f.Path.EndsWith("ToastService.cs"));
        }
    }

    [Fact]
    public async Task Prune_drops_installed_entries_for_items_gone_from_the_registry()
    {
        var client = TwoItems();
        var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        var project = ProjectContext.Discover(dir.Path);
        var config = new BlaizioConfig { Namespace = "Acme.Ui", Output = "Components/Ui" };
        config.Installed["ghost"] = new InstalledItem { Files = ["Ghost/Ghost.razor"] };
        var svc = new AddService(client, project, config, new DotnetCli(dir.Path));
        using (dir)
        {
            await svc.RunAsync(new AddRequest { Components = ["button", "utils"], Prune = true, NoNuget = true });

            Assert.False(config.Installed.ContainsKey("ghost"));
            Assert.True(config.Installed.ContainsKey("button"));
        }
    }
}
