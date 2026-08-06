using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Writing;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// Universal items: <c>registry:file</c>/<c>registry:page</c> files land outside the component
/// folder via <c>~/</c> project-root-relative targets, stay contained, and come back out by record.
/// </summary>
public class UniversalItemTests
{
    // ---- destination mapping ----

    [Fact]
    public void A_loose_file_lands_at_its_rooted_target_and_never_nests_under_a_namespace()
    {
        var file = new RegistryFile { Path = "Files/robots.txt", Type = FileType.File, Target = "~/wwwroot/robots.txt" };

        Assert.Equal("~/wwwroot/robots.txt", ComponentWriter.DestinationFor(file));
        Assert.Equal("~/wwwroot/robots.txt", ComponentWriter.DestinationFor(file, subdir: "Acme"));
    }

    [Fact]
    public void A_rooted_target_without_the_prefix_is_still_rooted()
    {
        var file = new RegistryFile { Path = "Files/svc.cs", Type = FileType.File, Target = "Services/ChartService.cs" };

        Assert.Equal("~/Services/ChartService.cs", ComponentWriter.DestinationFor(file));
    }

    [Fact]
    public void A_page_defaults_into_the_pages_folder()
    {
        var file = new RegistryFile { Path = "Pages/Dashboard.razor", Type = FileType.Page };

        Assert.Equal("~/Pages/Dashboard.razor", ComponentWriter.DestinationFor(file));
        Assert.Equal("~/Components/Pages/Dashboard.razor", ComponentWriter.DestinationFor(file, pagesDir: "Components/Pages"));
    }

    [Fact]
    public void A_component_file_still_maps_output_relative()
    {
        var file = new RegistryFile { Path = "Ui/Button/Button.razor" };

        Assert.Equal("Button/Button.razor", ComponentWriter.DestinationFor(file));
        Assert.Equal("Acme/Button/Button.razor", ComponentWriter.DestinationFor(file, subdir: "Acme"));
    }

    [Fact]
    public void A_rooted_record_resolves_under_the_project_and_cannot_escape_it()
    {
        using var dir = new TempDir();

        var abs = ComponentWriter.ResolveReported(dir.Path, "Components/Ui", "~/wwwroot/robots.txt");
        Assert.Equal(Path.Combine(dir.Path, "wwwroot", "robots.txt"), abs);

        Assert.ThrowsAny<Exception>(() =>
            ComponentWriter.ResolveReported(dir.Path, "Components/Ui", "~/../outside.txt"));
    }

    // ---- add / remove round-trip ----

    private static (AddService Add, BlaizioConfig Config, TempDir Dir) Build(FakeRegistryClient client)
    {
        var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        var project = ProjectContext.Discover(dir.Path);
        var config = new BlaizioConfig { Namespace = "Acme.Ui" };
        return (new AddService(client, project, config, new DotnetCli(dir.Path)), config, dir);
    }

    private static FakeRegistryClient Seo() => new FakeRegistryClient().Add(new RegistryItem
    {
        Name = "seo-files",
        Type = ItemType.File,
        Files =
        [
            new RegistryFile { Path = "Files/robots.txt", Type = FileType.File, Target = "~/wwwroot/robots.txt", Content = "User-agent: *\n" },
            new RegistryFile { Path = "Pages/Sitemap.razor", Type = FileType.Page, Content = "@page \"/sitemap\"\n" },
        ],
    });

    [Fact]
    public async Task Add_writes_rooted_files_records_them_and_remove_deletes_them()
    {
        var (add, config, dir) = Build(Seo());
        using (dir)
        {
            var result = await add.RunAsync(new AddRequest { Components = ["seo-files"], NoNuget = true });

            Assert.True(dir.Exists("wwwroot/robots.txt"));
            Assert.True(dir.Exists("Pages/Sitemap.razor"));
            Assert.Contains("~/wwwroot/robots.txt", result.Files.Select(f => f.Path));
            Assert.Contains("~/wwwroot/robots.txt", config.Installed["seo-files"].Files.Select(f => f.Path));

            var remove = await new RemoveService(Seo()).RunAsync(
                dir.Path, new RemoveRequest { Components = ["seo-files"], Force = true });

            Assert.Contains("wwwroot/robots.txt", remove.Removed);
            Assert.False(dir.Exists("wwwroot/robots.txt"));
            Assert.False(dir.Exists("Pages/Sitemap.razor"));
        }
    }

    [Fact]
    public async Task A_loose_file_without_a_target_fails_before_anything_mutates()
    {
        var (add, _, dir) = Build(new FakeRegistryClient().Add(new RegistryItem
        {
            Name = "broken",
            Files = [new RegistryFile { Path = "Files/x.txt", Type = FileType.File, Content = "x" }],
        }));
        using (dir)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => add.RunAsync(new AddRequest { Components = ["broken"], NoNuget = true }));

            Assert.Contains("no target", ex.Message);
            Assert.False(dir.Exists("Components/Ui/x.txt"));
        }
    }

    [Fact]
    public async Task A_page_lands_in_components_pages_when_the_project_has_one()
    {
        var (add, _, dir) = Build(Seo());
        using (dir)
        {
            Directory.CreateDirectory(dir.Combine("Components", "Pages"));

            await add.RunAsync(new AddRequest { Components = ["seo-files"], NoNuget = true });

            Assert.True(dir.Exists("Components/Pages/Sitemap.razor"));
            Assert.False(dir.Exists("Pages/Sitemap.razor"));
        }
    }
}
