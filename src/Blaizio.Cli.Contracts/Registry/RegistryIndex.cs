using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// The registry catalogue served at <c>{registry}/index.json</c>: a lightweight list of
/// every available item for <c>list</c>/<c>search</c>, without the file payloads.
/// </summary>
public sealed class RegistryIndex
{
    /// <summary>
    /// The published schema this document follows (<see cref="RegistrySchema.Registry"/>). Written
    /// by <c>generate</c> and <c>build</c> so editors can complete and validate the file; read back
    /// as an ordinary field and never acted on.
    /// </summary>
    [JsonPropertyName("$schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>Registry display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Every item, minus <see cref="RegistryItem.Files"/> content.</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<RegistryItem> Items { get => field ?? []; init; } = [];

    /// <summary>
    /// Other manifests folded into this one, as file paths relative to the manifest that lists
    /// them. A source-side field only: <c>build</c> flattens the includes away, so a served
    /// <c>index.json</c> never carries one.
    /// </summary>
    [JsonPropertyName("include")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Include { get; init; }

    /// <summary>
    /// Skins this registry ships per-skin inlined item variants for (under <c>{base}/{skin}/</c>).
    /// Null for registries without style variants - items resolve at the base path only.
    /// </summary>
    [JsonPropertyName("styles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Styles { get; init; }
}
