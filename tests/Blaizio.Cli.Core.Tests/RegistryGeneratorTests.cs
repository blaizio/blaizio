using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Registry.Generation;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class RegistryGeneratorTests
{
    private static TempDir FakeSource()
    {
        var dir = new TempDir();
        // Shared root helpers + an Extensions folder -> the utils item.
        dir.Write("Tw.cs", "namespace Blaizio.Ui; public static class Tw {}");
        dir.Write("Extensions/EnumExtensions.cs", "namespace Blaizio.Ui; static class E {}");
        // Two families; AlertDialog references BzButton (cross-family dep on button).
        dir.Write("Components/Button/BzButton.razor", "<button>@ChildContent</button>");
        dir.Write("Components/Button/ButtonVariant.cs", "namespace Blaizio.Ui; enum V {}");
        dir.Write("Components/AlertDialog/BzAlertDialog.razor", "<BzButton>ok</BzButton>");
        return dir;
    }

    private static RegistryItem Item(RegistryIndex index, string name)
        => index.Items.Single(i => i.Name == name);

    [Fact]
    public void Emits_a_utils_lib_item_from_the_shared_files()
    {
        using var dir = FakeSource();
        var index = new RegistryGenerator().Generate(dir.Path);

        var utils = Item(index, "utils");
        Assert.Equal(ItemType.Lib, utils.Type);
        Assert.Equal(2, utils.Files.Count);
        Assert.Contains(utils.Files, f => f.Path == "Tw.cs");
        Assert.Contains(utils.Files, f => f.Path == "Extensions/EnumExtensions.cs");
    }

    [Fact]
    public void Emits_one_ui_item_per_family_with_kebab_names()
    {
        using var dir = FakeSource();
        var index = new RegistryGenerator().Generate(dir.Path);

        Assert.Contains(index.Items, i => i.Name == "button");
        Assert.Contains(index.Items, i => i.Name == "alert-dialog");   // PascalCase -> kebab
        Assert.Equal(ItemType.Ui, Item(index, "button").Type);
    }

    [Fact]
    public void Uses_forward_slash_relative_paths_rooted_at_the_source()
    {
        using var dir = FakeSource();
        var index = new RegistryGenerator().Generate(dir.Path);

        Assert.Contains(Item(index, "button").Files, f => f.Path == "Components/Button/BzButton.razor");
    }

    [Fact]
    public void Every_component_depends_on_utils()
    {
        using var dir = FakeSource();
        var index = new RegistryGenerator().Generate(dir.Path);

        Assert.Contains("utils", Item(index, "button").RegistryDependencies);
        Assert.Contains("utils", Item(index, "alert-dialog").RegistryDependencies);
    }

    [Fact]
    public void Infers_a_cross_family_dependency_from_a_bz_type_reference()
    {
        using var dir = FakeSource();
        var index = new RegistryGenerator().Generate(dir.Path);

        // AlertDialog's markup uses <BzButton>, so it depends on button.
        Assert.Contains("button", Item(index, "alert-dialog").RegistryDependencies);
        // ...but button does not depend on alert-dialog (no back-reference).
        Assert.DoesNotContain("alert-dialog", Item(index, "button").RegistryDependencies);
    }

    [Fact]
    public void Does_not_make_a_family_depend_on_itself()
    {
        using var dir = FakeSource();
        var index = new RegistryGenerator().Generate(dir.Path);

        Assert.DoesNotContain("button", Item(index, "button").RegistryDependencies);
    }

    [Theory]
    [InlineData("Button", "button")]
    [InlineData("AlertDialog", "alert-dialog")]
    [InlineData("TableOfContents", "table-of-contents")]
    [InlineData("InputOtp", "input-otp")]
    public void Kebab_casing(string pascal, string expected)
        => Assert.Equal(expected, RegistryGenerator.ToKebab(pascal));
}
