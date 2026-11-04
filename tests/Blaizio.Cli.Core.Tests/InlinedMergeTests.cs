using System.Text.RegularExpressions;
using Blaizio.Cli.Core.Styling;
using Blaizio.Ui;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// <c>Tw.Merge</c> over inlined strings, the way an installed component runs it. The docs keep
/// <c>bz-*</c> tokens in their class strings, so the merger never meets these utilities there and a
/// class it misreads looks fine on the site. TailwindMerge.NET 1.4.0 reads a percentage inside
/// <c>text-[color-mix(...)]</c> as a font size, so whichever of the two came last in a merge dropped
/// the other: a status Button lost its <c>text-sm</c>, or at Xs its color. The sheets spell these
/// <c>text-[color:color-mix(...)]</c>, and the last test here fails on any arbitrary color that doesn't.
/// This repository builds on 1.5.0, which reads both spellings, but a project keeps its pin until
/// <c>blaizio update</c>, and a component it adds meanwhile merges on that older version.
/// </summary>
public partial class InlinedMergeTests
{
    private static readonly string[] Statuses = ["destructive", "success", "warning", "info"];

    private static readonly string[] ButtonSizes =
        ["default", "xs", "sm", "lg", "icon", "icon-xs", "icon-sm", "icon-lg"];

    public static TheoryData<string> Styles =>
        new("ash", "aura", "ember", "flint", "forge", "glow", "spark", "wisp");

