using Blaizio.Cli.Core.Templates;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class TemplateScaffolderTests
{
    private static readonly TemplateTokens Tokens =
        new(RootNamespace: "MyShop", ComponentNamespace: "MyShop.Components.Ui", ProjectName: "MyShop", Style: "ember");

    [Fact]
    public async Task Writes_files_to_their_relative_paths_with_tokens_substituted()
    {
        using var dir = new TempDir();
        var provider = new FakeTemplateProvider(
            new TemplateFile("Program.cs", "using {{RootNamespace}};"),
            new TemplateFile("Pages/Home.razor", "<h1>{{ProjectName}}</h1><App>@using {{ComponentNamespace}}</App>"),
            new TemplateFile("wwwroot/index.html", "<html class=\"style-{{Style}}\"></html>"));

        var result = await new TemplateScaffolder(provider).ScaffoldAsync(dir.Path, "showcase", Tokens);

        Assert.Equal(3, result.Written.Count);
        Assert.Equal("using MyShop;", dir.Read("Program.cs"));
        Assert.Equal("<h1>MyShop</h1><App>@using MyShop.Components.Ui</App>", dir.Read("Pages/Home.razor"));
        Assert.Equal("<html class=\"style-ember\"></html>", dir.Read("wwwroot/index.html"));
    }

    [Fact]
    public async Task The_pre_rename_skin_token_still_substitutes_the_style()
    {
        using var dir = new TempDir();
        var provider = new FakeTemplateProvider(
            new TemplateFile("wwwroot/index.html", "<html class=\"style-{{Skin}}\" data-s=\"{{Style}}\"></html>"));

        await new TemplateScaffolder(provider).ScaffoldAsync(dir.Path, "showcase", Tokens);

        Assert.Equal("<html class=\"style-ember\" data-s=\"ember\"></html>", dir.Read("wwwroot/index.html"));
    }

#pragma warning disable CS0618 // the obsolete Skin alias is what this test pins
    [Fact]
    public void The_obsolete_skin_property_aliases_style()
    {
        Assert.Equal("ember", Tokens.Skin);
        Assert.Equal("spark", (Tokens with { Skin = "spark" }).Style);
    }
#pragma warning restore CS0618

    [Fact]
    public async Task Skips_existing_files_by_default()
    {
        using var dir = new TempDir();
        dir.Write("Program.cs", "USER CODE");
        var provider = new FakeTemplateProvider(new TemplateFile("Program.cs", "using {{RootNamespace}};"));

        var result = await new TemplateScaffolder(provider).ScaffoldAsync(dir.Path, "showcase", Tokens);

        Assert.Contains("Program.cs", result.Skipped);
        Assert.Empty(result.Written);
        Assert.Equal("USER CODE", dir.Read("Program.cs"));
    }

    [Fact]
    public async Task Overwrites_when_requested()
    {
        using var dir = new TempDir();
        dir.Write("Program.cs", "USER CODE");
        var provider = new FakeTemplateProvider(new TemplateFile("Program.cs", "using {{RootNamespace}};"));

        await new TemplateScaffolder(provider).ScaffoldAsync(dir.Path, "showcase", Tokens, overwrite: true);

        Assert.Equal("using MyShop;", dir.Read("Program.cs"));
    }
}
