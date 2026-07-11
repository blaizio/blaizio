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
        Assert.True(dir.Exists("Styles/blaizio/shared.css"));
        Assert.True(dir.Exists("Styles/blaizio/style-ember.css"));
    }

    [Fact]
    public async Task Input_imports_the_managed_files_and_scans_the_output_dir()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember");
        var css = dir.Read("Styles/app.css");

        // source(none) turns off auto-detection so the scanner never walks bin/obj binaries.
        Assert.Contains("@import \"tailwindcss\" source(none);", css);
        Assert.Contains("@import \"./blaizio/animate.css\";", css);
        // The shared skin layer precedes the skin so the skin's scoped rules override it.
        Assert.Contains("@import \"./blaizio/shared.css\" layer(components);", css);
        Assert.Contains("@import \"./blaizio/style-ember.css\" layer(components);", css);
        // @source is relative to Styles/, so it climbs out to the component dir.
        Assert.Contains("@source \"../Components/Ui/**/*.razor\";", css);
        // App markup (pages, layouts) is scanned too, since auto-detection is off.
        Assert.Contains("@source \"../**/*.razor\";", css);
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
    public async Task Bundler_mode_syncs_the_recorded_input_and_never_writes_its_own()
    {
        using var dir = new TempDir();
        // A rollup-owned input at the project root with a stale skin mirror and user content.
        dir.Write("tailwind.css",
            "@import \"tailwindcss\";\n" +
            "@import \"./Styles/blaizio/theme.css\";\n" +
            "@import \"./Styles/blaizio/style-ember.css\" layer(components);\n" +
            ".hero { color: red; }\n");

        var result = await Setup().EnsureAsync(
            dir.Path, "Components/Ui", "ash", preset: "nebula", cssInput: "tailwind.css");
        var css = dir.Read("tailwind.css");

        Assert.Equal("tailwind.css", result.InputPath);
        Assert.False(dir.Exists("Styles/app.css"));                       // no parallel CLI input
        Assert.Contains(".hero { color: red; }", css);                    // user content preserved
        Assert.Contains("@import \"./Styles/blaizio/preset-nebula.css\";", css);
        Assert.Contains("@import \"./Styles/blaizio/style-ash.css\" layer(components);", css);
        Assert.DoesNotContain("style-ember.css", css);                    // stale skin line swapped
        Assert.DoesNotContain("source(none)", css);                       // the bundler owns scanning
        Assert.DoesNotContain("@source \"../**/*.razor\";", css);
        Assert.Contains("@source \"Components/Ui/**/*.razor\";", css);    // component utilities still scanned
        // The one tailwind import the bundler input already had is not duplicated.
        Assert.Equal(1, css.Split("@import \"tailwindcss\"").Length - 1);
    }

    [Fact]
    public async Task Bundler_mode_paths_are_relative_to_the_input_location()
    {
        using var dir = new TempDir();
        dir.Write("Styles/tailwind.css", "@import \"tailwindcss\";\n");

        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", cssInput: "Styles/tailwind.css");
        var css = dir.Read("Styles/tailwind.css");

        Assert.Contains("@import \"./blaizio/theme.css\";", css);
        Assert.Contains("@source \"../Components/Ui/**/*.razor\";", css);
    }

    [Fact]
    public async Task Bundler_mode_requires_the_recorded_input_to_exist()
    {
        using var dir = new TempDir();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", cssInput: "missing/tailwind.css"));
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
    public async Task Skips_the_top_up_when_told_a_user_input_is_not_ours_to_touch()
    {
        // update passes topUpUserInput: false - a user-authored input must come back byte-identical.
        using var dir = new TempDir();
        var original = "@import \"tailwindcss\";\n.hero { color: red; }\n";
        dir.Write("Styles/app.css", original);

        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", topUpUserInput: false);

        Assert.Equal(original, dir.Read("Styles/app.css"));
    }

    [Fact]
    public async Task HasCustomInput_spots_a_project_running_its_own_pipeline()
    {
        // No input yet: not custom (init/update may create one).
        using var dir = new TempDir();
        Assert.False(TailwindSetup.HasCustomInput(dir.Path));

        // A managed input (or one importing the managed assets) is ours.
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember");
        Assert.False(TailwindSetup.HasCustomInput(dir.Path));

        // A user-authored input with its own imports is a custom pipeline - hands off.
        dir.Write("Styles/app.css", "@import \"tailwindcss\";\n@import \"../vendor/skins.css\";\n");
        Assert.True(TailwindSetup.HasCustomInput(dir.Path));

        // ...unless it still pulls the managed assets - then refreshing them stays useful.
        dir.Write("Styles/app.css", "@import \"tailwindcss\";\n@import \"./blaizio/theme.css\";\n");
        Assert.False(TailwindSetup.HasCustomInput(dir.Path));
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

    [Fact]
    public async Task Writes_the_preset_and_imports_it_right_after_the_theme()
    {
        using var dir = new TempDir();
        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", preset: "comet");

        Assert.Equal("comet", result.Preset);
        Assert.True(dir.Exists("Styles/blaizio/preset-comet.css"));
        var css = dir.Read("Styles/app.css");
        // Order matters: the preset must follow theme.css so it wins the :root tie by source order.
        var theme = css.IndexOf("@import \"./blaizio/theme.css\";", StringComparison.Ordinal);
        var preset = css.IndexOf("@import \"./blaizio/preset-comet.css\";", StringComparison.Ordinal);
        var baseCss = css.IndexOf("@import \"./blaizio/base.css\";", StringComparison.Ordinal);
        Assert.True(theme >= 0 && preset > theme && baseCss > preset);
    }

    [Fact]
    public async Task Nova_writes_no_preset_file()
    {
        using var dir = new TempDir();
        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", preset: "nova");

        Assert.Equal("nova", result.Preset);
        Assert.False(dir.Exists("Styles/blaizio/preset-nova.css"));
        Assert.DoesNotContain("preset-", dir.Read("Styles/app.css"));
    }

    [Fact]
    public async Task Changing_the_preset_prunes_the_previous_one()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", preset: "comet");
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", preset: "nebula");

        Assert.True(dir.Exists("Styles/blaizio/preset-nebula.css"));
        Assert.False(dir.Exists("Styles/blaizio/preset-comet.css"));
        var css = dir.Read("Styles/app.css");
        Assert.Contains("preset-nebula.css", css);
        Assert.DoesNotContain("preset-comet.css", css);
    }

    [Fact]
    public async Task Switching_back_to_nova_removes_the_preset_file_and_import()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", preset: "comet");
        await Setup().EnsureAsync(dir.Path, "Components/Ui", "ember", preset: "nova");

        Assert.False(dir.Exists("Styles/blaizio/preset-comet.css"));
        Assert.DoesNotContain("preset-", dir.Read("Styles/app.css"));
    }
}
