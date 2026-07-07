using Blaizio.Cli.Core.Projects;

namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>
/// The escape hatch: write the input and get out of the way. No compile step is wired; the user
/// drives Tailwind however they like. Always selectable, never auto-recommended.
/// </summary>
public sealed class NonePipeline : ITailwindPipeline
{
    /// <inheritdoc />
    public string Id => "none";

    /// <inheritdoc />
    public string Title => "None (manual)";

    /// <inheritdoc />
    public string Summary => "Write the Tailwind input only; you run Tailwind yourself.";

    /// <inheritdoc />
    public bool CanSetup => true;

    /// <inheritdoc />
    public Detection Detect(ProjectContext project) => Detection.Absent;

    /// <inheritdoc />
    public string BuildHint(ProjectContext project, TailwindPaths paths)
        => $"tailwindcss -i {paths.Input} -o {paths.Output}";

    /// <inheritdoc />
    public Task<PipelineSetupResult> SetupAsync(ProjectContext project, TailwindPaths paths, CancellationToken ct = default)
        => Task.FromResult(new PipelineSetupResult
        {
            PipelineId = Id,
            BuildHint = BuildHint(project, paths),
            Notes = ["No compile step wired. Run Tailwind with your own toolchain."],
        });
}
