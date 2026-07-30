using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Resolution;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Core.Writing;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// The community-registry surface: theme items patching the tokens file, and namespaced
/// (@ns/item) installs nesting under their own subfolder with qualified records.
/// </summary>
public class CommunityRegistryTests
{
    private const string Tokens =
        """
        @import "tailwindcss";

        :root {
          --background: oklch(1 0 0);
          --primary: oklch(0.2 0 0);
          --radius: 0.75rem;
        }

        .dark {
          --background: oklch(0.15 0 0);
          --primary: oklch(0.9 0 0);
        }
        """;

    // ---- TailwindSetup.ApplyCssVars ----------------------------------------------------------

    [Fact]
    public void ApplyCssVars_patches_light_into_root_and_dark_into_dark()
    {
        var vars = new CssVarsSpec
        {
            Light = new Dictionary<string, string> { ["--primary"] = "oklch(0.5 0.2 250)" },
            Dark = new Dictionary<string, string> { ["--primary"] = "oklch(0.8 0.15 250)" },
        };

        var css = TailwindSetup.ApplyCssVars(Tokens, vars);

        Assert.Equal("oklch(0.5 0.2 250)", CssBlocks.Declarations(css, ":root").Single(d => d.Name == "--primary").Value);
        Assert.Equal("oklch(0.8 0.15 250)", CssBlocks.Declarations(css, ".dark").Single(d => d.Name == "--primary").Value);
        // Untouched declarations survive.
        Assert.Equal("0.75rem", CssBlocks.Declarations(css, ":root").Single(d => d.Name == "--radius").Value);
    }

    [Fact]
    public void ApplyCssVars_accepts_names_without_the_dash_prefix()
    {
        var vars = new CssVarsSpec { Light = new Dictionary<string, string> { ["primary"] = "red" } };

        var css = TailwindSetup.ApplyCssVars(Tokens, vars);

        Assert.Equal("red", CssBlocks.Declarations(css, ":root").Single(d => d.Name == "--primary").Value);
    }

    [Fact]
    public void ApplyCssVars_appends_a_missing_dark_block()
    {
        var vars = new CssVarsSpec { Dark = new Dictionary<string, string> { ["--primary"] = "blue" } };

        var css = TailwindSetup.ApplyCssVars(":root {\n  --primary: red;\n}\n", vars);

        Assert.Equal("blue", CssBlocks.Declarations(css, ".dark").Single(d => d.Name == "--primary").Value);
    }

    // ---- AddService: theme items -------------------------------------------------------------

    private static (AddService svc, TempDir dir, BlaizioConfig config) Project(IRegistryClient client)
    {
        var dir = new TempDir();
        dir.Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><RootNamespace>Acme</RootNamespace></PropertyGroup></Project>");
        var project = ProjectContext.Discover(dir.Path);
        var config = new BlaizioConfig { Namespace = "Acme.Ui", Output = "Components/Ui" };
        return (new AddService(client, project, config, new DotnetCli(dir.Path)), dir, config);
    }

    private static RegistryItem Theme(string name = "midnight") => new()
    {
        Name = name,
        Type = ItemType.Theme,
        CssVars = new CssVarsSpec
        {
            Light = new Dictionary<string, string> { ["--primary"] = "oklch(0.45 0.2 280)" },
            Dark = new Dictionary<string, string> { ["--primary"] = "oklch(0.75 0.18 280)" },
        },
    };

    [Fact]
    public async Task Theme_item_patches_the_tokens_file_and_lands_in_the_record()
    {
        var (svc, dir, config) = Project(new FakeRegistryClient().Add(Theme()));
        using (dir)
        {
            dir.Write("Styles/app.css", Tokens);

            var result = await svc.RunAsync(new AddRequest { Components = ["midnight"], NoNuget = true });

            var css = dir.Read("Styles/app.css");
            Assert.Equal("oklch(0.45 0.2 280)", CssBlocks.Declarations(css, ":root").Single(d => d.Name == "--primary").Value);
            Assert.Equal("oklch(0.75 0.18 280)", CssBlocks.Declarations(css, ".dark").Single(d => d.Name == "--primary").Value);
            Assert.Contains("midnight", config.Installed.Keys);
            Assert.Contains(result.Files, f => f.Path == "Styles/app.css" && f.Action == WriteAction.Overwritten);
            // A theme add copies no components, so imports stay untouched.
            Assert.False(dir.Exists("_Imports.razor"));
        }
    }

    [Fact]
    public async Task Theme_item_dry_run_only_plans_the_tokens_patch()
    {
        var (svc, dir, _) = Project(new FakeRegistryClient().Add(Theme()));
        using (dir)
        {
            dir.Write("Styles/app.css", Tokens);

            var result = await svc.RunAsync(new AddRequest { Components = ["midnight"], DryRun = true });

            Assert.Contains(result.Files, f => f.Path == "Styles/app.css" && f.Action == WriteAction.Planned);
            Assert.Equal(Tokens, dir.Read("Styles/app.css"));
        }
    }

