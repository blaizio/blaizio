namespace Blaizio.Cli.Core;

/// <summary>
/// Resolves externally supplied relative paths (registry files, template files, persisted config
/// records) against a root directory while guaranteeing the result cannot escape it.
/// </summary>
internal static class SafePath
{
    /// <summary>
    /// Combine <paramref name="root"/> with <paramref name="relative"/> and return the full path.
    /// Throws when <paramref name="relative"/> is rooted or when the combined path resolves
    /// outside <paramref name="root"/> (e.g. via <c>..</c> segments).
    /// </summary>
    public static string Resolve(string root, string relative)
    {
        var combined = ResolveCore(root, relative, out var rootFull);
        if (string.Equals(combined, rootFull, Comparison))
            throw new InvalidOperationException(
                $"Refusing path '{relative}': it resolves to the root directory itself.");
        return combined;
    }

    /// <summary>
    /// Like <see cref="Resolve"/>, for a directory the caller will treat as a containment root
    /// (an output dir, a prune root): the result may be <paramref name="root"/> itself, so values
    /// like <c>"."</c> or <c>""</c> are accepted.
    /// </summary>
    public static string ResolveDir(string root, string relative)
        => ResolveCore(root, relative, out _);

    private static string ResolveCore(string root, string relative, out string rootFull)
    {
        if (Path.IsPathRooted(relative))
            throw new InvalidOperationException(
                $"Refusing path '{relative}': absolute paths are not allowed.");

        rootFull = Path.GetFullPath(root);
        var combined = Path.GetFullPath(Path.Combine(rootFull, relative));

        var rootWithSep = Path.EndsInDirectorySeparator(rootFull)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;

        if (!string.Equals(combined, rootFull, Comparison)
            && !combined.StartsWith(rootWithSep, Comparison))
            throw new InvalidOperationException(
                $"Refusing path '{relative}': it escapes the containing directory.");

        return combined;
    }

    private static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
