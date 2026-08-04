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

    /// <summary>Load config and build services for <paramref name="cwd"/>. Config may be null.
    /// <paramref name="styleOverride"/> pins the skin whose registry variants items resolve to
    /// (default: the recorded <c>theme</c>) — <c>init</c> passes the skin it just chose because
    /// its client is built before the config lands on disk.</summary>
    public static async Task<CliServices> LoadAsync(
        string cwd, string? registryOverride = null, CancellationToken ct = default, string? styleOverride = null)
    {
        var project = ProjectContext.Discover(cwd);
        var config = await ConfigStore.LoadAsync(cwd, ct);
        var registryUrl = registryOverride ?? config?.Registry ?? new BlaizioConfig { Namespace = "x" }.Registry;

        // Plain names resolve to the recorded skin's inlined variant (r/{skin}/) when the
        // registry's index ships it; the client falls back to the base path otherwise.
        var style = styleOverride ?? config?.Style;
        var fallback = new RegistryClient(Http, ResolveLocal(registryUrl, cwd), style);

        // Named registries (`registry add @ns=url`) route `@ns/item` references; wrapped even when
        // the map is empty so an unknown `@ns/...` gets the "record it first" error, not a path one.
        // Credentials are passed as a factory, not a value: a private registry's environment
        // variables are read when that registry is actually used, so recording one you have no
        // token for today does not break every command tomorrow.
        var named = new Dictionary<string, IRegistryClient>(StringComparer.OrdinalIgnoreCase);
        foreach (var (ns, source) in config?.Registries ?? [])
        {
            var recorded = source;
            var alias = ns;
            named[ns] = new RegistryClient(
                Http, ResolveLocal(recorded.Url, cwd), style,
                recorded.IsPlain ? null : () => recorded.Resolve(alias));
        }

        var registry = new NamespacedRegistryClient(fallback, named);
        return new CliServices(project, config, registry, new DotnetCli(cwd));
    }

    /// <summary>
    /// A local registry path in blaizio.json / --registry is relative to the project directory,
    /// not wherever the process happens to run from.
    /// </summary>
    private static string ResolveLocal(string registryUrl, string cwd)
    {
        var isRemote = registryUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || registryUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (isRemote)
            return registryUrl;

        // A local TEMPLATE is rooted the same way, but the placeholders have to survive the trip:
        // GetFullPath would happily normalize the braces into a folder name.
        return RegistryTemplate.IsTemplate(registryUrl)
            ? Path.Combine(cwd, registryUrl)
            : Path.GetFullPath(registryUrl, cwd);
    }

    /// <summary>The config, or a clear error when the project has not been initialized.</summary>
    public BlaizioConfig RequireConfig() => Config
        ?? throw new InvalidOperationException(
            $"No {BlaizioConfig.FileName} found. Run 'blaizio add' first.");
}
