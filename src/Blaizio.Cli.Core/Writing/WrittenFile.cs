using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Writing;

/// <summary>What happened to a single file during an add.</summary>
public enum WriteAction
{
    /// <summary>New file created.</summary>
    Created,

    /// <summary>Existing file replaced (overwrite was allowed).</summary>
    Overwritten,

    /// <summary>Existing file left in place (overwrite not allowed, or the user kept their edits).</summary>
    Skipped,

    /// <summary>Existing file already byte-identical to upstream - not rewritten, not a change.</summary>
    Unchanged,

    /// <summary>Planned only; nothing touched (dry run).</summary>
    Planned,

    /// <summary>Orphaned file removed (prune): no resolved item ships it anymore.</summary>
    Deleted,
}

/// <summary>The outcome of writing one component file, for reporting and <c>--json</c>.</summary>
/// <param name="Path">Destination path relative to the output directory (tokens-file overlays:
/// relative to the project root), POSIX separators - the same form the installed record uses.</param>
/// <param name="Action">What happened to the file.</param>
public sealed record WrittenFile(string Path, WriteAction Action)
{
    /// <summary>
    /// Hash of the content now on disk, when this run put it there (created / overwritten /
    /// already-identical). <see langword="null"/> for planned, skipped and deleted files - a
    /// skipped file keeps whatever baseline was recorded before. Not part of the <c>--json</c>
    /// contract: it is ledger plumbing, recorded into <c>blaizio.json</c>.
    /// </summary>
    [JsonIgnore]
    public string? Hash { get; init; }
}
