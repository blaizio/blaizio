using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class TailwindMigrationTests
{
    private static TailwindSetup Setup() => new(new FakeCssAssets());

    /// <summary>A v1 project's managed theme.css - with a user-customized --primary that must
    /// survive, plus every v1-only piece the migration must drop.</summary>
    private const string V1Theme =
        """
        /* v1 maintainer comment */
        @custom-variant dark (&:is(.dark *));

        @theme inline {
          --font-heading: var(--font-heading);
          --radius-lg: var(--radius);
          --color-background: var(--background);
          --color-primary: var(--primary);
          --color-muted-foreground: var(--muted-foreground);
        }

        :root {
          --font-heading: var(--font-sans, ui-sans-serif, system-ui, sans-serif);
          --radius: 0.75rem;
          --background: oklch(1 0 0);
          --primary: oklch(0.42 0.19 275);
          --chart-1: oklch(0.63 0.23 304);
        }

        .dark {
          --background: oklch(0.17 0 0);
          --primary: oklch(0.6 0.17 275);
          --primary-button: oklch(0.53 0.18 275);
        }

        @layer base {
          * {
            border-color: var(--color-border);
          }
          .bz-font-heading {
            @apply font-heading;
          }
          body {
            background-color: var(--color-background);
            color: var(--color-foreground);
          }
        }

        .dark .bz-button-variant-default {
          background-color: var(--primary-button);
        }
        .dark .bz-button-variant-default:hover {
          background-color: color-mix(in oklab, var(--primary-button) 80%, transparent);
        }
        """;

    private const string V1Input =
        """
        /* blaizio:managed */
        @import "tailwindcss" source(none);
        @import "./blaizio/animate.css";
        @import "./blaizio/theme.css";
        @import "./blaizio/base.css";
        @import "./blaizio/shared.css" layer(components);
        @import "./blaizio/style-ember.css" layer(components);
        @source "../Components/Ui/**/*.razor";
        @source "../**/*.razor";
        """;

    private static void WriteV1Project(TempDir dir)
    {
        dir.Write("Styles/app.css", V1Input);
        dir.Write("Styles/blaizio/theme.css", V1Theme);
        dir.Write("Styles/blaizio/animate.css", "/* animate */");
        dir.Write("Styles/blaizio/base.css", "/* base */");
        dir.Write("Styles/blaizio/shared.css", "/* shared */");
        dir.Write("Styles/blaizio/style-ember.css", "/* skin */");
    }

    [Fact]
    public async Task Migrates_a_cli_owned_input_preserving_user_values()
    {
        using var dir = new TempDir();
        WriteV1Project(dir);

        var result = await Setup().MigrateAsync(dir.Path, "Components/Ui");
        var css = dir.Read("Styles/app.css");

        Assert.True(result.InputWasCliOwned);
        Assert.Equal("Styles/app.css", result.InputPath);
        // v3 imports in, v1 imports and sheets gone.
        Assert.Contains("@import \"../.blaizio/blaizio.css\";", css);
        Assert.DoesNotContain("./blaizio/", css);
        Assert.False(Directory.Exists(dir.Combine("Styles", "blaizio")));
        Assert.Contains(result.Removed, r => r.EndsWith("style-ember.css"));
        // The user's edited value survives - composed from THEIR theme.css, not the assets.
        Assert.Contains("--primary: oklch(0.42 0.19 275);", css);
        Assert.Contains("--primary: oklch(0.6 0.17 275);", css);
        // v1-only pieces are gone.
        Assert.DoesNotContain("--primary-button", css);
        Assert.DoesNotContain("bz-font-heading", css);
        Assert.DoesNotContain("bz-button-variant-default", css);
        // The rest of the base layer stays.
        Assert.Contains("border-color: var(--color-border);", css);
        Assert.Contains(".blaizio/", dir.Read(".gitignore"));
        Assert.False(TailwindSetup.IsLegacyV1(dir.Path));
    }

    [Fact]
    public async Task Folds_the_preset_fonts_and_pointer_overlays_into_the_block()
    {
        using var dir = new TempDir();
        WriteV1Project(dir);
        dir.Write("Styles/blaizio/preset-comet.css",
            """
            .preset-comet {
              --primary: oklch(0.5 0.11 195);
            }

            .preset-comet.dark {
              --primary: oklch(0.61 0.1 195);
              --primary-button: oklch(0.5 0.1 195);
            }
            """);
        dir.Write("Styles/blaizio/fonts.css",
            """
            /* blaizio:managed */
            :root { --font-heading: Georgia, "Times New Roman", serif; }
            html { font-family: ui-monospace, monospace; }
            """);
        dir.Write("Styles/blaizio/options.css",
            """
            /* blaizio:managed */
            @layer base {
              button:not(:disabled),
              [role="button"]:not(:disabled) { cursor: pointer; }
            }
            """);

        await Setup().MigrateAsync(dir.Path, "Components/Ui", preset: "comet");
        var css = dir.Read("Styles/app.css");

        Assert.Contains("--primary: oklch(0.5 0.11 195);", css);  // preset over the user's :root value
        Assert.Contains("--primary: oklch(0.61 0.1 195);", css);  // preset .dark
        Assert.DoesNotContain("--primary-button", css);           // retired token never migrates
        Assert.Contains("--font-heading: Georgia", css);
        Assert.Contains("font-family: ui-monospace", css);
        Assert.Contains("cursor: pointer", css);
    }

    [Fact]
    public async Task Migrates_a_bundler_input_without_clobbering_it()
    {
        using var dir = new TempDir();
        dir.Write("Styles/blaizio/theme.css", V1Theme);
        dir.Write("tailwind.css",
            "@import \"tailwindcss\";\n" +
            "@import \"./Styles/blaizio/theme.css\";\n" +
            "@import \"./Styles/blaizio/style-ember.css\" layer(components);\n" +
            ".hero { color: red; }\n");

        var result = await Setup().MigrateAsync(dir.Path, "Components/Ui", cssInput: "tailwind.css");
        var css = dir.Read("tailwind.css");

        Assert.False(result.InputWasCliOwned);
        Assert.Contains(".hero { color: red; }", css);            // user content preserved
        Assert.Contains("@import \"./.blaizio/blaizio.css\";", css);
        Assert.DoesNotContain("Styles/blaizio", css);             // stale v1 lines stripped
        Assert.Contains("--primary: oklch(0.42 0.19 275);", css); // token block injected with their values
        Assert.False(Directory.Exists(dir.Combine("Styles", "blaizio")));
        Assert.False(TailwindSetup.IsLegacyV1(dir.Path));
    }

    [Fact]
    public async Task Missing_sheets_fall_back_to_the_embedded_assets()
    {
        using var dir = new TempDir();
        // Legacy detected by the input's imports alone; the sheets themselves were deleted.
        dir.Write("Styles/app.css", V1Input);

        await Setup().MigrateAsync(dir.Path, "Components/Ui");
        var css = dir.Read("Styles/app.css");

        Assert.Contains("@theme inline", css);
        Assert.Contains("--primary: oklch(0.55 0.22 304);", css); // the FakeCssAssets baseline
        Assert.DoesNotContain("./blaizio/", css);
    }
}
