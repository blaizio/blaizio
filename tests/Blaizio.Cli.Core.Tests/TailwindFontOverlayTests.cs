using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class TailwindFontOverlayTests
{
    private static TailwindSetup Setup() => new(new FakeCssAssets());

    // A minimal managed input, as init would have written it.
    private const string ManagedInput =
        "/* blaizio:managed */\n@import \"tailwindcss\" source(none);\n@import \"./blaizio/theme.css\";\n";

    [Fact]
    public async Task Writes_fonts_overlay_and_wires_the_import()
    {
        using var dir = new TempDir();
        dir.Write("Styles/app.css", ManagedInput);

        var result = await TailwindSetup.EnsureFontsAsync(dir.Path, "classic", "code");

        Assert.True(result.HadSelection);
        Assert.True(result.ImportWired);
        Assert.Equal("Styles/blaizio/fonts.css", result.Path);

        var fonts = dir.Read("Styles/blaizio/fonts.css");
        Assert.Contains("--font-heading: Georgia", fonts);
        Assert.Contains("font-family: ui-monospace", fonts);

        Assert.Contains("@import \"./blaizio/fonts.css\";", dir.Read("Styles/app.css"));
    }

    [Fact]
    public async Task Default_selection_writes_nothing()
    {
        using var dir = new TempDir();
        dir.Write("Styles/app.css", ManagedInput);

        var result = await TailwindSetup.EnsureFontsAsync(dir.Path, "default", "default");

        Assert.False(result.HadSelection);
        Assert.False(result.ImportWired);
        Assert.Null(result.Path);
        Assert.False(dir.Exists("Styles/blaizio/fonts.css"));
        Assert.DoesNotContain("fonts.css", dir.Read("Styles/app.css"));
    }

    [Fact]
    public async Task Without_an_input_the_overlay_is_still_written_but_not_wired()
    {
        using var dir = new TempDir();

        var result = await TailwindSetup.EnsureFontsAsync(dir.Path, "classic", "code");

        Assert.True(result.HadSelection);
        Assert.False(result.ImportWired);
        Assert.True(dir.Exists("Styles/blaizio/fonts.css"));
        Assert.False(dir.Exists("Styles/app.css"));
    }
}
