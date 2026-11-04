using System.Text.RegularExpressions;
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
        // App-wide DI glue: excluded from utils by default (references component services).
        dir.Write("ServiceCollectionExtensions.cs", "namespace Blaizio.Ui; static class Svc {}");
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
    public void Excludes_the_di_registration_from_utils()
    {
        using var dir = FakeSource();
        var index = new RegistryGenerator().Generate(dir.Path);

        Assert.DoesNotContain(Item(index, "utils").Files, f => f.Path == "ServiceCollectionExtensions.cs");
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

    [Fact]
    public void Family_min_base_lands_on_its_item_and_nowhere_else()
    {
        using var dir = FakeSource();
        var options = new GeneratorOptions
        {
            FamilyMinBase = new Dictionary<string, string> { ["button"] = "0.1.0-alpha.24" },
        };
        var index = new RegistryGenerator(options).Generate(dir.Path);

        Assert.Equal("0.1.0-alpha.24", Item(index, "button").MinBase);
        Assert.Null(Item(index, "alert-dialog").MinBase);
        Assert.Null(Item(index, "utils").MinBase);
    }

    [Fact]
    public void The_code_block_family_brings_the_syntax_package_and_no_other_family_does()
    {
        using var dir = FakeSource();
        dir.Write("Components/CodeBlock/BzCodeBlock.razor", "<pre>@ChildContent</pre>");
        var index = new RegistryGenerator().Generate(dir.Path);

        Assert.Equal(["Blaizio.Base", "Blaizio.Icons", "Blaizio.Icons.Tabler", "TailwindMerge.NET", "Blaizio.Syntax"], Item(index, "code-block").NugetDependencies);
        Assert.DoesNotContain("Blaizio.Syntax", Item(index, "button").NugetDependencies);
        Assert.DoesNotContain("Blaizio.Syntax", Item(index, "utils").NugetDependencies);
    }

    /// <summary>
    /// Over the real Blaizio.Ui tree: a family whose code calls the highlighter (a type from
    /// Blaizio.Syntax) must bring that package, or every app installing it fails to compile. The
    /// scan cannot see package ids, so FamilyNuget carries it - this catches the next family that
    /// starts highlighting without an entry there.
    /// </summary>
    [Fact]
    public void Every_family_that_highlights_brings_the_syntax_package()
    {
        var source = Path.Combine(RepoRoot(), "src", "Blaizio.Ui");
        var index = new RegistryGenerator().Generate(source);

        var highlighting = index.Items
            .Where(i => i.Files.Any(f => SyntaxApi.IsMatch(CommentRx.Replace(File.ReadAllText(Path.Combine(source, f.Path)), " "))))
            .Select(i => i.Name)
            .ToList();

        Assert.Contains("code-block", highlighting);
        Assert.All(index.Items, i => Assert.True(
            highlighting.Contains(i.Name) == i.NugetDependencies.Contains("Blaizio.Syntax"),
            $"{i.Name}: highlights = {highlighting.Contains(i.Name)}, declares Blaizio.Syntax = {i.NugetDependencies.Contains("Blaizio.Syntax")}"));
    }

    private static readonly Regex SyntaxApi = new(@"\bSyntax(?:Text|Document|Line|Run|Kind|Options|Language|Grammar|Writer)\b");

    // The comment forms the generator strips before it looks for references.
    private static readonly Regex CommentRx = new(@"@\*.*?\*@|/\*.*?\*/|<!--.*?-->|(?<!:)//[^\r\n]*", RegexOptions.Singleline);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Directory.Build.props")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    // A third-party tree: no root helpers, one component that leans on official ones.
    private static TempDir ThirdPartySource()
    {
        var dir = new TempDir();
        dir.Write("Components/Rating/BzRating.razor",
            "<BzButton><BzIcon /></BzButton> @* like BzToggle *@");
        return dir;
    }

    [Fact]
    public void Without_root_helpers_components_depend_on_the_official_utils()
    {
        using var dir = ThirdPartySource();
        var index = new RegistryGenerator().Generate(dir.Path);

        Assert.DoesNotContain(index.Items, i => i.Name == "utils");
        Assert.Equal(["@default/utils"], Item(index, "rating").RegistryDependencies);
    }

    [Fact]
    public void Fonts_are_emitted_only_on_request()
    {
        using var dir = ThirdPartySource();

        var plain = new RegistryGenerator().Generate(dir.Path);
        Assert.DoesNotContain(plain.Items, i => i.Type == ItemType.Font);

        var withFonts = new RegistryGenerator(new GeneratorOptions { IncludeFonts = true }).Generate(dir.Path);
        Assert.Contains(withFonts.Items, i => i.Name == "font-inter");
        Assert.Contains(withFonts.Items, i => i.Name == "font-heading-inter");
    }

    [Fact]
    public void Reports_bz_references_outside_the_tree_without_guessing_a_dependency()
    {
        using var dir = ThirdPartySource();
        var generator = new RegistryGenerator();
        var index = generator.Generate(dir.Path);

        // BzButton is unknown here; BzIcon is the Icons package; BzToggle sits in a comment.
        Assert.Equal(["BzButton"], generator.UnresolvedReferences);
        Assert.DoesNotContain(Item(index, "rating").RegistryDependencies, d => d.Contains("button"));
    }

    [Fact]
    public void References_resolved_inside_the_tree_are_not_reported()
    {
        using var dir = FakeSource();
        var generator = new RegistryGenerator();
        generator.Generate(dir.Path);

        Assert.Empty(generator.UnresolvedReferences);
    }
}
