using Blaizio.Cli.Core.Registry;

namespace Blaizio.Cli.Core.Resolution;

/// <summary>
/// Walks <see cref="RegistryItem.RegistryDependencies"/> transitively from a set of requested
/// items and returns them in install order (a dependency always precedes anything that needs it).
/// Cycles and diamonds are handled: each item is emitted exactly once, after its dependencies.
/// </summary>
public sealed class DependencyResolver(IRegistryClient client)
{
    /// <summary>Resolve the full graph for the requested item names/URLs/paths.</summary>
    public async Task<ResolvedGraph> ResolveAsync(
        IReadOnlyList<string> requested,
        CancellationToken ct = default)
    {
        var ordered = new List<RegistryItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fetched = new Dictionary<string, RegistryItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in requested)
            await VisitAsync(reference, ordered, seen, fetched, ct);

        var nuget = ordered
            .SelectMany(i => i.NugetDependencies ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ResolvedGraph
        {
            Items = ordered,
            NugetPackages = nuget,
            Requested = [.. requested],
        };
    }

    private async Task VisitAsync(
        string reference,
        List<RegistryItem> ordered,
        HashSet<string> seen,
        Dictionary<string, RegistryItem> fetched,
        CancellationToken ct)
    {
        if (!fetched.TryGetValue(reference, out var item))
            fetched[reference] = item = await client.GetItemAsync(reference, ct);

        // Reserve the name before recursing so a cycle back to this item terminates,
        // and a diamond (two dependents share one dep) emits the dep once.
        if (!seen.Add(item.Name))
            return;

        foreach (var dep in item.RegistryDependencies ?? [])
            await VisitAsync(dep, ordered, seen, fetched, ct);

        ordered.Add(item);
    }
}
