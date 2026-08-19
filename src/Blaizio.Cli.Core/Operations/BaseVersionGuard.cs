using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Registry;

namespace Blaizio.Cli.Core.Operations;

/// <summary>
/// The pre-install check behind <see cref="RegistryItem.MinBase"/>: an item that calls into a Base
/// capability newer than the project's pinned <c>Blaizio.Base</c> must fail BEFORE its sources
/// land, with the upgrade path spelled out - the alternative is a component that installs cleanly
/// and 404s its JS module at runtime, which nothing on the way there warns about (an unpinned
/// package the csproj already references is skipped without a version look).
/// </summary>
public static class BaseVersionGuard
{
    /// <summary>The package the check reads from the csproj.</summary>
    public const string BasePackageId = "Blaizio.Base";

    /// <summary>
    /// Returns the failure message when <paramref name="referencedVersion"/> is a plain version
    /// older than some item's <see cref="RegistryItem.MinBase"/>, or null when the install may
    /// proceed. No reference, a floating reference (<c>0.1.0-alpha.*</c>), or an unparseable pin
    /// all pass: a missing reference installs fresh (and current), and a float resolves forward on
    /// restore - only a definite, too-old pin is worth stopping for.
    /// </summary>
    public static string? Check(IEnumerable<RegistryItem> items, string? referencedVersion)
    {
        if (referencedVersion is null)
            return null;

        (string Name, string Min)? strictest = null;
        foreach (var item in items)
        {
            if (item.MinBase is not { } min)
                continue;
            if (strictest is null
                || (PackageVersion.TryCompare(min, strictest.Value.Min, out var order) && order > 0))
                strictest = (item.Name, min);
        }
        if (strictest is null)
            return null;

        if (!PackageVersion.TryCompare(referencedVersion, strictest.Value.Min, out var cmp) || cmp >= 0)
            return null;

        return $"'{strictest.Value.Name}' needs {BasePackageId} {strictest.Value.Min} or newer; " +
               $"this project references {referencedVersion}. Update the tool first, then the project: " +
               $"dotnet tool update --global Blaizio.Cli, then blaizio update.";
    }
}
