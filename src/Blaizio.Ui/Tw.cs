using Microsoft.Extensions.Options;
using TailwindMerge;
using TailwindMerge.Models;

namespace Blaizio.Ui;

/// <summary>
/// The conventional <c>cn()</c> class helper: joins class fragments and resolves conflicting
/// Tailwind utilities (so a consumer's <c>Class</c> override wins predictably).
/// </summary>
public static class Tw
{
    // Blaizio's own scrollbar utilities (blaizio.css) are not part of Tailwind's vocabulary, so the
    // merger would keep BOTH the component's `scrollbar-thin` and a consumer's `scrollbar-none` -
    // and the cascade always hands that fight to `scrollbar-thin`, whatever the class order. Grouping
    // them makes the LAST one win, which is what every other Class override in the library does.
    // `scrollbar-auto` is the opt-out: passing it drops our styling, leaving the browser's own bar -
    // or a clean slate for a scrollbar class of the consumer's own, passed alongside it.
    private static readonly TwMergeConfig ScrollbarAware = BuildConfig();

    private static readonly TwMerge Merger = new(Options.Create(ScrollbarAware));

    private static TwMergeConfig BuildConfig()
    {
        var config = TwMergeConfig.Default();
        config.ClassGroups["bz-scrollbar"] =
            new ClassGroup("scrollbar", ["thin", "hover", "activity", "none", "auto"]);
        return config;
    }

    /// <summary>Joins the non-empty fragments and merges conflicting Tailwind classes.</summary>
    public static string Merge(params string?[] classes) =>
        Merger.Merge(string.Join(' ', classes.Where(c => !string.IsNullOrWhiteSpace(c)))) ?? string.Empty;
}
