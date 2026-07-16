using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class SkinInlinerTests
{
    private const string Shared =
        """
        /* baseline */
        .bz-button {
          @apply border bg-clip-padding;
        }

        .bz-button-variant-default {
          @apply bg-primary text-primary-foreground hover:bg-primary/80;
        }

        .bz-popover-content,
        .bz-hover-card-content {
          @apply z-50 rounded-md border shadow-md;
        }

        /* multi-@apply bodies concatenate */
        .bz-card {
          @apply flex flex-col;
          @apply rounded-xl border;
        }

        /* raw declarations become arbitrary properties */
        .bz-anchor {
          transition: left 0.15s ease, top 0.15s ease;
        }

        /* self-attribute rules compile to data variants */
        .bz-checkbox[data-color='success'][data-color] {
          @apply data-checked:bg-success;
        }

        .bz-dropzone[data-dragging] {
          @apply border-primary;
        }

        /* parent-state rules compile to named group variants */
        .bz-attachment[data-state='error'] .bz-attachment-title {
          @apply text-destructive;
        }

        /* the bare pin has no bz token - skipped */
        [data-state='closed'] {
          --tw-animation-fill-mode: forwards;
        }

        /* !important bodies are contract territory */
        .bz-frozen {
          animation: none !important;
        }

        @media (prefers-reduced-motion: reduce) {
          .bz-gated {
            animation: none;
          }
        }
        """;

    private const string Skin =
        """
        .style-test {
          .bz-button {
            @apply rounded-md text-sm font-medium;
          }

          /* conflicting utility: the skin wins via TwMerge */
          .bz-popover-content,
          .bz-hover-card-content {
            @apply rounded-xl p-1;
          }

          /* specificity hack normalizes to the token */
          .bz-input-group-input.bz-input-group-input {
            @apply rounded-none shadow-none;
          }

          .bz-pagination-link[data-active="true"] {
            @apply bg-background shadow-xs;
          }

          .bz-otp-slot:first-child {
            @apply rounded-s-sm;
          }

          .bz-table-sticky th {
            @apply bg-background;
          }

          .bz-separator:empty::after {
            content: "";
            transform: rotate(-45deg);
          }

          [dir="rtl"] .bz-separator:empty::after {
            transform: rotate(135deg);
          }
        }
        """;

    private static readonly SkinInliner Inliner = SkinInliner.Create(Shared, Skin);

    [Fact]
    public void Merges_shared_baseline_under_the_skin()
    {
        var result = Inliner.Inline("bz-button");
        Assert.Contains("border", result);
        Assert.Contains("bg-clip-padding", result);
        Assert.Contains("rounded-md", result);
        Assert.Contains("font-medium", result);
    }

    [Fact]
    public void Skin_wins_conflicting_utilities()
    {
        var result = Inliner.Inline("bz-popover-content");
        Assert.Contains("rounded-xl", result);
        Assert.DoesNotContain("rounded-md", result);
    }

    [Fact]
    public void Comma_lists_assign_the_body_to_every_token()
        => Assert.Equal(Inliner.Inline("bz-popover-content"), Inliner.Inline("bz-hover-card-content"));

    [Fact]
    public void Raw_declarations_become_arbitrary_properties()
        => Assert.Equal("[transition:left_0.15s_ease,_top_0.15s_ease]", Inliner.Resolve("bz-anchor"));

    [Fact]
    public void Self_attribute_rules_compile_to_data_variants()
    {
        // The doubled [data-color] specificity hack folds away.
        Assert.Equal("data-[color=success]:data-checked:bg-success", Inliner.Resolve("bz-checkbox"));
        // Bare data attribute -> bare data variant.
        Assert.Equal("data-dragging:border-primary", Inliner.Resolve("bz-dropzone"));
    }

    [Fact]
    public void Parent_state_rules_compile_to_named_groups()
    {
        Assert.Equal("group-data-[state=error]/attachment:text-destructive", Inliner.Resolve("bz-attachment-title"));
        // The parent token gains the group marker.
        Assert.Equal("group/attachment", Inliner.Resolve("bz-attachment"));
    }

    [Fact]
    public void Repeated_class_hacks_normalize_to_the_token()
        => Assert.Equal("rounded-none shadow-none", Inliner.Resolve("bz-input-group-input"));

    [Fact]
    public void Suffix_shapes_compile_to_variant_prefixes()
    {
        Assert.Equal("data-[active=true]:bg-background data-[active=true]:shadow-xs", Inliner.Resolve("bz-pagination-link"));
        Assert.Equal("first:rounded-s-sm", Inliner.Resolve("bz-otp-slot"));
        Assert.Equal("[&_th]:bg-background", Inliner.Resolve("bz-table-sticky"));
    }

    [Fact]
    public void Pseudo_elements_and_rtl_ancestors_compile()
    {
        var separator = Inliner.Resolve("bz-separator");
        Assert.NotNull(separator);
        Assert.Contains("empty:after:[content:\"\"]", separator);
        Assert.Contains("empty:after:[transform:rotate(-45deg)]", separator);
        Assert.Contains("rtl:empty:after:[transform:rotate(135deg)]", separator);
    }

    [Fact]
    public void Contract_territory_stays_unmapped()
    {
        Assert.Null(Inliner.Resolve("bz-frozen"));   // !important body
        Assert.Null(Inliner.Resolve("bz-gated"));    // under @media
        Assert.Equal("bz-toast-spinner", Inliner.Inline("bz-toast-spinner")); // no rule at all
    }

    [Fact]
    public void Tokens_never_match_partially()
    {
        var inliner = SkinInliner.Create(".bz-button { @apply X; }", "");
        Assert.Equal("bz-button-variant-default", inliner.Inline("bz-button-variant-default"));
    }

    [Fact]
    public void Substitutes_inside_razor_and_cs_source_strings()
    {
        var razor = """<div class="bz-card extra">x</div>""";
        Assert.Equal($"""<div class="{Inliner.Resolve("bz-card")} extra">x</div>""", Inliner.Inline(razor));
    }

    [Fact]
    public void Parses_the_real_sheets_without_error()
    {
        var root = FindRepoRoot();
        var shared = File.ReadAllText(Path.Combine(root, "src", "Blaizio.Ui", "Styles", "shared.css"));
        foreach (var skin in new[] { "ash", "aura", "ember", "flint", "forge", "glow", "spark", "wisp" })
        {
            var skinCss = File.ReadAllText(Path.Combine(root, "src", "Blaizio.Ui", "Styles", $"style-{skin}.css"));
            var inliner = SkinInliner.Create(shared, skinCss);
            Assert.True(inliner.Tokens.Count > 180, $"{skin}: only {inliner.Tokens.Count} tokens");
            Assert.Contains("bg-primary", inliner.Inline("bz-button-variant-default"));
            // The compiled shapes land: pagination active state, checkbox colors, attachment group.
            Assert.Contains("data-[active=true]:", inliner.Resolve("bz-pagination-link"));
            Assert.Contains("data-[color=success]:", inliner.Resolve("bz-checkbox"));
            Assert.Contains("group/attachment", inliner.Resolve("bz-attachment"));
            // Chart svg parts stay contract-owned.
            Assert.Null(inliner.Resolve("bz-chart-bar"));
            // The chart root carries the skin's theme vars.
            Assert.Contains("--bz-chart-bar-radius", inliner.Resolve("bz-chart"));
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Blaizio.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
