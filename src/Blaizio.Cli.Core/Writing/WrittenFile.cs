namespace Blaizio.Cli.Core.Writing;

/// <summary>What happened to a single file during an add.</summary>
public enum WriteAction
{
    /// <summary>New file created.</summary>
    Created,

    /// <summary>Existing file replaced (overwrite was allowed).</summary>
    Overwritten,

    /// <summary>Existing file left in place (overwrite not allowed).</summary>
    Skipped,

    /// <summary>Planned only; nothing touched (dry run).</summary>
    Planned,

    /// <summary>Orphaned file removed (prune): no resolved item ships it anymore.</summary>
    Deleted,
}

/// <summary>The outcome of writing one component file, for reporting and <c>--json</c>.</summary>
/// <param name="Path">Destination path relative to the output directory (tokens-file overlays:
/// relative to the project root), POSIX separators - the same form the installed record uses.</param>
/// <param name="Action">What happened to the file.</param>
public sealed record WrittenFile(string Path, WriteAction Action);
