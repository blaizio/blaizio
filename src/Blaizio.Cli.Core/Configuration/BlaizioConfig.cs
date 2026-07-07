using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Configuration;

/// <summary>
/// The <c>blaizio.json</c> written into a consumer project by <c>init</c> and read by every
/// later command. Records where components land, their namespace, theme and the registry URL.
/// </summary>
public sealed class BlaizioConfig
{
    /// <summary>The file name looked up at the project root.</summary>
    public const string FileName = "blaizio.json";

    /// <summary>JSON schema URL for editor completion.</summary>
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "https://blaiz.io/schema.json";

    /// <summary>Root namespace applied to every copied component.</summary>
    [JsonPropertyName("namespace")]
    public required string Namespace { get; set; }

    /// <summary>Directory (relative to the project) copied components are written into.</summary>
    [JsonPropertyName("output")]
    public string Output { get; set; } = "Components/Ui";

    /// <summary>Active theme token set name.</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "default";

    /// <summary>Whether RTL support was wired up at init.</summary>
    [JsonPropertyName("rtl")]
    public bool Rtl { get; set; }

    /// <summary>Base registry URL (or local path) items are fetched from.</summary>
    [JsonPropertyName("registry")]
    public string Registry { get; set; } = "https://blaiz.io/r";

    /// <summary>Namespace aliases; <c>ui</c> mirrors <see cref="Namespace"/>, <c>base</c> is the headless layer.</summary>
    /// <remarks>Null-tolerant: a hand-edited <c>"aliases": null</c> must not crash later commands.</remarks>
    [JsonPropertyName("aliases")]
    public Dictionary<string, string> Aliases
    {
        get => field;
        set => field = value ?? [];
    } = new()
    {
        ["base"] = "Blaizio",
    };
}
