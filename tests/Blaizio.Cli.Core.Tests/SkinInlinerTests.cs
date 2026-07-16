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

        /* contract-shaped rules are ignored by the parser */
        .bz-chart-tick {
          @apply fill-muted-foreground;
          font-size: 11px;
        }

        .bz-attachment[data-state='error'] .bz-attachment-title {
          @apply text-destructive;
        }

        [data-state='closed'] {
          --tw-animation-fill-mode: forwards;
        }

        @media (prefers-reduced-motion: reduce) {
          .bz-chart {
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
        }
        """;

    private static SkinInliner Create() => SkinInliner.Create(Shared, Skin);

    [Fact]
    public void Merges_shared_baseline_under_the_skin()
    {
        var inliner = Create();
        var result = inliner.Inline("bz-button");

        // Baseline + skin, one string.
        Assert.Contains("border", result);
        Assert.Contains("bg-clip-padding", result);
        Assert.Contains("rounded-md", result);
        Assert.Contains("font-medium", result);
    }

    [Fact]
    public void Skin_wins_conflicting_utilities()
    {
        var result = Create().Inline("bz-popover-content");

        Assert.Contains("rounded-xl", result);
        Assert.DoesNotContain("rounded-md", result);
        Assert.Contains("p-1", result);
    }

    [Fact]
    public void Comma_lists_assign_the_body_to_every_token()
    {
        var inliner = Create();
        Assert.Equal(inliner.Inline("bz-popover-content"), inliner.Inline("bz-hover-card-content"));
    }

    [Fact]
    public void Multiple_apply_statements_concatenate()
    {
        var result = Create().Inline("bz-card");
        Assert.Contains("flex-col", result);
        Assert.Contains("rounded-xl", result);
    }

    [Fact]
    public void Unmapped_tokens_pass_through_verbatim()
    {
        var inliner = Create();
        // Contract-owned hooks (raw-decl rule was skipped) and unknown names survive.
        Assert.Equal("bz-chart-tick", inliner.Inline("bz-chart-tick"));
        Assert.Equal("bz-attachment-title", inliner.Inline("bz-attachment-title"));
        Assert.Equal("bz-toast-spinner", inliner.Inline("bz-toast-spinner"));
    }

    [Fact]
    public void Tokens_never_match_partially()
    {
        // bz-button must not fire inside the longer token.
        var inliner = SkinInliner.Create(".bz-button { @apply X; }", "");
        Assert.Equal("bz-button-variant-default", inliner.Inline("bz-button-variant-default"));
    }

    [Fact]
    public void Substitutes_inside_razor_and_cs_source_strings()
    {
        var inliner = Create();

        var razor = """<div class="bz-button extra-class">x</div>""";
        Assert.Equal(
            $"""<div class="{inliner.Inline("bz-button")} extra-class">x</div>""",
            inliner.Inline(razor));

        var cs = """private const string BaseClasses = "bz-card group/card";""";
        Assert.Contains("rounded-xl border group/card", inliner.Inline(cs).Replace("flex flex-col ", ""));
    }

    [Fact]
    public void Tokens_lists_the_mapped_tokens()
    {
        var tokens = Create().Tokens;
        Assert.Contains("bz-button", tokens);
        Assert.Contains("bz-hover-card-content", tokens);
        Assert.DoesNotContain("bz-chart-tick", tokens);
        Assert.DoesNotContain("bz-attachment-title", tokens);
    }

    [Fact]
    public void Parses_the_real_sheets_without_error()
    {
        // Smoke over the real authored sheets: every skin builds a map with a healthy token count.
        var root = FindRepoRoot();
        var shared = File.ReadAllText(Path.Combine(root, "src", "Blaizio.Ui", "Styles", "shared.css"));
        foreach (var skin in new[] { "ash", "aura", "ember", "flint", "forge", "glow", "spark", "wisp" })
        {
            var skinCss = File.ReadAllText(Path.Combine(root, "src", "Blaizio.Ui", "Styles", $"style-{skin}.css"));
            var inliner = SkinInliner.Create(shared, skinCss);
            Assert.True(inliner.Tokens.Count > 150, $"{skin}: only {inliner.Tokens.Count} tokens");
            // The flagship token resolves to real utilities in every skin.
            Assert.Contains("bg-primary", inliner.Inline("bz-button-variant-default"));
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
