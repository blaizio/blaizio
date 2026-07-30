using System.Text.Json.Serialization;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Registry;

namespace Blaizio.Cli.Infrastructure;

/// <summary>A pipeline's detection result, for <c>tailwind detect --json</c>.</summary>
public sealed record PipelineReport(
    string Id,
    string Title,
    string Presence,
    string? Evidence,
    bool CanSetup,
    bool Recommended);

/// <summary>The outcome of <c>tailwind setup</c>, for <c>--json</c>.</summary>
public sealed record SetupReport(
    string Pipeline,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> Notes,
    string BuildHint);

/// <summary>The outcome of <c>tailwind fetch</c>, for <c>--json</c>.</summary>
public sealed record FetchReport(string Path, string Asset, long Bytes, bool FromCache, bool Sha256Verified);

/// <summary>
/// Source-generated JSON for everything a command prints to stdout under <c>--json</c>: compact
/// (one line, script-friendly), camelCase, nulls dropped. The Core shapes appear here too so the
/// STREAM format is decided in one place - <c>CoreJson</c> keeps its indented writing for files
/// on disk (blaizio.json, registry manifests), which humans read and diff.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(IReadOnlyList<PipelineReport>))]
[JsonSerializable(typeof(SetupReport))]
[JsonSerializable(typeof(FetchReport))]
[JsonSerializable(typeof(RegistryIndex))]
[JsonSerializable(typeof(RegistryItem))]
[JsonSerializable(typeof(AddResult))]
[JsonSerializable(typeof(DiffResult))]
public sealed partial class CliJson : JsonSerializerContext;
