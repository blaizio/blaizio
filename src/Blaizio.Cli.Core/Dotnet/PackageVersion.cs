namespace Blaizio.Cli.Core.Dotnet;

/// <summary>
/// SemVer 2.0 ordering for the version strings a csproj pins, small enough to not warrant a
/// NuGet.Versioning reference. Handles the shapes Blaizio actually ships: release
/// (<c>1.2.3</c>), prerelease (<c>0.1.0-alpha.24</c>), and dogfood revisions
/// (<c>0.1.0-alpha.23.19</c>). Anything else - wildcards, ranges, garbage - refuses to compare
/// rather than guessing.
/// </summary>
public static class PackageVersion
{
    /// <summary>
    /// Compare two version strings under SemVer precedence. Returns <see langword="false"/> when
    /// either side is not a plain (pre)release version - a float like <c>0.1.0-alpha.*</c>, a
    /// range, an empty string - in which case <paramref name="result"/> means nothing.
    /// </summary>
    public static bool TryCompare(string? left, string? right, out int result)
    {
        result = 0;
        if (!TryParse(left, out var l) || !TryParse(right, out var r))
            return false;

        for (var i = 0; i < 3; i++)
        {
            if (l.Release[i] != r.Release[i])
            {
                result = l.Release[i].CompareTo(r.Release[i]);
                return true;
            }
        }

        // Equal release: a release version outranks any prerelease of it.
        if (l.Prerelease.Length == 0 || r.Prerelease.Length == 0)
        {
            result = r.Prerelease.Length.CompareTo(l.Prerelease.Length);
            return true;
        }

        // Prerelease identifiers, left to right: numerics compare numerically and rank below
        // alphanumerics; otherwise ordinal. A longer list wins once the shorter is its prefix.
        var count = Math.Min(l.Prerelease.Length, r.Prerelease.Length);
        for (var i = 0; i < count; i++)
        {
            var (a, b) = (l.Prerelease[i], r.Prerelease[i]);
            var aNum = long.TryParse(a, out var an);
            var bNum = long.TryParse(b, out var bn);
            result = (aNum, bNum) switch
            {
                (true, true) => an.CompareTo(bn),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(a, b),
            };
            if (result != 0)
                return true;
        }
        result = l.Prerelease.Length.CompareTo(r.Prerelease.Length);
        return true;
    }

    private static bool TryParse(string? text, out (long[] Release, string[] Prerelease) parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var span = text.Trim();
        // Build metadata never affects precedence.
        var plus = span.IndexOf('+');
        if (plus >= 0) span = span[..plus];

        var dash = span.IndexOf('-');
        var releasePart = dash < 0 ? span : span[..dash];
        var prereleasePart = dash < 0 ? null : span[(dash + 1)..];

        var numbers = releasePart.Split('.');
        if (numbers.Length != 3)
            return false;
        var release = new long[3];
        for (var i = 0; i < 3; i++)
        {
            if (!long.TryParse(numbers[i], out release[i]) || release[i] < 0)
                return false;
        }

        var prerelease = prereleasePart is null ? [] : prereleasePart.Split('.');
        foreach (var id in prerelease)
        {
            if (id.Length == 0)
                return false;
            foreach (var c in id)
                if (!char.IsAsciiLetterOrDigit(c) && c != '-')
                    return false;
        }

        parsed = (release, prerelease);
        return true;
    }
}
