namespace Blaizio.Cli.Core;

/// <summary>
/// Resolves externally supplied relative paths (registry files, template files) against a root
/// directory while guaranteeing the result cannot escape it.
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
        if (Path.IsPathRooted(relative))
            throw new InvalidOperationException(
                $"Refusing to write '{relative}': absolute paths are not allowed.");

        var rootFull = Path.GetFullPath(root);
        var combined = Path.GetFullPath(Path.Combine(rootFull, relative));

        var rootWithSep = Path.EndsInDirectorySeparator(rootFull)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSep, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Refusing to write '{relative}': path escapes the project directory.");

        return combined;
    }
}
