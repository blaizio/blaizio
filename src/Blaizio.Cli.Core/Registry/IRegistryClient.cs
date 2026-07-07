namespace Blaizio.Cli.Core.Registry;

/// <summary>Fetches the catalogue and individual items from a registry (remote URL or local path).</summary>
public interface IRegistryClient
{
    /// <summary>Load the full catalogue for <c>list</c>/<c>search</c>.</summary>
    Task<RegistryIndex> GetIndexAsync(CancellationToken ct = default);

    /// <summary>
    /// Load one resolved item by registry name, absolute URL, or local file path.
    /// The returned item's files carry inline <see cref="RegistryFile.Content"/>.
    /// </summary>
    Task<RegistryItem> GetItemAsync(string nameOrUrlOrPath, CancellationToken ct = default);
}
