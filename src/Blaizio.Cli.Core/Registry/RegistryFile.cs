using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Registry;

/// <summary>One source file carried by a registry item.</summary>
public sealed class RegistryFile
{
    /// <summary>Path of the file relative to the registry root (e.g. <c>Ui/Button/Button.razor</c>).</summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>The item kind this file belongs to; drives where it lands in the consumer project.</summary>
    [JsonPropertyName("type")]
    public ItemType Type { get; init; } = ItemType.Ui;

    /// <summary>
    /// Inline file contents. Present in a resolved item JSON served by the registry;
    /// absent in the source manifest, where <see cref="Path"/> points at the file on disk.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Optional destination override relative to the configured output directory.
    /// When null the file lands at <see cref="Path"/> minus its item-type prefix.
    /// </summary>
    [JsonPropertyName("target")]
    public string? Target { get; init; }
}
