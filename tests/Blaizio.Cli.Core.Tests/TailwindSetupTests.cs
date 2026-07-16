using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class TailwindSetupTests
{
    private static TailwindSetup Setup() => new(new FakeCssAssets());

    [Fact]
    public async Task Creates_the_tokens_file_and_nothing_else()
    {
        using var dir = new TempDir();
        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui");

        Assert.True(result.InputCreated);
        Assert.False(result.LegacyV1);
        Assert.True(dir.Exists("Styles/app.css"));
        // v3: no managed CSS directory - the contract materializes into .blaizio/ at build.
        Assert.False(Directory.Exists(dir.Combine("Styles", "blaizio")));
    }

    [Fact]
    public async Task Tokens_file_imports_the_contract_and_scans_the_output_dir()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        var css = dir.Read("Styles/app.css");

        // source(none) turns off auto-detection so the scanner never walks bin/obj binaries.
        Assert.Contains("@import \"tailwindcss\" source(none);", css);
        // The sheets Blaizio.Base materializes into .blaizio/ at build.
        Assert.Contains("@import \"../.blaizio/animate.css\";", css);
        Assert.Contains("@import \"../.blaizio/blaizio.css\";", css);
        // @source is relative to Styles/, so it climbs out to the component dir.
        Assert.Contains("@source \"../Components/Ui/**/*.razor\";", css);
        Assert.Contains("@source \"../Components/Ui/**/*.cs\";", css);
        // App markup (pages, layouts) is scanned too, since auto-detection is off.
        Assert.Contains("@source \"../**/*.razor\";", css);
    }

    [Fact]
    public async Task Tokens_file_carries_the_theme_values_comment_free()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        var css = dir.Read("Styles/app.css");

        Assert.Contains("@custom-variant dark", css);
        Assert.Contains("@theme inline", css);
        Assert.Contains("--radius: 0.75rem;", css);
        Assert.Contains("--primary: oklch(0.55 0.22 304);", css);
        Assert.Contains("--background: oklch(0.17 0 0);", css); // .dark values present too
        Assert.Contains("@layer base", css);
        // Maintainer comments are stripped from the token block (the values are the user's now);
        // only the short scaffold header at the top remains.
        Assert.DoesNotContain("maintainer comment", css);
    }

    [Fact]
    public async Task Gitignores_the_contract_dir()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        Assert.Contains(".blaizio/", dir.Read(".gitignore"));

        // Idempotent, and an existing .gitignore is appended, not clobbered.
        dir.Write(".gitignore", "bin/\nobj/\n");
        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        var gitignore = dir.Read(".gitignore");
        Assert.Contains("bin/", gitignore);
        Assert.Equal(1, gitignore.Split(".blaizio/").Length - 1);
    }

    [Fact]
    public async Task Merges_the_preset_palette_into_root_and_dark()
    {
        using var dir = new TempDir();
        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui", preset: "comet");
        var css = dir.Read("Styles/app.css");

        Assert.Equal("comet", result.Preset);
        // No preset file, no preset import - the values ARE the file.
        Assert.DoesNotContain("preset-", css);
        Assert.Contains("--primary: oklch(0.5 0.11 195);", css);       // :root, comet
        Assert.Contains("--primary: oklch(0.61 0.1 195);", css);       // .dark, comet
        Assert.Contains("--background: oklch(0.176 0.015 215);", css); // .dark, comet
        Assert.Contains("--radius: 0.75rem;", css);                    // not preset-shaped: base value
        Assert.DoesNotContain("oklch(0.55 0.22 304)", css);            // nova primary fully replaced
    }

    [Fact]
    public async Task Bakes_chart_and_radius_into_root_only()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui", chart: "ocean", radius: "lg");
        var css = dir.Read("Styles/app.css");

        Assert.Contains("--radius: 1.05rem;", css);
        Assert.Contains("--chart-1: oklch(0.6 0.17 245);", css);
        // The block-scoped patch must not rewrite the @theme inline lookalikes.
        Assert.Contains("--radius-lg: var(--radius);", css);
    }

    [Fact]
    public async Task Pointer_flag_adds_the_cursor_rule_to_the_base_layer()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui", new TailwindOptions(Pointer: true));

        Assert.Contains("cursor: pointer", dir.Read("Styles/app.css"));
    }

    [Fact]
    public async Task Bundler_mode_syncs_the_recorded_input_and_never_writes_its_own()
    {
        using var dir = new TempDir();
        // A rollup-owned input at the project root with user content.
        dir.Write("tailwind.css",
            "@import \"tailwindcss\";\n" +
            ".hero { color: red; }\n");

        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui", cssInput: "tailwind.css");
        var css = dir.Read("tailwind.css");

        Assert.Equal("tailwind.css", result.InputPath);
        Assert.False(dir.Exists("Styles/app.css"));                    // no parallel CLI input
        Assert.Contains(".hero { color: red; }", css);                 // user content preserved
        Assert.Contains("@import \"./.blaizio/blaizio.css\";", css);   // contract wired
        Assert.DoesNotContain("source(none)", css);                    // the bundler owns scanning
        Assert.DoesNotContain("@source \"../**/*.razor\";", css);
        Assert.Contains("@source \"Components/Ui/**/*.razor\";", css); // component utilities still scanned
        // The one tailwind import the bundler input already had is not duplicated.
        Assert.Equal(1, css.Split("@import \"tailwindcss\"").Length - 1);
        // New @imports must land before other rules - a trailing @import is dead code in CSS.
        Assert.True(css.IndexOf(".blaizio/blaizio.css", StringComparison.Ordinal)
                    < css.IndexOf(".hero", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Bundler_mode_reports_a_v1_input_and_leaves_it_untouched()
    {
        // Swapping the imports to the v3 contract while the components still carry bz-* classes
        // would break the app - the v1 -> v3 migration in `update` is what moves it forward.
        using var dir = new TempDir();
        var original =
            "@import \"tailwindcss\";\n" +
            "@import \"./Styles/blaizio/theme.css\";\n" +
            ".hero { color: red; }\n";
        dir.Write("tailwind.css", original);

        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui", cssInput: "tailwind.css");

        Assert.True(result.LegacyV1);
        Assert.Equal(original, dir.Read("tailwind.css"));
    }

    [Fact]
    public async Task Bundler_mode_injects_the_token_block_when_absent()
    {
        using var dir = new TempDir();
        dir.Write("tailwind.css", "@import \"tailwindcss\";\n.hero { color: red; }\n");

        await Setup().EnsureAsync(dir.Path, "Components/Ui", preset: "comet", cssInput: "tailwind.css");
        var css = dir.Read("tailwind.css");

        Assert.Contains("@theme inline", css);
        Assert.Contains("--primary: oklch(0.5 0.11 195);", css); // preset merged into the block

        // A second run must not append the block again.
        await Setup().EnsureAsync(dir.Path, "Components/Ui", preset: "comet", cssInput: "tailwind.css");
        Assert.Equal(1, dir.Read("tailwind.css").Split("@theme inline").Length - 1);
    }

    [Fact]
    public async Task Bundler_mode_paths_are_relative_to_the_input_location()
    {
        using var dir = new TempDir();
        dir.Write("Styles/tailwind.css", "@import \"tailwindcss\";\n");

        await Setup().EnsureAsync(dir.Path, "Components/Ui", cssInput: "Styles/tailwind.css");
        var css = dir.Read("Styles/tailwind.css");

        Assert.Contains("@import \"../.blaizio/blaizio.css\";", css);
        Assert.Contains("@source \"../Components/Ui/**/*.razor\";", css);
    }

    [Fact]
    public async Task Bundler_mode_requires_the_recorded_input_to_exist()
    {
        using var dir = new TempDir();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Setup().EnsureAsync(dir.Path, "Components/Ui", cssInput: "missing/tailwind.css"));
    }

    [Fact]
    public async Task Tops_up_a_user_authored_input_without_clobbering_it()
    {
        using var dir = new TempDir();
        dir.Write("Styles/app.css", "@import \"tailwindcss\";\n.hero { color: red; }\n");

        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui");
        var css = dir.Read("Styles/app.css");

        Assert.False(result.InputCreated);
        Assert.Contains(".hero { color: red; }", css);                // user content preserved
        Assert.Contains("@import \"../.blaizio/blaizio.css\";", css); // missing directive appended
        Assert.Contains("@theme inline", css);                        // token block injected
    }

    [Fact]
    public async Task Skips_the_top_up_when_told_a_user_input_is_not_ours_to_touch()
    {
        // update passes topUpUserInput: false - a user-authored input must come back byte-identical.
        using var dir = new TempDir();
        var original = "@import \"tailwindcss\";\n.hero { color: red; }\n";
        dir.Write("Styles/app.css", original);

        await Setup().EnsureAsync(dir.Path, "Components/Ui", topUpUserInput: false);

        Assert.Equal(original, dir.Read("Styles/app.css"));
    }

    [Fact]
    public async Task Regenerates_a_v1_marker_file_with_no_v1_imports_left()
    {
        using var dir = new TempDir();
        dir.Write("Styles/app.css", "/* blaizio:managed */\n@import \"tailwindcss\" source(none);\n");

        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui");

        Assert.False(result.InputCreated);
        var css = dir.Read("Styles/app.css");
        Assert.Contains("@import \"../.blaizio/blaizio.css\";", css);
        Assert.Contains("@theme inline", css);
    }

    [Fact]
    public async Task A_v1_layout_is_reported_and_left_untouched()
    {
        using var dir = new TempDir();
        var original =
            "/* blaizio:managed */\n@import \"tailwindcss\" source(none);\n" +
            "@import \"./blaizio/theme.css\";\n@import \"./blaizio/style-ember.css\" layer(components);\n";
        dir.Write("Styles/app.css", original);
        dir.Write("Styles/blaizio/theme.css", ":root { --radius: 0.75rem; }\n");

        var result = await Setup().EnsureAsync(dir.Path, "Components/Ui");

        Assert.True(result.LegacyV1);
        Assert.Equal(original, dir.Read("Styles/app.css"));
        Assert.True(TailwindSetup.IsLegacyV1(dir.Path));
    }

    [Fact]
    public async Task HasCustomInput_spots_a_project_running_its_own_pipeline()
    {
        // No input yet: not custom (init/update may create one).
        using var dir = new TempDir();
        Assert.False(TailwindSetup.HasCustomInput(dir.Path));

        // The v3 tokens file references the contract - ours.
        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        Assert.False(TailwindSetup.HasCustomInput(dir.Path));

        // A user-authored input with its own imports is a custom pipeline - hands off.
        dir.Write("Styles/app.css", "@import \"tailwindcss\";\n@import \"../vendor/skins.css\";\n");
        Assert.True(TailwindSetup.HasCustomInput(dir.Path));
    }

    [Fact]
    public async Task HasContractImport_is_the_initialized_marker()
    {
        using var dir = new TempDir();
        Assert.False(TailwindSetup.HasContractImport(dir.Path));

        await Setup().EnsureAsync(dir.Path, "Components/Ui");
        Assert.True(TailwindSetup.HasContractImport(dir.Path));

        dir.Write("custom.css", "@import \"tailwindcss\";\n");
        Assert.False(TailwindSetup.HasContractImport(dir.Path, "custom.css"));
    }

    [Fact]
    public async Task ApplyPreset_patches_every_theme_value_in_place()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui", preset: "comet");
        // The user re-themed by hand and added their own token; a preset apply must overwrite the
        // standard tokens and keep theirs.
        var css = dir.Read("Styles/app.css")
            .Replace("--primary: oklch(0.5 0.11 195);", "--primary: red;\n  --brand: hotpink;");
        dir.Write("Styles/app.css", css);

        var result = await Setup().ApplyPresetAsync(dir.Path, "nebula");
        css = dir.Read("Styles/app.css");

        Assert.True(result.Patched);
        Assert.Contains("--primary: oklch(0.55 0.18 275);", css);       // :root, nebula
        Assert.Contains("--primary: oklch(0.62 0.16 275);", css);       // .dark, nebula
        Assert.Contains("--background: oklch(1 0 0);", css);            // nebula defines none: base value restored
        Assert.Contains("--brand: hotpink;", css);                      // user's own token survives
    }

    [Fact]
    public async Task ApplyPreset_keeps_the_font_selection_and_reapplies_overlays()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui", chart: "ocean", radius: "lg");
        await TailwindSetup.EnsureFontsAsync(dir.Path, "classic", "default");

        await Setup().ApplyPresetAsync(dir.Path, "comet", chart: "ocean", radius: "lg");
        var css = dir.Read("Styles/app.css");

        Assert.Contains("--font-heading: Georgia", css);          // fonts are their own selection
        Assert.Contains("--radius: 1.05rem;", css);               // recorded radius survives the re-theme
        Assert.Contains("--chart-1: oklch(0.6 0.17 245);", css);  // recorded chart survives too
    }

    [Fact]
    public async Task ApplyPreset_reports_a_missing_tokens_file()
    {
        using var dir = new TempDir();
        var result = await Setup().ApplyPresetAsync(dir.Path, "comet");
        Assert.False(result.Patched);
        Assert.Null(result.Path);
    }

    [Fact]
    public async Task EnsureThemeTokens_patches_the_tokens_file_in_place()
    {
        using var dir = new TempDir();
        await Setup().EnsureAsync(dir.Path, "Components/Ui");

        var result = await TailwindSetup.EnsureThemeTokensAsync(dir.Path, "default", "sm");

        Assert.True(result.HadSelection);
        Assert.True(result.Patched);
        Assert.Contains("--radius: 0.45rem;", dir.Read("Styles/app.css"));
    }

    [Fact]
    public async Task EnsureThemeTokens_reports_a_missing_file_and_a_default_selection()
    {
        using var dir = new TempDir();
        Assert.False((await TailwindSetup.EnsureThemeTokensAsync(dir.Path, "default", "default")).HadSelection);

        var missing = await TailwindSetup.EnsureThemeTokensAsync(dir.Path, "default", "sm");
        Assert.True(missing.HadSelection);
        Assert.False(missing.Patched);
        Assert.Null(missing.Path);
    }
}
