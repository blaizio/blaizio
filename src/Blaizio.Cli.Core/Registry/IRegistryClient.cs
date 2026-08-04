namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// What a search asks a registry for. Sent to the registry as query parameters on the catalogue
/// request; a dynamic registry answers with the matching page and a <c>pagination</c> object, a
/// static one ignores query strings and the caller filters what came back.
/// </summary>
/// <param name="Query">Terms matched against name, title and description (comma-separated alternatives).</param>
/// <param name="Types">Item types to keep, as wire names (<c>registry:ui</c>, ...). Empty keeps all.</param>
/// <param name="Limit">Page size, or null for the registry's default.</param>
/// <param name="Offset">Matched items to skip, or null for none.</param>
public sealed record RegistrySearch(
    string? Query = null,
    IReadOnlyList<string>? Types = null,
    int? Limit = null,
    int? Offset = null);

/// <summary>Fetches the catalogue and individual items from a registry (remote URL or local path).</summary>
public interface IRegistryClient
{
    /// <summary>Load the full catalogue for <c>list</c>/<c>search</c>.</summary>
    Task<RegistryIndex> GetIndexAsync(CancellationToken ct = default);

    /// <summary>
    /// Ask the registry to search. The default is the full catalogue - the shape every static
    /// registry already has - and the caller detects that nothing was filtered by the missing
    /// <see cref="RegistryIndex.Pagination"/> and filters locally. A client whose registry can
    /// answer server-side overrides this and passes the search through.
    /// </summary>
    Task<RegistryIndex> SearchAsync(RegistrySearch search, CancellationToken ct = default)
        => GetIndexAsync(ct);

    /// <summary>
    /// Load one resolved item by registry name, absolute URL, or local file path.
    /// The returned item's files carry inline <see cref="RegistryFile.Content"/>.
    /// </summary>
    Task<RegistryItem> GetItemAsync(string nameOrUrlOrPath, CancellationToken ct = default);
}
