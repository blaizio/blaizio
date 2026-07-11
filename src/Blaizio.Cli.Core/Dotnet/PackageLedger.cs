using Blaizio.Cli.Core.Configuration;

namespace Blaizio.Cli.Core.Dotnet;

/// <summary>
/// Tracks which NuGet packages the CLI itself installed, so <c>deinit</c> undoes exactly those.
/// A package the csproj referenced before the CLI ran is user-owned and never recorded — the
/// ledger works strictly by record, never by name pattern.
/// </summary>
public static class PackageLedger
{
    /// <summary>
    /// The subset of <paramref name="ids"/> the csproj already references (before an install runs).
    /// These stay off the ledger even though <c>dotnet add package</c> is about to touch them.
    /// </summary>
    public static IReadOnlySet<string> PreExisting(string? csprojPath, IEnumerable<string> ids)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (csprojPath is null || !File.Exists(csprojPath))
            return result;

        var text = File.ReadAllText(csprojPath);
        foreach (var id in ids)
        {
            if (text.Contains($"Include=\"{id}\"", StringComparison.OrdinalIgnoreCase))
                result.Add(id);
        }
        return result;
    }

    /// <summary>
    /// Record the freshly installed <paramref name="ids"/> in <paramref name="config"/>, skipping
    /// <paramref name="preExisting"/> ones and duplicates. Returns true when the ledger changed.
    /// </summary>
    public static bool Record(BlaizioConfig config, IEnumerable<string> ids, IReadOnlySet<string> preExisting)
    {
        var changed = false;
        foreach (var id in ids)
        {
            if (preExisting.Contains(id))
                continue;
            if (config.Packages.Any(p => string.Equals(p, id, StringComparison.OrdinalIgnoreCase)))
                continue;
            config.Packages.Add(id);
            changed = true;
        }
        return changed;
    }
}
