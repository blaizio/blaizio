using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Resolution;
using Blaizio.Cli.Core.Rewriting;
using Blaizio.Cli.Core.Writing;

namespace Blaizio.Cli.Core.Operations;

/// <summary>Options controlling a single <c>add</c> run.</summary>
public sealed class AddRequest
{
    /// <summary>Item names/URLs/paths to add.</summary>
    public required IReadOnlyList<string> Components { get; init; }

    /// <summary>Replace existing files instead of skipping them.</summary>
    public bool Overwrite { get; init; }

    /// <summary>Resolve and report only; write nothing and install nothing.</summary>
    public bool DryRun { get; init; }

    /// <summary>Skip NuGet installs and transitive registry dependencies.</summary>
    public bool NoDeps { get; init; }

    /// <summary>Skip only the NuGet install (keep transitive registry deps). For hosts that already
    /// declare the packages (e.g. a scaffolded template project).</summary>
    public bool NoNuget { get; init; }

    /// <summary>Namespace override (highest precedence). Null falls back to config.</summary>
    public string? NamespaceOverride { get; init; }

    /// <summary>Output directory override. Null falls back to config.</summary>
    public string? PathOverride { get; init; }
}

/// <summary>
/// Orchestrates <c>add</c>: resolve the dependency graph, install NuGet packages, copy files with
/// the namespace rewrite applied, and register the <c>@using</c>. The single entry point shared by
/// the CLI and IDE plugins; progress is surfaced through an optional <see cref="IProgress{T}"/> sink.
/// </summary>
public sealed class AddService(
    IRegistryClient registry,
    ProjectContext project,
    BlaizioConfig config,
    DotnetCli dotnet)
{
    /// <summary>Run the add. <paramref name="progress"/> receives human-readable step messages.</summary>
    public async Task<AddResult> RunAsync(
        AddRequest request,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var componentNamespace = NamespaceResolver.Resolve(request.NamespaceOverride, config, project);
        var outputDir = request.PathOverride ?? config.Output;

        progress?.Report($"Resolving {request.Components.Count} item(s)...");
        var resolver = new DependencyResolver(registry);
        var graph = request.NoDeps
            ? await ResolveShallowAsync(request.Components, ct)
            : await resolver.ResolveAsync(request.Components, ct);

        if (!request.NoDeps && !request.NoNuget && !request.DryRun && graph.NugetPackages.Count > 0)
        {
            if (project.CsprojPath is null)
            {
                progress?.Report("No .csproj found — skipping NuGet install.");
            }
            else
            {
                progress?.Report($"Installing {graph.NugetPackages.Count} NuGet package(s)...");
                var install = await dotnet.AddPackagesAsync(graph.NugetPackages, ct);
                if (!install.Success)
                    throw new InvalidOperationException(
                        $"'dotnet add package' failed:{Environment.NewLine}{install.ErrorText}");
            }
        }

        var rewriter = new NamespaceRewriter(componentNamespace);
        var writer = new ComponentWriter(project.ProjectDir, outputDir, rewriter);

        var files = new List<WrittenFile>();
        var perItem = new Dictionary<string, IReadOnlyList<WrittenFile>>();
        foreach (var item in graph.Items)
        {
            progress?.Report($"{(request.DryRun ? "Planning" : "Writing")} {item.Name}...");
            var written = await writer.WriteAsync(item, request.Overwrite, request.DryRun, ct);
            perItem[item.Name] = written;
            files.AddRange(written);
        }

        var importsUpdated = false;
        if (!request.DryRun && files.Any(f => f.Action is WriteAction.Created or WriteAction.Overwritten))
        {
            // Copied components reference the styled namespace AND the headless Base layer
            // (Blaze* primitives), so both @usings must be present for them to compile.
            var componentUsing = await ImportsUpdater.EnsureUsingAsync(project.ProjectDir, componentNamespace, ct);
            var baseNamespace = config.Aliases.TryGetValue("base", out var b) && !string.IsNullOrWhiteSpace(b) ? b : "Blaizio";
            var baseUsing = await ImportsUpdater.EnsureUsingAsync(project.ProjectDir, baseNamespace, ct);
            // Copied .cs files don't see _Imports.razor and no longer nest under Blaizio after the
            // rewrite, so emit a project-wide global using for the Base/Icons namespace.
            var globalUsing = await GlobalUsingsWriter.EnsureAsync(project.ProjectDir, outputDir, baseNamespace, ct);
            importsUpdated = componentUsing || baseUsing || globalUsing;
        }

        if (!request.DryRun)
        {
            // Record what's installed so `update` (no args) and `diff` know the project's contents.
            foreach (var (name, written) in perItem)
                config.Installed[name] = new InstalledItem
                {
                    Files = [.. written.Select(f => f.RelativePath.Replace('\\', '/'))],
                };
            await ConfigStore.SaveAsync(project.ProjectDir, config, ct);
        }

        return new AddResult
        {
            Items = [.. graph.Items.Select(i => i.Name)],
            NugetPackages = graph.NugetPackages,
            Files = files,
            Namespace = componentNamespace,
            ImportsUpdated = importsUpdated,
            DryRun = request.DryRun,
        };
    }

    /// <summary>Fetch only the requested items, ignoring their registry dependencies (<c>--no-deps</c>).</summary>
    private async Task<ResolvedGraph> ResolveShallowAsync(
        IReadOnlyList<string> components,
        CancellationToken ct)
    {
        var items = new List<RegistryItem>(components.Count);
        foreach (var reference in components)
            items.Add(await registry.GetItemAsync(reference, ct));

        return new ResolvedGraph
        {
            Items = items,
            NugetPackages = [],
            Requested = components,
        };
    }
}
