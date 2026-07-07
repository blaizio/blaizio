using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// A single installable unit in the registry: a component, lib file, theme or template,
/// together with everything needed to add it (files, NuGet packages, sibling items).
/// </summary>
public sealed class RegistryItem
{
    /// <summary>Unique registry name, e.g. <c>button</c>.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>What this item installs.</summary>
    [JsonPropertyName("type")]
    public ItemType Type { get; init; } = ItemType.Ui;

    /// <summary>Human title for gallery/search UIs.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>One-line summary for gallery/search UIs.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>NuGet package ids this item needs (installed via <c>dotnet add package</c>).</summary>
    [JsonPropertyName("nugetDependencies")]
    public IReadOnlyList<string> NugetDependencies { get => field ?? []; init; } = [];

    /// <summary>Other registry item names this item depends on; resolved transitively.</summary>
    [JsonPropertyName("registryDependencies")]
    public IReadOnlyList<string> RegistryDependencies { get => field ?? []; init; } = [];

    /// <summary>Source files carried by this item.</summary>
    [JsonPropertyName("files")]
    public IReadOnlyList<RegistryFile> Files { get => field ?? []; init; } = [];

    /// <summary>Theme token overrides contributed by this item, if any.</summary>
    [JsonPropertyName("cssVars")]
    public IReadOnlyDictionary<string, string>? CssVars { get; init; }

    /// <summary>Tailwind content globs / config fragments contributed by this item.</summary>
    [JsonPropertyName("tailwind")]
    public TailwindConfig? Tailwind { get; init; }
}

/// <summary>Tailwind hints an item contributes to the consumer project.</summary>
public sealed class TailwindConfig
{
    /// <summary>Content globs to ensure are present so the item's classes are scanned.</summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<string> Content { get => field ?? []; init; } = [];
}
