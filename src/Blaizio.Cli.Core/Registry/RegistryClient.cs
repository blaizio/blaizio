using System.Net.Http.Json;
using System.Text.Json;

namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// Default <see cref="IRegistryClient"/>. Resolves items against a base registry that is either
/// an <c>http(s)</c> URL or a local directory, and also accepts a fully-qualified URL or file path
/// as the item reference (so <c>add ./my-item.json</c> and <c>add https://.../x.json</c> work).
/// </summary>
public sealed class RegistryClient(HttpClient http, string baseRegistry) : IRegistryClient
{
    private readonly bool _remote =
        baseRegistry.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        baseRegistry.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<RegistryIndex> GetIndexAsync(CancellationToken ct = default)
        => await ReadAsync(Combine("index.json"), CoreJson.Default.RegistryIndex, ct);

    /// <inheritdoc />
    public async Task<RegistryItem> GetItemAsync(string nameOrUrlOrPath, CancellationToken ct = default)
    {
        var location = IsQualified(nameOrUrlOrPath)
            ? nameOrUrlOrPath
            : Combine($"{nameOrUrlOrPath}.json");
        return await ReadAsync(location, CoreJson.Default.RegistryItem, ct);
    }

    private static bool IsQualified(string reference) =>
        reference.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        reference.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
        Path.IsPathRooted(reference);

    private string Combine(string leaf) =>
        _remote ? $"{baseRegistry.TrimEnd('/')}/{leaf}" : Path.Combine(baseRegistry, leaf);

    private async Task<T> ReadAsync<T>(
        string location,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken ct)
    {
        var isHttp =
            location.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            location.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (isHttp)
            {
                var result = await http.GetFromJsonAsync(location, typeInfo, ct);
                return result ?? throw Malformed(location);
            }

            await using var stream = File.OpenRead(location);
            var local = await JsonSerializer.DeserializeAsync(stream, typeInfo, ct);
            return local ?? throw Malformed(location);
        }
        catch (HttpRequestException ex)
        {
            throw new RegistryException($"Could not reach the registry at '{location}'.", ex);
        }
        catch (FileNotFoundException ex)
        {
            throw new RegistryException($"Registry file not found: '{location}'.", ex);
        }
        catch (JsonException ex)
        {
            throw new RegistryException($"Registry response at '{location}' was not valid JSON.", ex);
        }
    }

    private static RegistryException Malformed(string location)
        => new($"Registry response at '{location}' was empty.");
}

/// <summary>Raised when the registry cannot be reached or returns unusable data.</summary>
public sealed class RegistryException(string message, Exception? inner = null)
    : Exception(message, inner);
