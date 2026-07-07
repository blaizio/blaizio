using Blaizio.Cli.Core.Projects;

namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>The input/output paths a pipeline compiles between (project-relative, POSIX-style).</summary>
/// <param name="Input">Tailwind input file, e.g. <c>Styles/app.css</c>.</param>
/// <param name="Output">Compiled CSS output, e.g. <c>wwwroot/app.css</c>.</param>
public sealed record TailwindPaths(string Input, string Output);

/// <summary>The outcome of wiring a pipeline into a project.</summary>
public sealed class PipelineSetupResult
{
    /// <summary>The pipeline that ran.</summary>
    public required string PipelineId { get; init; }

    /// <summary>Files created or modified (project-relative).</summary>
    public IReadOnlyList<string> ChangedFiles { get; init; } = [];

    /// <summary>Follow-up notes for the user (install commands, manual steps).</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>The exact command that compiles CSS with this pipeline.</summary>
    public required string BuildHint { get; init; }
}

/// <summary>
/// One way to compile the (universal) Tailwind input into CSS: standalone binary, Node CLI, a Vite
/// or PostCSS bundler plugin, or an MSBuild target. The input is identical across pipelines — v4's
/// config is CSS-first — so a pipeline only has to detect itself, wire the compile step, and say how
/// to run it. Adding a new way to compile is a new implementation of this interface, nothing else.
/// </summary>
public interface ITailwindPipeline
{
    /// <summary>Stable id used by <c>--tailwind</c> and <c>tailwind setup --mode</c> (e.g. <c>standalone</c>).</summary>
    string Id { get; }

    /// <summary>Short human title for lists.</summary>
    string Title { get; }

    /// <summary>One-line description of when this pipeline fits.</summary>
    string Summary { get; }

    /// <summary>Whether <see cref="SetupAsync"/> can wire this pipeline, or it is detect-and-report only.</summary>
    bool CanSetup { get; }

    /// <summary>Probe the project for this pipeline without changing anything.</summary>
    Detection Detect(ProjectContext project);

    /// <summary>The command that compiles CSS once this pipeline is set up.</summary>
    string BuildHint(ProjectContext project, TailwindPaths paths);

    /// <summary>Wire this pipeline into the project. Idempotent. Throws when <see cref="CanSetup"/> is false.</summary>
    Task<PipelineSetupResult> SetupAsync(ProjectContext project, TailwindPaths paths, CancellationToken ct = default);
}
