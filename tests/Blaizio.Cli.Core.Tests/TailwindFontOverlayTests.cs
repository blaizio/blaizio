using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class TailwindFontOverlayTests
{
    private static TailwindSetup Setup() => new(new FakeCssAssets());

    [Fact]
    public async Task Patches_the_selection_into_the_tokens_file()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");

        var result = await TailwindSetup.EnsureFontsAsync(dir.Path, "classic", "code");

        Assert.True(result.HadSelection);
        Assert.True(result.Patched);
        Assert.Equal("Styles/app.css", result.Path);

        var css = dir.Read("Styles/app.css");
        Assert.Contains("--font-heading: Georgia", css);
        Assert.Contains("font-family: ui-monospace", css);
        // The @theme inline map entry keeps its var() indirection - only :root gets the stack.
        Assert.Contains("--font-heading: var(--font-heading);", css);
    }

    [Fact]
    public async Task Switching_a_half_back_to_default_resets_it()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        await TailwindSetup.EnsureFontsAsync(dir.Path, "classic", "code");

        // The body half goes back to default (e.g. `add font-heading-*` recorded pair) - the
        // html rule disappears; the heading keeps its stack.
        var result = await TailwindSetup.EnsureFontsAsync(dir.Path, "classic", "default");
        var css = dir.Read("Styles/app.css");

        Assert.True(result.Patched);
        Assert.Contains("--font-heading: Georgia", css);
        Assert.DoesNotContain("font-family: ui-monospace", css);

        // And the heading too: back to the built-in default stack.
        await TailwindSetup.EnsureFontsAsync(dir.Path, "default", "code");
        css = dir.Read("Styles/app.css");
        Assert.DoesNotContain("Georgia", css);
        Assert.Contains("--font-heading: var(--font-sans", css);
    }

    [Fact]
    public async Task Default_selection_writes_nothing()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        var before = dir.Read("Styles/app.css");

        var result = await TailwindSetup.EnsureFontsAsync(dir.Path, "default", "default");

        Assert.False(result.HadSelection);
        Assert.False(result.Patched);
        Assert.Null(result.Path);
        Assert.Equal(before, dir.Read("Styles/app.css"));
    }

    [Fact]
    public async Task Without_a_tokens_file_nothing_is_patched()
    {
        using var dir = new TempDir();

        var result = await TailwindSetup.EnsureFontsAsync(dir.Path, "classic", "code");

        Assert.True(result.HadSelection);
        Assert.False(result.Patched);
        Assert.False(dir.Exists("Styles/app.css"));
    }
}
