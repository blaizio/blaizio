using System.Text.Json.Serialization;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Registry;

namespace Blaizio.Cli.Core;

/// <summary>
/// Source-generated JSON context for all Core (de)serialization: the on-disk config, registry
/// payloads, and the CLI's <c>--json</c> result shapes. One place keeps them in lockstep and
/// keeps the tool trim/AOT-friendly.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BlaizioConfig))]
[JsonSerializable(typeof(RegistryItem))]
[JsonSerializable(typeof(RegistryIndex))]
[JsonSerializable(typeof(RegistryFile))]
[JsonSerializable(typeof(AddResult))]
[JsonSerializable(typeof(DiffResult))]
public sealed partial class CoreJson : JsonSerializerContext;
