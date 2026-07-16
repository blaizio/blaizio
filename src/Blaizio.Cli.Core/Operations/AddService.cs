using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Resolution;
using Blaizio.Cli.Core.Rewriting;
using Blaizio.Cli.Core.Styling;
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

    /// <summary>
    /// Delete files under the output directory that no resolved item ships (anymore), so files
    /// removed upstream don't linger as orphans. Only meaningful when the resolved graph covers the
    /// whole registry (<c>--all</c>) — a partial add doesn't know the full expected file set.
    /// </summary>
    public bool Prune { get; init; }

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
                // Ledger the ids this run actually introduces (pre-existing references are
                // user-owned) so uninstall can undo exactly them.
                var preExisting = PackageLedger.PreExisting(project.CsprojPath, graph.NugetPackages);
                var install = await dotnet.AddPackagesAsync(graph.NugetPackages, progress, ct);
                if (!install.Success)
                    throw new InvalidOperationException(
                        $"'dotnet add package' failed:{Environment.NewLine}{install.ErrorText}");
                PackageLedger.Record(config, graph.NugetPackages, preExisting);
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

        // Font items copy no files - they re-style. Each one records its half of the selection
        // (heading or body) in blaizio.json, then the recorded pair is patched into the tokens
        // file (--font-heading / html font-family) and the Google Fonts stylesheet is (re)wired
        // as the marked host link. Adding a font is explicit intent, so this bypasses the
        // user-font detection a preset apply would run.
        var fontItems = graph.Items.Where(i => i.Type == ItemType.Font).ToList();
        if (fontItems.Count > 0)
        {
            foreach (var item in fontItems)
            {
                var spec = item.Font
                    ?? throw new InvalidOperationException($"Font item '{item.Name}' carries no font payload.");
                if (FontCatalog.Find(spec.Name) is null)
                    throw new InvalidOperationException(
                        $"Unknown font '{spec.Name}' (item '{item.Name}'). Update the Blaizio CLI: dotnet tool update -g Blaizio.Cli");
                if (spec.Heading)
                    config.Heading = spec.Name;
                else
                    config.Font = spec.Name;
            }

            var tokensRel = config.Css ?? Path.Combine(TailwindSetup.StylesDir, TailwindSetup.InputName);
            var tokensAbs = Path.GetFullPath(Path.Combine(project.ProjectDir, tokensRel));
            if (request.DryRun)
            {
                files.Add(new WrittenFile(tokensRel.Replace('\\', '/'), tokensAbs, WriteAction.Planned));
            }
            else
            {
                progress?.Report("Applying fonts...");
                var heading = config.Heading ?? "default";
                var body = config.Font ?? "default";
                var patched = await TailwindSetup.EnsureFontsAsync(project.ProjectDir, heading, body, config.Css, ct);
                if (!patched.Patched)
                    throw new InvalidOperationException(
                        $"No tokens file at '{tokensRel.Replace('\\', '/')}' to patch the fonts into. Run 'blaizio init' first.");
                await new HostPageSetup().EnsureFontLinkAsync(project.ProjectDir, FontCatalog.CssUrl(heading, body), ct);
                files.Add(new WrittenFile(tokensRel.Replace('\\', '/'), tokensAbs, WriteAction.Overwritten));
            }
        }

        if (request.Prune)
        {
            progress?.Report("Pruning orphaned files...");
            files.AddRange(Prune(graph.Items, Path.Combine(project.ProjectDir, outputDir), request.DryRun));
        }

        var importsUpdated = false;
        // Component writes only (perItem) - the font overlay landing in `files` is styling, and a
        // font-only add must not touch _Imports.razor.
        if (!request.DryRun && perItem.Values.SelectMany(w => w)
                .Any(f => f.Action is WriteAction.Created or WriteAction.Overwritten))
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
            // A prune covers the whole registry, so items no longer in it are gone from disk too.
            if (request.Prune)
                foreach (var stale in config.Installed.Keys.Where(k => !perItem.ContainsKey(k)).ToList())
                    config.Installed.Remove(stale);
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

    /// <summary>
    /// Delete files under <paramref name="outputRoot"/> that no resolved item ships and that aren't
    /// CLI-owned (the generated global-usings file), then drop directories left empty. Comparing
    /// against the resolved graph — not <c>blaizio.json</c> — keeps this correct even when the
    /// recorded state and the on-disk copy have drifted apart.
    /// </summary>
    private static List<WrittenFile> Prune(IReadOnlyList<RegistryItem> items, string outputRoot, bool dryRun)
    {
        var results = new List<WrittenFile>();
        var root = Path.GetFullPath(outputRoot);
        if (!Directory.Exists(root))
            return results;

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var expected = new HashSet<string>(comparer) { GlobalUsingsWriter.FileName };
        foreach (var item in items)
            foreach (var file in item.Files)
                expected.Add(ComponentWriter.DestinationFor(file));

        foreach (var absolute in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, absolute);
            if (expected.Contains(relative))
                continue;
            if (!dryRun)
                File.Delete(absolute);
            results.Add(new WrittenFile(relative, absolute, WriteAction.Deleted));
        }

        if (!dryRun)
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);

        return results;
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
