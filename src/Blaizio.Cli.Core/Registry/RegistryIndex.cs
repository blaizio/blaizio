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

    /// <summary>
    /// Skins this registry ships per-skin inlined item variants for (under <c>{base}/{skin}/</c>).
    /// Null for registries without style variants - items resolve at the base path only.
    /// </summary>
    [JsonPropertyName("styles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Styles { get; init; }
}
