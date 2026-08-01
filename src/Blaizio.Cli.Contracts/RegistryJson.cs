using System.Text.Json.Serialization;
using Blaizio.Cli.Core.Registry;

namespace Blaizio.Cli.Core;

/// <summary>
/// Source-generated JSON context for the registry wire model - the read side shared with the
/// docs site (index.json / item payloads). The CLI's own <c>CoreJson</c> re-declares these types
/// alongside its config and result shapes; every property here carries an explicit
/// <see cref="JsonPropertyNameAttribute"/>, so the two contexts cannot drift on naming.
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RegistryIndex))]
[JsonSerializable(typeof(RegistryItem))]
[JsonSerializable(typeof(RegistryFile))]
[JsonSerializable(typeof(FontSpec))]
public sealed partial class RegistryJson : JsonSerializerContext;
