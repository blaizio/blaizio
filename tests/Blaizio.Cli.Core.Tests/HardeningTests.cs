using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Rewriting;
using Blaizio.Cli.Core.Templates;
using Blaizio.Cli.Core.Writing;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>Tests for the path-containment, config and rewriter hardening.</summary>
public class HardeningTests
{
    private static RegistryItem ItemWithPath(string path, string? target = null) => new()
    {
        Name = "evil",
        Files = [new RegistryFile { Path = path, Target = target, Type = FileType.Ui, Content = "x" }],
    };

    private static ComponentWriter Writer(TempDir dir)
        => new(dir.Path, "Components/Ui", new NamespaceRewriter("MyApp.Ui"));

    [Fact]
    public async Task ComponentWriter_rejects_a_parent_traversal_target()
    {
        using var dir = new TempDir();
        var item = ItemWithPath("Ui/x.razor", target: "../../outside.razor");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Writer(dir).WriteAsync(item, overwrite: false, dryRun: false));
    }

    [Fact]
    public async Task ComponentWriter_rejects_a_rooted_target()
    {
        using var dir = new TempDir();
        var rooted = OperatingSystem.IsWindows() ? "C:\\evil\\x.razor" : "/evil/x.razor";
        var item = ItemWithPath("Ui/x.razor", target: rooted);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Writer(dir).WriteAsync(item, overwrite: false, dryRun: false));
    }

    [Fact]
    public async Task TemplateScaffolder_rejects_an_escaping_relative_path()
    {
        using var dir = new TempDir();
        var provider = new FakeTemplateProvider(
            new TemplateFile("../outside.txt", "x"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new TemplateScaffolder(provider).ScaffoldAsync(
                dir.Path, "any", new TemplateTokens("A", "A.Ui", "A", "ember")));
    }

    [Fact]
    public void SafePath_rejects_a_sibling_prefix_directory()
    {
        using var dir = new TempDir();
        var root = dir.Combine("proj");
        Directory.CreateDirectory(root);

        // "proj-evil" starts with "proj" as a string but is not beneath it - the containment
        // check must compare whole path segments, not raw prefixes.
        Assert.Throws<InvalidOperationException>(() => SafePath.Resolve(root, "../proj-evil/x.txt"));
    }

    [Fact]
    public void SafePath_ResolveDir_accepts_the_root_itself_but_not_an_escape()
    {
        using var dir = new TempDir();

        Assert.Equal(System.IO.Path.GetFullPath(dir.Path), SafePath.ResolveDir(dir.Path, "."));
        Assert.Throws<InvalidOperationException>(() => SafePath.ResolveDir(dir.Path, ".."));
        Assert.Throws<InvalidOperationException>(() => SafePath.ResolveDir(dir.Path, "/evil"));
    }

    [Fact]
    public async Task RemoveService_refuses_a_record_that_escapes_the_project()
    {
        using var dir = new TempDir();
        dir.Write("secret.txt", "keep");
        dir.Write("proj/blaizio.json",
            """
            {
              "namespace": "App.Ui",
              "output": "Components/Ui",
              "installed": { "evil": { "files": ["../../../secret.txt"] } }
            }
            """);

        var registry = new FakeRegistryClient().Add(new RegistryItem { Name = "evil" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RemoveService(registry)
            .RunAsync(dir.Combine("proj"), new RemoveRequest { Components = ["evil"] }));
        Assert.True(dir.Exists("secret.txt"));
    }

    [Fact]
    public async Task RemoveService_refuses_a_rooted_output()
    {
        using var dir = new TempDir();
        dir.Write("proj/blaizio.json",
            """
            {
              "namespace": "App.Ui",
              "output": "/evil",
              "installed": { "slider": { "files": ["BzSlider.razor"] } }
            }
            """);

        var registry = new FakeRegistryClient().Add(new RegistryItem { Name = "slider" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RemoveService(registry)
            .RunAsync(dir.Combine("proj"), new RemoveRequest { Components = ["slider"] }));
    }

    [Fact]
    public async Task UninstallService_refuses_a_record_that_escapes_the_project()
    {
        using var dir = new TempDir();
        dir.Write("secret.txt", "keep");
        dir.Write("proj/blaizio.json",
            """
            {
              "namespace": "App.Ui",
              "output": "Components/Ui",
              "installed": { "evil": { "files": ["../../../secret.txt"] } }
            }
            """);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new UninstallService().RunAsync(dir.Combine("proj")));
        Assert.True(dir.Exists("secret.txt"));
    }

    [Fact]
    public async Task UninstallService_refuses_a_rooted_custom_css_path()
    {
        using var dir = new TempDir();
        dir.Write("proj/blaizio.json",
            """
            {
              "namespace": "App.Ui",
              "css": "/outside/app.css"
            }
            """);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new UninstallService().RunAsync(dir.Combine("proj")));
    }

    [Fact]
    public void NamespaceRewriter_treats_dollar_in_target_literally()
    {
        var rewriter = new NamespaceRewriter("My$App.Ui");
        Assert.Equal("namespace My$App.Ui.Button;", rewriter.Rewrite("namespace Blaizio.Ui.Button;"));
    }

    [Fact]
    public void Config_aliases_survive_a_null_assignment()
    {
        var config = new BlaizioConfig { Namespace = "X", Aliases = null! };
        Assert.NotNull(config.Aliases);
        Assert.False(config.Aliases.TryGetValue("base", out _));
    }

    [Fact]
    public async Task ConfigStore_reports_invalid_json_with_file_context()
    {
        using var dir = new TempDir();
        dir.Write(BlaizioConfig.FileName, "{ not json");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => ConfigStore.LoadAsync(dir.Path));
        Assert.Contains(BlaizioConfig.FileName, ex.Message);
    }

    [Fact]
    public async Task ConfigStore_save_leaves_no_temp_file_behind()
    {
        using var dir = new TempDir();
        await ConfigStore.SaveAsync(dir.Path, new BlaizioConfig { Namespace = "X" });

        Assert.True(dir.Exists(BlaizioConfig.FileName));
        Assert.False(dir.Exists(BlaizioConfig.FileName + ".tmp"));
    }
}
