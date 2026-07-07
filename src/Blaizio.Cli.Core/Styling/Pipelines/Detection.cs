namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>How strongly a pipeline appears to be present in a project.</summary>
public enum PipelinePresence
{
    /// <summary>No sign of this pipeline.</summary>
    Absent,

    /// <summary>Some evidence, but not fully wired (e.g. a bundler with no Tailwind plugin yet).</summary>
    Partial,

    /// <summary>Fully present and compiling Tailwind.</summary>
    Present,
}

/// <summary>The result of probing a project for a given pipeline.</summary>
/// <param name="Presence">How present the pipeline is.</param>
/// <param name="Evidence">Human-readable reason (the file/lockfile/target that was found), or null.</param>
public sealed record Detection(PipelinePresence Presence, string? Evidence = null)
{
    /// <summary>Nothing found.</summary>
    public static Detection Absent { get; } = new(PipelinePresence.Absent);

    /// <summary>Convenience for a full match.</summary>
    public static Detection Present(string evidence) => new(PipelinePresence.Present, evidence);

    /// <summary>Convenience for a partial match.</summary>
    public static Detection Partial(string evidence) => new(PipelinePresence.Partial, evidence);
}
