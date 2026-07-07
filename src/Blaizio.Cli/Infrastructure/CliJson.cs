using System.Text.Json.Serialization;

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

/// <summary>Source-generated JSON for CLI-only DTOs (the Core shapes live in <c>CoreJson</c>).</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(IReadOnlyList<PipelineReport>))]
[JsonSerializable(typeof(SetupReport))]
public sealed partial class CliJson : JsonSerializerContext;
