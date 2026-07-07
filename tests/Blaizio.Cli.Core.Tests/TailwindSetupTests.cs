using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class TailwindSetupTests
{
    private static TailwindSetup Setup() => new(new FakeCssAssets());

    [Fact]
    public async Task Writes_managed_assets_and_a_new_input()
    {
        using var dir = new TempDir();
        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember");

        Assert.True(result.InputCreated);
        Assert.True(dir.Exists("Styles/app.css"));
        Assert.True(dir.Exists("Styles/blaizio/theme.css"));
        Assert.True(dir.Exists("Styles/blaizio/animate.css"));
        Assert.True(dir.Exists("Styles/blaizio/base.css"));
        Assert.True(dir.Exists("Styles/blaizio/style-ember.css"));
    }

    [Fact]
    public async Task Input_imports_the_managed_files_and_scans_the_output_dir()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember");
        var css = dir.Read("Styles/app.css");

        Assert.Contains("@import \"tailwindcss\";", css);
        Assert.Contains("@import \"./blaizio/animate.css\";", css);
        Assert.Contains("@import \"./blaizio/style-ember.css\" layer(components);", css);
        // @source is relative to Styles/, so it climbs out to the component dir.
        Assert.Contains("@source \"../Components/Ui/**/*.razor\";", css);
    }

    [Fact]
    public async Task Regenerates_a_managed_input_and_prunes_the_previous_skin()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember");
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "spark");

        Assert.True(dir.Exists("Styles/blaizio/style-spark.css"));
        Assert.False(dir.Exists("Styles/blaizio/style-ember.css"));
        var css = dir.Read("Styles/app.css");
        Assert.Contains("style-spark.css", css);
        Assert.DoesNotContain("style-ember.css", css);
    }

    [Fact]
    public async Task Tops_up_a_user_authored_input_without_clobbering_it()
    {
        using var dir = new TempDir();
        dir.Write("Styles/app.css", "@import \"tailwindcss\";\n.hero { color: red; }\n");

        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember");
        var css = dir.Read("Styles/app.css");

        Assert.False(result.InputCreated);
        Assert.Contains(".hero { color: red; }", css);          // user content preserved
        Assert.Contains("@import \"./blaizio/base.css\";", css); // missing directive appended
        // The one tailwind import the user already had is not duplicated.
        Assert.Equal(1, css.Split("@import \"tailwindcss\";").Length - 1);
    }

    [Fact]
    public async Task Writes_options_css_when_pointer_enabled_and_imports_it()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", new TailwindOptions(Pointer: true));

        Assert.True(dir.Exists("Styles/blaizio/options.css"));
        Assert.Contains("cursor: pointer", dir.Read("Styles/blaizio/options.css"));
        Assert.Contains("@import \"./blaizio/options.css\";", dir.Read("Styles/app.css"));
    }

    [Fact]
    public async Task Removes_options_css_when_toggled_off()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", new TailwindOptions(Pointer: true));
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", new TailwindOptions(Pointer: false));

        Assert.False(dir.Exists("Styles/blaizio/options.css"));
        Assert.DoesNotContain("options.css", dir.Read("Styles/app.css"));
    }
}
