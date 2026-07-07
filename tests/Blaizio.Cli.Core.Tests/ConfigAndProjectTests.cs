using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Projects;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class ConfigStoreTests
{
    [Fact]
    public async Task Round_trips_a_config()
    {
        using var dir = new TempDir();
        var config = new BlaizioConfig { Namespace = "Acme.Ui", Output = "Comp/Ui", Theme = "spark", Rtl = true };

        await ConfigStore.SaveAsync(dir.Path, config);
        var loaded = await ConfigStore.LoadAsync(dir.Path);

        Assert.NotNull(loaded);
        Assert.Equal("Acme.Ui", loaded!.Namespace);
        Assert.Equal("Comp/Ui", loaded.Output);
        Assert.Equal("spark", loaded.Theme);
        Assert.True(loaded.Rtl);
    }

    [Fact]
    public async Task Load_returns_null_when_absent()
    {
        using var dir = new TempDir();
        Assert.Null(await ConfigStore.LoadAsync(dir.Path));
        Assert.False(ConfigStore.Exists(dir.Path));
    }

    [Fact]
    public async Task Require_throws_when_absent()
    {
        using var dir = new TempDir();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ConfigStore.RequireAsync(dir.Path));
    }
}

public class ProjectContextTests
{
    [Fact]
    public void Reads_root_namespace_from_csproj()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><RootNamespace>Acme.Web</RootNamespace></PropertyGroup></Project>");

        var project = ProjectContext.Discover(dir.Path);

        Assert.Equal("Acme.Web", project.RootNamespace);
        Assert.Equal("Acme.Web.Components.Ui", project.DefaultComponentNamespace);
        Assert.NotNull(project.CsprojPath);
    }

    [Fact]
    public void Falls_back_to_assembly_name_when_no_root_namespace()
    {
        using var dir = new TempDir();
        dir.Write("MyLib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        var project = ProjectContext.Discover(dir.Path);

        Assert.Equal("MyLib", project.AssemblyName);
        Assert.Equal("MyLib", project.RootNamespace);
    }

    [Fact]
    public void Handles_a_directory_with_no_csproj()
    {
        using var dir = new TempDir();
        var project = ProjectContext.Discover(dir.Path);
        Assert.Null(project.CsprojPath);
    }
}

public class NamespaceResolverTests
{
    private static ProjectContext Project(string dir) => ProjectContext.Discover(dir);

    [Fact]
    public void Flag_wins_over_config_and_project()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        var config = new BlaizioConfig { Namespace = "Cfg.Ui" };

        Assert.Equal("Flag.Ui", NamespaceResolver.Resolve("Flag.Ui", config, Project(dir.Path)));
    }

    [Fact]
    public void Config_wins_over_project_when_no_flag()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        var config = new BlaizioConfig { Namespace = "Cfg.Ui" };

        Assert.Equal("Cfg.Ui", NamespaceResolver.Resolve(flag: null, config, Project(dir.Path)));
    }

    [Fact]
    public void Falls_back_to_project_default()
    {
        using var dir = new TempDir();
        dir.Write("App.csproj", "<Project><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");

        Assert.Equal("Acme.Components.Ui", NamespaceResolver.Resolve(flag: null, config: null, Project(dir.Path)));
    }
}

public class ImportsUpdaterTests
{
    [Fact]
    public async Task Adds_using_and_creates_the_file()
    {
        using var dir = new TempDir();
        var changed = await ImportsUpdater.EnsureUsingAsync(dir.Path, "Acme.Ui");

        Assert.True(changed);
        Assert.Contains("@using Acme.Ui", dir.Read("_Imports.razor"));
    }

    [Fact]
    public async Task Is_idempotent()
    {
        using var dir = new TempDir();
        await ImportsUpdater.EnsureUsingAsync(dir.Path, "Acme.Ui");
        var changedAgain = await ImportsUpdater.EnsureUsingAsync(dir.Path, "Acme.Ui");

        Assert.False(changedAgain);
        var occurrences = dir.Read("_Imports.razor").Split("@using Acme.Ui").Length - 1;
        Assert.Equal(1, occurrences);
    }
}
