using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class TailwindEjectTests
{
    private static TailwindSetup Setup() => new(new FakeCssAssets());

    [Fact]
    public async Task Inlines_the_materialized_sheets_and_removes_the_imports()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        dir.Write(".blaizio/blaizio.css", "/* materialized contract */\n@custom-variant data-open (&[data-state=open]);\n");
        dir.Write(".blaizio/animate.css", "/* materialized animate */\n");

        var result = await Setup().EjectAsync(dir.Path);
        var css = dir.Read("Styles/app.css");

        Assert.True(result.Materialized);
        Assert.Equal("Styles/app.css", result.InputPath);
        Assert.DoesNotContain("@import \"../.blaizio/blaizio.css\";", css);
        Assert.DoesNotContain("@import \"../.blaizio/animate.css\";", css);
        Assert.Contains("/* materialized contract */", css);
        Assert.Contains("/* materialized animate */", css);
        // Animate first, contract second — the old import order.
        Assert.True(css.IndexOf("materialized animate", StringComparison.Ordinal)
            < css.IndexOf("materialized contract", StringComparison.Ordinal));
        // Everything else survives: the Tailwind import, the scans, the token block.
        Assert.Contains("@import \"tailwindcss\" source(none);", css);
        Assert.Contains("@source \"../Components/Ui/**/*.razor\";", css);
        Assert.Contains("@theme inline", css);
    }

    [Fact]
    public async Task Falls_back_to_the_embedded_sheets_when_the_project_was_never_built()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");

        var result = await Setup().EjectAsync(dir.Path);
        var css = dir.Read("Styles/app.css");

        Assert.False(result.Materialized);
        Assert.Contains("/* base */", css);    // FakeCssAssets.GetBaseCss
        Assert.Contains("/* animate */", css); // FakeCssAssets.GetAnimateCss
        Assert.DoesNotContain(".blaizio/blaizio.css\";", css);
    }

    [Fact]
    public async Task Never_mixes_sources_when_only_one_sheet_is_materialized()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        dir.Write(".blaizio/blaizio.css", "/* materialized contract */\n");
        // animate.css missing: half a materialization could pair sheets from different versions.

        var result = await Setup().EjectAsync(dir.Path);
        var css = dir.Read("Styles/app.css");

        Assert.False(result.Materialized);
        Assert.Contains("/* base */", css);
        Assert.DoesNotContain("/* materialized contract */", css);
    }

    [Fact]
    public async Task Ejecting_twice_throws_nothing_to_eject()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        await Setup().EjectAsync(dir.Path);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Setup().EjectAsync(dir.Path));
        Assert.Contains("nothing to eject", ex.Message);
    }

    [Fact]
    public async Task Ejecting_an_uninitialized_project_throws()
    {
        using var dir = new TempDir();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Setup().EjectAsync(dir.Path));
    }

    [Fact]
    public async Task Ejects_a_bundler_recorded_input_in_place()
    {
        using var dir = new TempDir();
        dir.Write("assets/main.css", "@import \"tailwindcss\";\n/* user content */\n");
        await Setup().EnsureAsync(dir.Path, "Components/Ui", cssInput: "assets/main.css");

        var result = await Setup().EjectAsync(dir.Path, cssInput: "assets/main.css");
        var css = dir.Read("assets/main.css");

        Assert.Equal("assets/main.css", result.InputPath);
        Assert.Contains("/* user content */", css);
        Assert.Contains("/* base */", css);
        Assert.DoesNotContain(".blaizio/blaizio.css\";", css);
        Assert.DoesNotContain(".blaizio/animate.css\";", css);
    }

    [Fact]
    public async Task EnsureAsync_leaves_an_ejected_tokens_file_alone()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        await Setup().EjectAsync(dir.Path);
        var ejected = dir.Read("Styles/app.css");

        // A re-run (init top-up, update's bundler sync) must not resurrect the imports.
        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui", ejected: true);

        Assert.True(result.Ejected);
        Assert.False(result.InputCreated);
        Assert.Equal(ejected, dir.Read("Styles/app.css"));
    }
}
