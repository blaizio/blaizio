using Blaizio.Cli.Commands;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Infrastructure;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// Tests over the real embedded resources — a broken LogicalName encoding or missing skin would
/// otherwise only surface at runtime.
/// </summary>
public class EmbeddedAssetTests
{
    [Fact]
    public void Css_assets_ship_every_documented_skin()
    {
        var assets = new EmbeddedCssAssets();
        string[] documented = ["ash", "aura", "ember", "flint", "forge", "glow", "spark", "wisp"];

        foreach (var skin in documented)
            Assert.Contains(skin, assets.AvailableSkins);
    }

    [Fact]
    public void Every_available_skin_resolves_content()
    {
        var assets = new EmbeddedCssAssets();
        foreach (var skin in assets.AvailableSkins)
            Assert.False(string.IsNullOrWhiteSpace(assets.GetSkinCss(skin)));
    }

    [Fact]
    public void Theme_base_and_animate_css_are_embedded()
    {
        var assets = new EmbeddedCssAssets();
        Assert.False(string.IsNullOrWhiteSpace(assets.GetThemeCss()));
        Assert.False(string.IsNullOrWhiteSpace(assets.GetBaseCss()));
        Assert.False(string.IsNullOrWhiteSpace(assets.GetAnimateCss()));
    }

    [Fact]
    public void Showcase_template_exists_and_decodes_to_real_paths()
    {
        var templates = new EmbeddedTemplates();
        Assert.True(templates.Has("showcase"));

        var files = templates.GetFiles("showcase");
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            // The __ and ~ encodings must be fully decoded away.
            Assert.DoesNotContain("__", file.RelativePath);
            Assert.DoesNotContain("~", file.RelativePath);
            Assert.DoesNotContain(".tmpl", file.RelativePath);
            Assert.False(string.IsNullOrWhiteSpace(file.Content));
        }

        // The scaffold must produce a runnable Blazor WASM app shell.
        Assert.Contains(files, f => f.RelativePath == "Program.cs");
        Assert.Contains(files, f => f.RelativePath.EndsWith("index.html", StringComparison.Ordinal));
    }

    [Fact]
    public void FilterItems_matches_name_title_and_description_case_insensitively()
    {
        RegistryItem[] items =
        [
            new() { Name = "button", Title = "Button", Description = "Clicky." },
            new() { Name = "card", Title = "Fancy Card", Description = "A panel." },
            new() { Name = "tabs" },
        ];

        Assert.Single(ListCommand.FilterItems(items, "BUTTON"));
        Assert.Single(ListCommand.FilterItems(items, "fancy"));
        Assert.Single(ListCommand.FilterItems(items, "panel"));
        Assert.Equal(3, ListCommand.FilterItems(items, null).Count());
        Assert.Empty(ListCommand.FilterItems(items, "nope"));
    }

    [Fact]
    public void Namespace_alias_rewrite_stops_at_the_terminator()
    {
        string[] args = ["add", "-ns", "My.Ns", "--", "-ns"];
        var normalized = CliApp.NormalizeNamespaceAlias(args);

        Assert.Equal("--namespace", normalized[1]);
        Assert.Equal("-ns", normalized[4]); // untouched past --
    }
}
