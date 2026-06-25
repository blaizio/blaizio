namespace Blazeo;

/// <summary>
/// The default item filter for <see cref="BaseCommand"/>: a case-insensitive match of the search against
/// an item's value and keywords. An item matches when the search is a substring of, or an in-order
/// subsequence of, the value or any keyword - so "calp" finds "Calendar Project" the way a fuzzy
/// command palette does. Override it with <see cref="BaseCommand.Filter"/> for custom scoring.
/// </summary>
public static class CommandFilter
{
    /// <summary>Whether <paramref name="value"/> (or any of <paramref name="keywords"/>) matches <paramref name="search"/>.</summary>
    public static bool Matches(string search, string value, IReadOnlyList<string>? keywords)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;

        var needle = search.Trim();
        if (MatchesTerm(value, needle)) return true;

        if (keywords is not null)
        {
            foreach (var keyword in keywords)
            {
                if (MatchesTerm(keyword, needle)) return true;
            }
        }

        return false;
    }

    // A strong substring hit first, then a looser in-order subsequence so typos / abbreviations still land.
    private static bool MatchesTerm(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase) || IsSubsequence(needle, haystack);

    // Every character of needle appears in haystack, in order (not necessarily adjacent), ignoring case.
    private static bool IsSubsequence(string needle, string haystack)
    {
        var i = 0;
        foreach (var c in haystack)
        {
            if (i < needle.Length && char.ToLowerInvariant(c) == char.ToLowerInvariant(needle[i])) i++;
        }

        return i == needle.Length;
    }
}
