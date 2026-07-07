using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// The registry catalogue served at <c>{registry}/index.json</c>: a lightweight list of
/// every available item for <c>list</c>/<c>search</c>, without the file payloads.
/// </summary>
public sealed class RegistryIndex
{
    /// <summary>Registry display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Every item, minus <see cref="RegistryItem.Files"/> content.</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<RegistryItem> Items { get => field ?? []; init; } = [];
}
