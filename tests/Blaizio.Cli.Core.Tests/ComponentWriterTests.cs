using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Rewriting;
using Blaizio.Cli.Core.Writing;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class ComponentWriterTests
{
    private static RegistryItem ButtonItem(string content = "namespace Blaizio.Ui.Button;") => new()
    {
        Name = "button",
        Files = [new RegistryFile { Path = "Ui/Button/Button.razor", Type = FileType.Ui, Content = content }],
    };

    private static ComponentWriter Writer(TempDir dir, string output = "Components/Ui")
        => new(dir.Path, output, new NamespaceRewriter("MyApp.Ui"));

    [Fact]
    public async Task Strips_the_item_type_prefix_and_writes_under_the_output_dir()
    {
        using var dir = new TempDir();
        var result = await Writer(dir).WriteAsync(ButtonItem(), overwrite: false, dryRun: false);

        var written = Assert.Single(result);
        Assert.Equal(WriteAction.Created, written.Action);
        Assert.Equal("Button\\Button.razor".Replace('\\', Path.DirectorySeparatorChar), written.RelativePath);
        Assert.True(dir.Exists("Components/Ui/Button/Button.razor"));
    }

    [Fact]
    public async Task Applies_the_namespace_rewrite_to_written_content()
    {
        using var dir = new TempDir();
        await Writer(dir).WriteAsync(ButtonItem(), overwrite: false, dryRun: false);

        Assert.Equal("namespace MyApp.Ui.Button;", dir.Read("Components/Ui/Button/Button.razor"));
    }

    [Fact]
    public async Task Skips_an_existing_file_when_overwrite_is_false()
    {
        using var dir = new TempDir();
        dir.Write("Components/Ui/Button/Button.razor", "OLD");

        var result = await Writer(dir).WriteAsync(ButtonItem(), overwrite: false, dryRun: false);

        Assert.Equal(WriteAction.Skipped, result[0].Action);
        Assert.Equal("OLD", dir.Read("Components/Ui/Button/Button.razor"));
    }

    [Fact]
    public async Task Overwrites_an_existing_file_when_allowed()
    {
        using var dir = new TempDir();
        dir.Write("Components/Ui/Button/Button.razor", "OLD");

        var result = await Writer(dir).WriteAsync(ButtonItem(), overwrite: true, dryRun: false);

        Assert.Equal(WriteAction.Overwritten, result[0].Action);
        Assert.Equal("namespace MyApp.Ui.Button;", dir.Read("Components/Ui/Button/Button.razor"));
    }

    [Fact]
    public async Task DryRun_plans_without_touching_the_disk()
    {
        using var dir = new TempDir();
        var result = await Writer(dir).WriteAsync(ButtonItem(), overwrite: false, dryRun: true);

        Assert.Equal(WriteAction.Planned, result[0].Action);
        Assert.False(dir.Exists("Components/Ui/Button/Button.razor"));
    }

    [Fact]
    public async Task Honors_an_explicit_target_over_the_stripped_path()
    {
        using var dir = new TempDir();
        var item = new RegistryItem
        {
            Name = "utils",
            Files = [new RegistryFile { Path = "Lib/Cn.cs", Target = "Internal/Cn.cs", Content = "x", Type = FileType.Lib }],
        };

        await Writer(dir).WriteAsync(item, overwrite: false, dryRun: false);

        Assert.True(dir.Exists("Components/Ui/Internal/Cn.cs"));
    }

    [Fact]
    public async Task Throws_when_a_file_has_no_content()
    {
        using var dir = new TempDir();
        var item = new RegistryItem
        {
            Name = "bad",
            Files = [new RegistryFile { Path = "Ui/Bad.razor", Content = null }],
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Writer(dir).WriteAsync(item, overwrite: false, dryRun: false));
    }
}
