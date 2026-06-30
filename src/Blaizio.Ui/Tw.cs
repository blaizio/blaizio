using TailwindMerge;

namespace Blaizio.Ui;

/// <summary>
/// The conventional <c>cn()</c> class helper: joins class fragments and resolves conflicting
/// Tailwind utilities (so a consumer's <c>Class</c> override wins predictably).
/// </summary>
public static class Tw
{
    private static readonly TwMerge Merger = new();

    /// <summary>Joins the non-empty fragments and merges conflicting Tailwind classes.</summary>
    public static string Merge(params string?[] classes) =>
        Merger.Merge(string.Join(' ', classes.Where(c => !string.IsNullOrWhiteSpace(c)))) ?? string.Empty;
}
