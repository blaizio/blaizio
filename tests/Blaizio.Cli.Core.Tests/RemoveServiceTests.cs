using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Registry;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class RemoveServiceTests
{
    private static RegistryItem Item(string name, string[]? deps = null, string[]? nuget = null) => new()
    {
        Name = name,
        RegistryDependencies = deps ?? [],
        NugetDependencies = nuget ?? [],
    };

    /// <summary>A project with slider + color-picker installed, each owning one file.</summary>
    private static TempDir Project()
    {
        var dir = new TempDir();
        dir.Write("blaizio.json",
            """
            {
              "namespace": "App.Components.Ui",
              "output": "Components/Ui",
              "packages": ["Blaizio.Base", "SkiaSharp"],
              "installed": {
                "slider": { "files": ["Slider/BzSlider.razor"] },
                "color-picker": { "files": ["ColorPicker/BzColorPicker.razor"] }
              }
            }
            """);
        dir.Write("Components/Ui/Slider/BzSlider.razor", "<div />");
        dir.Write("Components/Ui/ColorPicker/BzColorPicker.razor", "<div />");
        return dir;
    }

    private static FakeRegistryClient Registry() => new FakeRegistryClient()
        .Add(Item("slider", nuget: ["Blaizio.Base"]))
        .Add(Item("color-picker", deps: ["slider"], nuget: ["Blaizio.Base", "SkiaSharp"]));

    [Fact]
    public async Task Removes_the_items_tracked_files_and_its_record()
    {
        using var dir = Project();

        var result = await new RemoveService(Registry())
            .RunAsync(dir.Path, new RemoveRequest { Components = ["color-picker"] });

        Assert.Equal(["color-picker"], result.Items);
        Assert.Contains("Components/Ui/ColorPicker/BzColorPicker.razor", result.Removed);
        Assert.False(dir.Exists("Components/Ui/ColorPicker/BzColorPicker.razor"));

        var config = await ConfigStore.RequireAsync(dir.Path);
        Assert.DoesNotContain("color-picker", config.Installed.Keys);
        // Untouched: the survivor keeps its file and its record.
        Assert.True(dir.Exists("Components/Ui/Slider/BzSlider.razor"));
        Assert.Contains("slider", config.Installed.Keys);
    }

    [Fact]
    public async Task Refuses_an_item_another_installed_component_depends_on()
    {
        using var dir = Project();

        var result = await new RemoveService(Registry())
            .RunAsync(dir.Path, new RemoveRequest { Components = ["slider"] });

        Assert.Empty(result.Items);
        Assert.Equal(["color-picker"], result.Blocked["slider"]);
        Assert.True(dir.Exists("Components/Ui/Slider/BzSlider.razor"));
    }

    [Fact]
    public async Task Force_removes_a_depended_on_item()
    {
        using var dir = Project();

        var result = await new RemoveService(Registry())
            .RunAsync(dir.Path, new RemoveRequest { Components = ["slider"], Force = true });

        Assert.Equal(["slider"], result.Items);
        Assert.Empty(result.Blocked);
        Assert.False(dir.Exists("Components/Ui/Slider/BzSlider.razor"));
    }

    [Fact]
    public async Task Removing_a_dependent_and_its_dependency_together_is_not_blocked()
    {
        using var dir = Project();

        // slider's only dependent is also going, so nothing survives to need it.
        var result = await new RemoveService(Registry())
            .RunAsync(dir.Path, new RemoveRequest { Components = ["color-picker", "slider"] });

        Assert.Equal(["color-picker", "slider"], result.Items);
        Assert.Empty(result.Blocked);
    }

    [Fact]
    public async Task Resolves_names_regardless_of_case_and_separators()
    {
        using var dir = Project();

        var result = await new RemoveService(Registry())
            .RunAsync(dir.Path, new RemoveRequest { Components = ["ColorPicker"] });

        Assert.Equal(["color-picker"], result.Items);
    }

    [Fact]
    public async Task Dry_run_reports_without_touching_anything()
    {
        using var dir = Project();

        var result = await new RemoveService(Registry())
            .RunAsync(dir.Path, new RemoveRequest { Components = ["color-picker"], DryRun = true });

        Assert.Contains("Components/Ui/ColorPicker/BzColorPicker.razor", result.Removed);
        Assert.True(dir.Exists("Components/Ui/ColorPicker/BzColorPicker.razor"));
        var config = await ConfigStore.RequireAsync(dir.Path);
        Assert.Contains("color-picker", config.Installed.Keys);
    }

    [Fact]
    public async Task Keeps_a_file_a_surviving_item_also_ships()
    {
        using var dir = new TempDir();
        dir.Write("blaizio.json",
            """
            {
              "namespace": "App.Components.Ui",
              "output": "Components/Ui",
              "installed": {
                "utils": { "files": ["Tw.cs"] },
                "button": { "files": ["Tw.cs", "Button/BzButton.razor"] }
              }
            }
            """);
        dir.Write("Components/Ui/Tw.cs", "// shared");
        dir.Write("Components/Ui/Button/BzButton.razor", "<button />");

        await new RemoveService(new FakeRegistryClient().Add(Item("utils")).Add(Item("button")))
            .RunAsync(dir.Path, new RemoveRequest { Components = ["button"] });

        Assert.True(dir.Exists("Components/Ui/Tw.cs"));
        Assert.False(dir.Exists("Components/Ui/Button/BzButton.razor"));
    }

    [Fact]
    public async Task Never_touches_a_user_authored_file_under_the_output_directory()
    {
        using var dir = Project();
        dir.Write("Components/Ui/ColorPicker/MyPicker.razor", "<div />");

        await new RemoveService(Registry())
            .RunAsync(dir.Path, new RemoveRequest { Components = ["color-picker"] });

        Assert.True(dir.Exists("Components/Ui/ColorPicker/MyPicker.razor"));
    }

    [Fact]
    public async Task Reports_a_name_that_is_not_installed_and_does_nothing()
    {
        using var dir = Project();

        var result = await new RemoveService(Registry())
            .RunAsync(dir.Path, new RemoveRequest { Components = ["bogus"] });

        Assert.Equal(["bogus"], result.NotInstalled);
        Assert.True(result.NothingToDo);
    }

    [Fact]
    public async Task Reports_orphans_and_unused_packages_without_removing_them()
    {
        using var dir = Project();

        var result = await new RemoveService(Registry())
            .RunAsync(dir.Path, new RemoveRequest { Components = ["color-picker"] });

        // slider came in as color-picker's dependency and nothing needs it now - reported only.
        Assert.Equal(["slider"], result.Orphaned);
        Assert.True(dir.Exists("Components/Ui/Slider/BzSlider.razor"));
        // SkiaSharp was only color-picker's; Blaizio.Base is still slider's.
        Assert.Equal(["SkiaSharp"], result.UnusedPackages);
        var config = await ConfigStore.RequireAsync(dir.Path);
        Assert.Contains("SkiaSharp", config.Packages);
    }

    [Fact]
    public async Task An_unreachable_registry_still_removes_without_the_dependency_guard()
    {
        using var dir = Project();

        // Empty registry: GetIndexAsync succeeds with no items, so nothing is known to depend on
        // slider. The removal proceeds rather than failing the command.
        var result = await new RemoveService(new FakeRegistryClient())
            .RunAsync(dir.Path, new RemoveRequest { Components = ["slider"] });

        Assert.Equal(["slider"], result.Items);
        Assert.Empty(result.Blocked);
        Assert.Empty(result.UnusedPackages);
    }
}
