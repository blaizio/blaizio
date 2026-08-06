using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// Items shipping <c>css</c> blocks: the marker-fenced region engine, the add leg writing it into
/// the tokens file, and remove stripping it back out - all by record.
/// </summary>
public class ItemCssRegionTests
{
    private static readonly Dictionary<string, string> Spin = new()
    {
        ["@keyframes bz-spin"] = "from { rotate: 0deg; }\nto { rotate: 360deg; }",
        ["@utility tab-4"] = "tab-size: 4;",
    };

    // ---- the region engine ----

    [Fact]
    public void Apply_appends_a_fenced_region_and_replaces_it_in_place()
    {
        var css = ":root {\n  --radius: 0.75rem;\n}\n";

        var once = ItemCssRegions.Apply(css, "marquee", Spin);
        Assert.Contains("/* blaizio:css marquee */", once);
        Assert.Contains("@keyframes bz-spin {", once);
        Assert.Contains("  from { rotate: 0deg; }", once);
        Assert.Contains("/* blaizio:css:end marquee */", once);

        // Re-applying different blocks replaces the region, not duplicates it.
        var again = ItemCssRegions.Apply(once, "marquee", new Dictionary<string, string> { ["@utility tab-2"] = "tab-size: 2;" });
        Assert.DoesNotContain("bz-spin", again);
        Assert.Contains("tab-2", again);
        Assert.Single(ItemCssRegions.Items(again));
    }

    [Fact]
    public void Remove_strips_exactly_the_items_region()
    {
        var css = ItemCssRegions.Apply(
            ItemCssRegions.Apply(":root {\n}\n", "a", Spin),
            "b", new Dictionary<string, string> { ["@utility x"] = "y: z;" });

        var stripped = ItemCssRegions.Remove(css, "a");

        Assert.DoesNotContain("blaizio:css a", stripped);
        Assert.DoesNotContain("bz-spin", stripped);
        Assert.Contains("/* blaizio:css b */", stripped);
        Assert.Contains(":root {", stripped);
    }

    [Fact]
    public void A_broken_close_marker_never_eats_user_css()
    {
        var css = "/* blaizio:css a */\n@keyframes k { }\n.user-rule { color: red; }\n";

        var stripped = ItemCssRegions.Remove(css, "a");

        Assert.DoesNotContain("blaizio:css", stripped);
        Assert.Contains(".user-rule { color: red; }", stripped);
    }

    // ---- add / remove integration ----

    private static (AddService Add, RemoveService Remove, BlaizioConfig Config, TempDir Dir) Build(FakeRegistryClient client)
    {
        var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        dir.Write("Styles/app.css", "@import \"tailwindcss\";\n\n:root {\n  --radius: 0.75rem;\n}\n");
        var project = ProjectContext.Discover(dir.Path);
        var config = new BlaizioConfig { Namespace = "Acme.Ui" };
        var add = new AddService(client, project, config, new DotnetCli(dir.Path));
        return (add, new RemoveService(client), config, dir);
    }

    private static FakeRegistryClient Marquee() => new FakeRegistryClient().Add(new RegistryItem
    {
        Name = "marquee",
        Css = Spin,
        Files = [new RegistryFile { Path = "Ui/Marquee/Marquee.razor", Content = "m" }],
    });

    [Fact]
    public async Task Add_writes_the_region_records_it_and_remove_strips_it()
    {
        var (add, remove, config, dir) = Build(Marquee());
        using (dir)
        {
            await add.RunAsync(new AddRequest { Components = ["marquee"], NoNuget = true });

            var tokens = dir.Read("Styles/app.css");
            Assert.Contains("/* blaizio:css marquee */", tokens);
            Assert.Contains("@keyframes bz-spin", tokens);
            Assert.True(config.Installed["marquee"].Css);

            var result = await remove.RunAsync(dir.Path, new RemoveRequest { Components = ["marquee"], Force = true });

            Assert.Contains("Styles/app.css", result.Cleaned);
            var after = dir.Read("Styles/app.css");
            Assert.DoesNotContain("blaizio:css", after);
            Assert.DoesNotContain("bz-spin", after);
            Assert.Contains("--radius: 0.75rem;", after);
        }
    }

    [Fact]
    public async Task Without_a_tokens_file_the_blocks_are_skipped_not_fatal()
    {
        var (add, _, config, dir) = Build(Marquee());
        using (dir)
        {
            File.Delete(dir.Combine("Styles", "app.css"));

            var result = await add.RunAsync(new AddRequest { Components = ["marquee"], NoNuget = true });

            Assert.Contains("marquee", result.Items);
            Assert.True(dir.Exists("Components/Ui/Marquee/Marquee.razor"));
            Assert.False(config.Installed["marquee"].Css);
        }
    }
}
