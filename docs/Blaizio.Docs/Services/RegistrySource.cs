using System.Net;
using System.Net.Http.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Registry;

namespace Blaizio.Docs.Services;

/// <summary>
/// Read-only client for the docs site's own registry at <c>/r</c> — what the "Source" action on
/// a component page reads. The docs dogfood the real registry build, so the JSON here is exactly
/// what <c>blaizio add</c> would install: per-skin INLINED variants under <c>/r/{style}/</c>
/// (the index's <c>styles</c> list says which exist), raw v1 sources at the base path.
/// </summary>
public interface IRegistrySource
{
    /// <summary>The registry catalogue, fetched once and cached for the session; null when unreachable.</summary>
    Task<RegistryIndex?> GetIndexAsync();

    /// <summary>
    /// One item's inlined variant for <paramref name="style"/>, falling back to the base item
    /// when the registry ships no variant for that style. Null when the item doesn't exist.
    /// </summary>
    Task<RegistryItem?> GetItemAsync(string name, string style);
}

/// <inheritdoc cref="IRegistrySource" />
public sealed class RegistrySource(HttpClient http) : IRegistrySource
{
    private Task<RegistryIndex?>? _index;

    /// <inheritdoc />
    public Task<RegistryIndex?> GetIndexAsync() => _index ??= FetchIndexAsync();

    /// <inheritdoc />
    public async Task<RegistryItem?> GetItemAsync(string name, string style)
    {
        var index = await GetIndexAsync();
        if (index is null || !index.Items.Any(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)))
            return null;

        var styled = index.Styles?.Contains(style, StringComparer.OrdinalIgnoreCase) == true;
        return await FetchItemAsync(styled ? $"r/{style}/{name}.json" : $"r/{name}.json");
    }

    private async Task<RegistryIndex?> FetchIndexAsync()
    {
        try
        {
            return await http.GetFromJsonAsync("r/index.json", CoreJson.Default.RegistryIndex);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<RegistryItem?> FetchItemAsync(string url)
    {
        try
        {
            return await http.GetFromJsonAsync(url, CoreJson.Default.RegistryItem);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
