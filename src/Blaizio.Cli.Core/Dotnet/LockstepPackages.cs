using Blaizio.Cli.Core.Operations;

namespace Blaizio.Cli.Core.Dotnet;

/// <summary>
/// The packages Blaizio ships under one version (<c>BlaizioVersionBase</c>): Blaizio.Base,
/// Blaizio.Icons and every icon set, Blaizio.Syntax and Blaizio.Syntax.Languages. Each depends on
/// the others at its own version, so a newer Blaizio.Syntax beside an older Blaizio.Base fails the
/// restore (NU1605). Registry items name them without a version; this is where an install learns
/// which one the project is on.
/// </summary>
public static class LockstepPackages
{
    /// <summary>Whether <paramref name="id"/> is one of Blaizio's lockstep packages.</summary>
    public static bool Contains(string id) =>
        id.Equals(BaseVersionGuard.BasePackageId, StringComparison.OrdinalIgnoreCase)
        || id.Equals("Blaizio.Icons", StringComparison.OrdinalIgnoreCase)
        || id.StartsWith("Blaizio.Icons.", StringComparison.OrdinalIgnoreCase)
        || id.Equals("Blaizio.Syntax", StringComparison.OrdinalIgnoreCase)
        || id.StartsWith("Blaizio.Syntax.", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="packages"/> with each lockstep package that has no version and is not
    /// referenced yet pinned to the Blaizio.Base version the project references - so adding
    /// <c>code-block</c> brings the Blaizio.Syntax of the project's release, not the newest one.
    /// Unchanged when the project references no plain Blaizio.Base version (none, a float, a
    /// range): then the newest is right, as before. A package already referenced is left alone,
    /// whatever its version: moving it is <c>update</c>'s job.
    /// </summary>
    public static IReadOnlyList<NugetDependency> PinTo(
        IEnumerable<NugetDependency> packages, IReadOnlyDictionary<string, string?> referenced)
    {
        var list = packages.ToList();
        if (referenced.GetValueOrDefault(BaseVersionGuard.BasePackageId) is not { } baseVersion
            || !PackageVersion.IsPlain(baseVersion))
            return list;
        return [.. list.Select(p => p.Version is null && Contains(p.Id) && !referenced.ContainsKey(p.Id)
            ? p with { Version = baseVersion.Trim() }
            : p)];
    }
}
