using Blaizio.Cli.Core.Projects;

namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>A pipeline paired with what detection found for it.</summary>
/// <param name="Pipeline">The pipeline.</param>
/// <param name="Detection">Its detection result in the current project.</param>
public sealed record PipelineProbe(ITailwindPipeline Pipeline, Detection Detection);

/// <summary>
/// The set of known Tailwind pipelines. Probes a project against all of them, resolves one by id,
/// and recommends the best fit for <c>auto</c> mode — preferring a pipeline the project already has
/// (never clobber), and falling back to the zero-Node standalone setup when nothing is present.
/// </summary>
public sealed class TailwindPipelineRegistry
{
    // Listed in preference order: an existing bundler wins over Node, Node over standalone.
    private readonly ITailwindPipeline[] _pipelines =
    [
        new VitePipeline(),
        new PostcssPipeline(),
        new NodePipeline(),
        new StandalonePipeline(),
        new NonePipeline(),
    ];

    /// <summary>Every known pipeline, in preference order.</summary>
    public IReadOnlyList<ITailwindPipeline> All => _pipelines;

    /// <summary>Resolve a pipeline by its <see cref="ITailwindPipeline.Id"/> (case-insensitive), or null.</summary>
    public ITailwindPipeline? Resolve(string id)
        => _pipelines.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Probe the project against every pipeline.</summary>
    public IReadOnlyList<PipelineProbe> DetectAll(ProjectContext project)
        => [.. _pipelines.Select(p => new PipelineProbe(p, p.Detect(project)))];

    /// <summary>
    /// The best pipeline for <c>auto</c>: the highest-preference one that is already Present, else the
    /// highest-preference Partial that can be set up, else the standalone default.
    /// </summary>
    public ITailwindPipeline Recommend(ProjectContext project)
    {
        var probes = DetectAll(project);

        var present = probes.FirstOrDefault(p => p.Detection.Presence == PipelinePresence.Present);
        if (present is not null)
            return present.Pipeline;

        var partial = probes.FirstOrDefault(p =>
            p.Detection.Presence == PipelinePresence.Partial && p.Pipeline.CanSetup);
        if (partial is not null)
            return partial.Pipeline;

        return Resolve("standalone")!;
    }
}