    [Fact]
    public async Task Theme_item_without_a_tokens_file_fails_loudly()
    {
        var (svc, dir, _) = Project(new FakeRegistryClient().Add(Theme()));
        using (dir)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.RunAsync(new AddRequest { Components = ["midnight"], NoNuget = true }));
        }
    }

    [Fact]
    public async Task Theme_item_without_cssVars_fails_loudly()
    {
        var empty = new RegistryItem { Name = "hollow", Type = ItemType.Theme };
        var (svc, dir, _) = Project(new FakeRegistryClient().Add(empty));
        using (dir)
        {
            dir.Write("Styles/app.css", Tokens);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.RunAsync(new AddRequest { Components = ["hollow"], NoNuget = true }));
        }
    }

    // ---- ComponentWriter.FolderFor -----------------------------------------------------------

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("@acme", "Acme")]
    [InlineData("@acme-ui", "AcmeUi")]
    [InlineData("@big_corp.design", "BigCorpDesign")]
    public void FolderFor_maps_namespaces_to_pascal_case_folders(string? ns, string? expected) =>
        Assert.Equal(expected, ComponentWriter.FolderFor(ns));

    // ---- Namespaced installs -----------------------------------------------------------------

    private static IRegistryClient Composite()
    {
        var fallback = new FakeRegistryClient().Add(new RegistryItem
        {
            Name = "button",
            Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "namespace Blaizio.Ui.Button;" }],
        });
        var acme = new FakeRegistryClient()
            .Add(new RegistryItem
            {
                Name = "button",
                RegistryDependencies = ["chip"],
                Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Content = "namespace Blaizio.Ui.Button; // acme" }],
            })
            .Add(new RegistryItem
            {
                Name = "chip",
                Files = [new RegistryFile { Path = "Ui/Chip/Chip.razor", Content = "namespace Blaizio.Ui.Chip;" }],
            });
        return new NamespacedRegistryClient(fallback, new Dictionary<string, IRegistryClient> { ["@acme"] = acme });
    }

    [Fact]
    public async Task Resolver_keeps_a_namespaced_item_distinct_from_its_default_registry_twin()
    {
        var graph = await new DependencyResolver(Composite()).ResolveAsync(["button", "@acme/button"]);

        Assert.Equal(["button", "@acme/chip", "@acme/button"], graph.Items.Select(i => i.QualifiedName));
    }

    [Fact]
    public async Task Namespaced_add_nests_files_namespace_and_record_under_the_registry_folder()
    {
        var (svc, dir, config) = Project(Composite());
        using (dir)
        {
            await svc.RunAsync(new AddRequest { Components = ["button", "@acme/button"], NoNuget = true });

            // The default registry's button stays where it always was.
            Assert.Equal("namespace Acme.Ui.Button;", dir.Read("Components/Ui/Button/Button.razor"));
            // The @acme twin nests: its own folder, one namespace segment down, plus its dependency.
            Assert.Equal("namespace Acme.Ui.Acme.Button; // acme", dir.Read("Components/Ui/Acme/Button/Button.razor"));
            Assert.True(dir.Exists("Components/Ui/Acme/Chip/Chip.razor"));

            Assert.Contains("button", config.Installed.Keys);
            Assert.Contains("@acme/button", config.Installed.Keys);
            Assert.Contains("@acme/chip", config.Installed.Keys);
            Assert.Equal(["Acme/Button/Button.razor"], config.Installed["@acme/button"].Files);

            Assert.Contains("@using Acme.Ui.Acme", dir.Read("_Imports.razor"));
        }
    }

    [Fact]
    public async Task Prune_spares_files_recorded_by_other_namespaced_installs()
    {
        var (svc, dir, config) = Project(Composite());
        using (dir)
        {
            config.Installed["@acme/button"] = new InstalledItem { Files = ["Acme/Button/Button.razor"] };
            dir.Write("Components/Ui/Acme/Button/Button.razor", "namespace Acme.Ui.Acme.Button;");

            await svc.RunAsync(new AddRequest { Components = ["button"], Prune = true, NoNuget = true });

            Assert.True(dir.Exists("Components/Ui/Acme/Button/Button.razor"));
            Assert.Contains("@acme/button", config.Installed.Keys);
        }
    }

    [Fact]
    public async Task Remove_finds_a_namespaced_record_without_the_at_sign()
    {
        using var dir = new TempDir();
        dir.Write("blaizio.json",
            """
            {
              "namespace": "App.Components.Ui",
              "output": "Components/Ui",
              "installed": {
                "@acme/button": { "files": ["Acme/Button/Button.razor"] }
              }
            }
            """);
        dir.Write("Components/Ui/Acme/Button/Button.razor", "<div />");

        var result = await new RemoveService(new FakeRegistryClient())
            .RunAsync(dir.Path, new RemoveRequest { Components = ["acme/button"] });

        Assert.Equal(["@acme/button"], result.Items);
        Assert.False(dir.Exists("Components/Ui/Acme/Button/Button.razor"));
    }
}
