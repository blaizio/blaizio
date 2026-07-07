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
}

/// <summary>The outcome of writing one component file, for reporting and <c>--json</c>.</summary>
public sealed record WrittenFile(string RelativePath, string AbsolutePath, WriteAction Action);
