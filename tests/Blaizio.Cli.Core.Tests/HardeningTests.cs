using Blaizio.Cli.Core.Configuration;
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
