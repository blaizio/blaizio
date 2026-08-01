using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Registry;

/// <summary>The kind of thing a registry item installs.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ItemType>))]
public enum ItemType
{
    /// <summary>A styled UI component (the common case).</summary>
    [JsonStringEnumMemberName("registry:ui")]
    Ui,

    /// <summary>A shared helper/library file (e.g. the <c>cn</c> class merger).</summary>
    [JsonStringEnumMemberName("registry:lib")]
    Lib,

    /// <summary>A theme token set.</summary>
    [JsonStringEnumMemberName("registry:theme")]
    Theme,

    /// <summary>A webfont selection (heading or body) applied through the font overlay.</summary>
    [JsonStringEnumMemberName("registry:font")]
    Font,

    /// <summary>A full project template scaffolded by <c>init</c>.</summary>
    [JsonStringEnumMemberName("registry:template")]
    Template,
}