    [Theory]
    [MemberData(nameof(Styles))]
    public void Status_button_keeps_its_text_size_and_color_at_every_size(string style)
    {
        var inliner = CreateFor(style);
        foreach (var status in Statuses)
        {
            foreach (var size in ButtonSizes)
            {
                var sizeClasses = inliner.Resolve($"bz-button-size-{size}");
                var plain = Tw.Merge(inliner.Resolve("bz-button"), inliner.Resolve("bz-button-variant-outline"), sizeClasses);
                var merged = Tw.Merge(inliner.Resolve("bz-button"), inliner.Resolve($"bz-button-variant-{status}"), sizeClasses);

                Assert.NotEmpty(TextSizes(merged));
                Assert.Equal(TextSizes(plain), TextSizes(merged));
                Assert.Contains(StatusColor(status), Classes(merged));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Status_badge_keeps_its_text_size_and_color(string style)
    {
        var inliner = CreateFor(style);
        foreach (var status in Statuses)
        {
            var plain = Tw.Merge(inliner.Resolve("bz-badge"), inliner.Resolve("bz-badge-variant-outline"));
            var merged = Tw.Merge(inliner.Resolve("bz-badge"), inliner.Resolve($"bz-badge-variant-{status}"));

            Assert.NotEmpty(TextSizes(merged));
            Assert.Equal(TextSizes(plain), TextSizes(merged));
            Assert.Contains(StatusColor(status), Classes(merged));
        }
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Status_alert_keeps_its_text_size_and_color(string style)
    {
        var inliner = CreateFor(style);
        foreach (var status in Statuses)
        {
            var plain = Tw.Merge(inliner.Resolve("bz-alert"), inliner.Resolve("bz-alert-variant-default"));
            var merged = Tw.Merge(inliner.Resolve("bz-alert"), inliner.Resolve($"bz-alert-variant-{status}"));

            Assert.NotEmpty(TextSizes(merged));
            Assert.Equal(TextSizes(plain), TextSizes(merged));
            Assert.Contains(StatusColor(status), Classes(merged));
        }
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void A_consumer_color_replaces_the_status_color(string style)
    {
        var inliner = CreateFor(style);
        foreach (var status in Statuses)
        {
            foreach (var consumer in new[] { "text-red-500", "text-[#123456]" })
            {
                string[] merges =
                [
                    Tw.Merge(inliner.Resolve("bz-button"), inliner.Resolve($"bz-button-variant-{status}"), inliner.Resolve("bz-button-size-default"), consumer),
                    Tw.Merge(inliner.Resolve("bz-badge"), inliner.Resolve($"bz-badge-variant-{status}"), consumer),
                    Tw.Merge(inliner.Resolve("bz-alert"), inliner.Resolve($"bz-alert-variant-{status}"), consumer),
                ];
                foreach (var merged in merges)
                {
                    Assert.Contains(consumer, Classes(merged));
                    Assert.DoesNotContain(StatusColor(status), Classes(merged));
                    Assert.NotEmpty(TextSizes(merged));
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void A_consumer_color_replaces_a_token_color_under_the_same_variant(string style)
    {
        var syntax = CreateFor(style).Resolve("bz-code-block-syntax");
        var merged = Classes(Tw.Merge(syntax, "[&_[data-token=string]]:text-[#123456]"));

        Assert.Contains("[&_[data-token=string]]:text-[#123456]", merged);
        Assert.DoesNotContain(merged, c => c.StartsWith("[&_[data-token=string]]:text-[color:", StringComparison.Ordinal));
        Assert.Contains(merged, c => c.StartsWith("[&_[data-token=keyword]]:text-[color:", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Added_line_marker_replaces_the_muted_marker(string style)
    {
        var inliner = CreateFor(style);
        var merged = Classes(Tw.Merge(inliner.Resolve("bz-code-block-line"), inliner.Resolve("bz-code-block-line-variant-added")));

        Assert.Contains($"after:{StatusColor("success")}", merged);
        Assert.DoesNotContain("after:text-muted-foreground", merged);
    }

    // Every sheet token and every literal in the components, so a new arbitrary color needs no new test.
    // The spelling is asserted on its own: the merges below pass on 1.5.0 either way, so they alone
    // would let through a bare color that breaks every project still on 1.4.0.
    [Theory]
    [MemberData(nameof(Styles))]
    public void Every_arbitrary_text_color_reads_as_a_color(string style)
    {
        var inliner = CreateFor(style);
        var candidates = inliner.Tokens
            .SelectMany(token => Classes(inliner.Resolve(token)!))
            .Concat(SourceClasses())
            .Where(c => ArbitraryTextColor().IsMatch(c))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(candidates);

        foreach (var color in candidates)
        {
            var variants = color[..color.LastIndexOf("text-[", StringComparison.Ordinal)];

            Assert.DoesNotContain("text-[color-mix(", color);
            Assert.Contains($"{variants}text-sm", Classes(Tw.Merge($"{variants}text-sm", color)));
            Assert.Contains(color, Classes(Tw.Merge($"{variants}text-sm", color)));
            Assert.DoesNotContain(color, Classes(Tw.Merge(color, $"{variants}text-red-500")));
        }
    }

    private static string StatusColor(string status) =>
        $"text-[color:color-mix(in_oklab,var(--{status}),black_20%)]";

    private static string[] Classes(string merged) =>
        merged.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string[] TextSizes(string merged) =>
        [.. Classes(merged).Where(c => TextSize().IsMatch(c))];

    private static IEnumerable<string> SourceClasses() =>
        Directory.EnumerateFiles(Path.Combine(Root(), "src", "Blaizio.Ui", "Components"), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
            .SelectMany(f => File.ReadAllText(f).Split([' ', '"', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

    private static StyleInliner CreateFor(string style)
    {
        var styles = Path.Combine(Root(), "src", "Blaizio.Ui", "Styles");
        return StyleInliner.Create(
            File.ReadAllText(Path.Combine(styles, "shared.css")),
            File.ReadAllText(Path.Combine(styles, $"style-{style}.css")));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Blaizio.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    // Bare sizes only: a variant-prefixed one styles a child or a state and never meets the base.
    [GeneratedRegex(@"^text-(xs|sm|base|lg|\d*xl|\[[\d.]+(px|rem|em)\])(/\S+)?$")]
    private static partial Regex TextSize();

    [GeneratedRegex(@"(^|:)text-\[(?![\d.]+(px|rem|em)\])")]
    private static partial Regex ArbitraryTextColor();
}
