using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// Builds the Core service graph (project context, config, registry client) for a working directory.
/// A tiny composition root so each command doesn't repeat the wiring.
/// </summary>
public sealed class CliServices
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private CliServices(ProjectContext project, BlaizioConfig? config, IRegistryClient registry, DotnetCli dotnet)
    {
        Project = project;
        Config = config;
        Registry = registry;
        Dotnet = dotnet;
    }

    /// <summary>The discovered project.</summary>
    public ProjectContext Project { get; }

    /// <summary>The loaded config, or null when the project is not initialized.</summary>
    public BlaizioConfig? Config { get; }

    /// <summary>Registry client pointed at the config (or default) registry URL.</summary>
    public IRegistryClient Registry { get; }

    /// <summary>Dotnet SDK wrapper scoped to the project directory.</summary>
    public DotnetCli Dotnet { get; }

    /// <summary>Load config and build services for <paramref name="cwd"/>. Config may be null.</summary>
    public static async Task<CliServices> LoadAsync(string cwd, string? registryOverride = null, CancellationToken ct = default)
    {
        var project = ProjectContext.Discover(cwd);
        var config = await ConfigStore.LoadAsync(cwd, ct);
        var registryUrl = registryOverride ?? config?.Registry ?? new BlaizioConfig { Namespace = "x" }.Registry;

        // A local registry path in blaizio.json / --registry is relative to the project directory,
        // not wherever the process happens to run from.
        var isRemote = registryUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || registryUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (!isRemote)
            registryUrl = Path.GetFullPath(registryUrl, cwd);

        var registry = new RegistryClient(Http, registryUrl);
        return new CliServices(project, config, registry, new DotnetCli(cwd));
    }

    /// <summary>The config, or a clear error when the project has not been initialized.</summary>
    public BlaizioConfig RequireConfig() => Config
        ?? throw new InvalidOperationException(
            $"No {BlaizioConfig.FileName} found. Run 'blaizio init' first.");
}
