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

    /// <summary>
    /// Replace existing files instead of skipping them. Files the user changed since install are
    /// still protected: they go to <see cref="ResolveConflicts"/> (or are kept when there is no
    /// resolver), unless <see cref="Force"/> is set.
    /// </summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Replace even the files the user edited, with no prompt. The explicit "throw my changes
    /// away" switch; without it an unattended run always keeps local edits.
    /// </summary>
    public bool Force { get; init; }

    /// <summary>
    /// Asked to decide which of the edited items may be overwritten, before anything is written;
    /// returns the item names that may. <see langword="null"/> (an unattended run) keeps every
    /// edited file - the safe default, since nobody is there to be asked.
    /// </summary>
    public Func<IReadOnlyList<EditedItem>, CancellationToken, Task<IReadOnlySet<string>>>? ResolveConflicts { get; init; }

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

    /// <summary>
    /// Go on when a requested item cannot be found: it is reported in <see cref="AddResult.Skipped"/>
    /// instead of failing the run. For <c>update</c>, where one stale ledger entry must not block
    /// every other component's refresh. A found item whose dependency is missing is skipped too -
    /// it is not installable - and an unreachable registry still fails.
    /// </summary>
    public bool SkipMissing { get; init; }

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
    /// <summary>
    /// Run the add. <paramref name="progress"/> receives human-readable step messages.
    /// <para>
    /// Mutations are transactional: inputs are validated before anything is touched, every file is
    /// snapshotted before its first write, NuGet packages install only after the files landed, and
    /// <c>blaizio.json</c> is saved last. Any failure (including cancellation) rolls the files back
    /// and uninstalls the packages this run introduced, so a failed add leaves either the original
    /// project or - if a file could not be restored - an explicit list of what to check.
    /// </para>
    /// </summary>
    public async Task<AddResult> RunAsync(
        AddRequest request,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var componentNamespace = NamespaceResolver.Resolve(request.NamespaceOverride, config, project);
        var outputDir = request.PathOverride ?? config.Output;

        progress?.Report($"Resolving {request.Components.Count} item(s)...");
        // Recorded pins ride into dependency resolution: a dep landing on a pinned name fetches
        // that pin instead of floating past it. Requested names stay literal (add button = unpin).
        var pins = config.Installed
            .Where(kv => kv.Value.Pin is not null)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Pin!, StringComparer.OrdinalIgnoreCase);
        var resolver = new DependencyResolver(registry, pins);
        var graph = request.NoDeps
            ? await ResolveShallowAsync(request.Components, ct)
            : await resolver.ResolveAsync(request.Components, ct, request.SkipMissing);

        // Validate every payload BEFORE the first mutation, so bad input fails with the project
        // untouched instead of after packages and files already landed.
        foreach (var item in graph.Items.Where(i => i.Type == ItemType.Font))
        {
            var spec = item.Font
                ?? throw new InvalidOperationException($"Font item '{item.Name}' carries no font payload.");
            if (FontCatalog.Find(spec.Name) is null)
                throw new InvalidOperationException(
                    $"Unknown font '{spec.Name}' (item '{item.Name}'). Update the Blaizio CLI: dotnet tool update -g Blaizio.Cli");
        }
        foreach (var item in graph.Items.Where(i => i.Type == ItemType.Theme))
            if (item.CssVars is not { IsEmpty: false })
                throw new InvalidOperationException($"Theme item '{item.Name}' carries no cssVars payload.");
        foreach (var item in graph.Items)
            foreach (var file in item.Files.Where(f => f.Type == FileType.File && string.IsNullOrEmpty(f.Target)))
                throw new InvalidOperationException(
                    $"Item '{item.Name}' file '{file.Path}' is registry:file but has no target. "
                    + "A loose file must say where it lands (e.g. \"~/wwwroot/robots.txt\").");
        // An item calling into a Base capability the project's pinned Blaizio.Base predates must
        // stop here: the unpinned install below would skip the already-referenced package without
        // a version look, and the component's JS would 404 at runtime.
        if (BaseVersionGuard.Check(
                graph.Items,
                dotnet.ExistingReferences().GetValueOrDefault(BaseVersionGuard.BasePackageId)) is { } tooOld)
            throw new InvalidOperationException(tooOld);

        var tx = request.DryRun ? null : new AddTransaction();
        try
        {
            return await ApplyAsync(request, graph, componentNamespace, outputDir, tx, progress, ct);
        }
        catch (Exception ex)
        {
            if (tx is null)
                throw;

            // Put the project back: restore or delete every touched file, uninstall the packages
            // this run introduced. CancellationToken.None throughout - the rollback must complete
            // even when the failure IS a cancellation.
            var unrestored = await tx.RollbackFilesAsync(CancellationToken.None);
            foreach (var id in tx.IntroducedPackages)
            {
                try
                {
                    await dotnet.RemovePackageAsync(id, CancellationToken.None);
                }
                catch (Exception)
                {
                    unrestored = [.. unrestored, $"package {id}"];
                }
            }

            var detail = unrestored.Count == 0
                ? "the project was restored to its pre-add state"
                : $"rollback could not restore: {string.Join(", ", unrestored)}";
            throw new InvalidOperationException($"add failed ({detail}): {ex.Message}", ex);
        }
    }

    private async Task<AddResult> ApplyAsync(
        AddRequest request,
        ResolvedGraph graph,
        string componentNamespace,
        string outputDir,
        AddTransaction? tx,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        // One writer per registry namespace: a namespaced registry's items nest under their own
        // subfolder (and namespace segment), so @acme/button never collides with the default
        // registry's button on disk or in C#.
        var writers = new Dictionary<string, ComponentWriter>(StringComparer.Ordinal);
        ComponentWriter WriterFor(string? sourceNamespace)
        {
            var folder = ComponentWriter.FolderFor(sourceNamespace);
            if (!writers.TryGetValue(folder ?? "", out var writer))
                writers[folder ?? ""] = writer = new ComponentWriter(
                    project.ProjectDir,
                    outputDir,
                    new NamespaceRewriter(folder is null ? componentNamespace : $"{componentNamespace}.{folder}"),
                    folder);
            return writer;
        }

        // Before the first write: which local files would an overwrite destroy? Only files that
        // BOTH differ from the baseline recorded at install time and carry an incoming upstream
        // change count - anything else is either untouched or already up to date. A run that is
        // not overwriting skips existing files anyway, but the scan still runs, so the report can
        // say WHY a file was skipped (your edits) instead of leaving a silent grey line.
        var edited = await LocalEdits.ScanAsync(graph.Items, i => WriterFor(i.SourceNamespace), config, ct);

        // --force takes upstream everywhere; otherwise the caller decides (the CLI shows a picker),
        // and an unattended run with no resolver keeps every edit.
        var approved = request.Force
            ? edited.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : request is { ResolveConflicts: { } resolve, DryRun: false } && edited.Count > 0
                ? await resolve(edited, ct)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // "Kept" only means something when the run WANTED to replace them; a plain add skips every
        // existing file by definition and has nothing to report as a decision.
        var keptLocal = request.Overwrite
            ? edited.Where(e => !approved.Contains(e.Name)).Select(e => e.Name).ToList()
            : [];
        var protectedFiles = edited
            .Where(e => !approved.Contains(e.Name))
            .ToDictionary(
                e => e.Name,
                e => (IReadOnlySet<string>)e.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase);

        var files = new List<WrittenFile>();
        var perItem = new Dictionary<string, IReadOnlyList<WrittenFile>>();
        foreach (var item in graph.Items)
        {
            progress?.Report($"{(request.DryRun ? "Planning" : "Writing")} {item.QualifiedName}...");
            var written = await WriterFor(item.SourceNamespace)
                .WriteAsync(item, request.Overwrite, request.DryRun, tx is null ? null : tx.SnapshotFile,
                    protectedFiles.GetValueOrDefault(item.QualifiedName), ct);
            perItem[item.QualifiedName] = written;
            files.AddRange(written);
        }

        // Files an item shipped last time but doesn't anymore - an upstream rename or split leaves
        // the old copy on disk, where it still compiles and the type it declares still resolves, so
        // the consumer meets it as "cannot convert X to Y" at some call site instead of a missing
        // file. They are ours by record (blaizio.json listed them), so an overwrite takes them back
        // out. A plain add rewrites nothing and so removes nothing either.
        var leftBehind = new List<string>();
        if (request.Overwrite)
            files.AddRange(await SweepOrphansAsync(
                graph, perItem, request, outputDir, leftBehind, tx, ct));

        // Font items copy no files - they re-style. Each one records its half of the selection
        // (heading or body) in blaizio.json, then the recorded pair is patched into the tokens
        // file (--font-heading / html font-family) and the Google Fonts stylesheet is (re)wired
        // as the marked host link. Adding a font is explicit intent, so this bypasses the
        // user-font detection a preset apply would run. (Payloads were validated up front.)
        var fontItems = graph.Items.Where(i => i.Type == ItemType.Font).ToList();
        if (fontItems.Count > 0)
        {
            foreach (var item in fontItems)
            {
                var spec = item.Font!;
                if (spec.Heading)
                    config.Heading = spec.Name;
                else
                    config.Font = spec.Name;
            }

            var tokensRel = config.Css ?? Path.Combine(TailwindSetup.StylesDir, TailwindSetup.InputName);
            if (request.DryRun)
            {
                files.Add(new WrittenFile(tokensRel.Replace('\\', '/'), WriteAction.Planned));
            }
            else
            {
                progress?.Report("Applying fonts...");
                tx!.SnapshotFile(Path.Combine(project.ProjectDir, tokensRel));
                foreach (var page in HostPageSetup.CandidatePages)
                    tx.SnapshotFile(Path.Combine(project.ProjectDir, page));
                var heading = config.Heading ?? "default";
                var body = config.Font ?? "default";
                var patched = await TailwindSetup.EnsureFontsAsync(project.ProjectDir, heading, body, config.Css, ct: ct);
                if (!patched.Patched)
                    throw new InvalidOperationException(
                        $"No tokens file at '{tokensRel.Replace('\\', '/')}' to patch the fonts into. Run 'blaizio add' first.");
                await new HostPageSetup().EnsureFontLinkAsync(project.ProjectDir, FontCatalog.CssUrl(heading, body), ct);
                files.Add(new WrittenFile(tokensRel.Replace('\\', '/'), WriteAction.Overwritten));
            }
        }

        // Theme items copy no files either - their cssVars payload is patched into the tokens
        // file (light -> :root, dark -> .dark), leaving every other declaration alone. The item
        // still lands in the installed record so remove/uninstall know about it; restoring the
        // stock look afterwards is `blaizio apply <preset>`.
        var themeItems = graph.Items.Where(i => i.Type == ItemType.Theme).ToList();
        if (themeItems.Count > 0)
        {
            var tokensRel = config.Css ?? Path.Combine(TailwindSetup.StylesDir, TailwindSetup.InputName);
            foreach (var item in themeItems)
            {
                var vars = item.CssVars!;

                if (request.DryRun)
                {
                    files.Add(new WrittenFile(tokensRel.Replace('\\', '/'), WriteAction.Planned));
                    continue;
                }

                progress?.Report($"Applying theme {item.Name}...");
                tx!.SnapshotFile(Path.Combine(project.ProjectDir, tokensRel));
                var patched = await TailwindSetup.EnsureCssVarsAsync(project.ProjectDir, vars, config.Css, ct);
                if (!patched.Patched)
                    throw new InvalidOperationException(
                        $"No tokens file at '{tokensRel.Replace('\\', '/')}' to patch the theme into. Run 'blaizio add' first.");
                files.Add(new WrittenFile(tokensRel.Replace('\\', '/'), WriteAction.Overwritten));
            }
        }

        // Items shipping css blocks (@keyframes, @utility, @layer rules) get a managed, item-keyed
        // region in the tokens file - replaced on re-install, stripped by remove/uninstall. A
        // project without a tokens file (--tailwind none) skips them with a note: the component's
        // files are still fully usable, only its extra CSS has nowhere managed to live.
        var cssItems = graph.Items.Where(i => i.Css is { Count: > 0 }).ToList();
        var cssWritten = new HashSet<string>(StringComparer.Ordinal);
        if (cssItems.Count > 0)
        {
            var tokensRel = config.Css ?? Path.Combine(TailwindSetup.StylesDir, TailwindSetup.InputName);
            var tokensAbs = Path.Combine(project.ProjectDir, tokensRel);
            var tokensPosix = tokensRel.Replace('\\', '/');
            if (!File.Exists(tokensAbs))
            {
                progress?.Report($"No tokens file at '{tokensPosix}' - the items' css blocks were skipped.");
            }
            else
            {
                foreach (var item in cssItems)
                {
                    if (request.DryRun)
                    {
                        files.Add(new WrittenFile(tokensPosix, WriteAction.Planned));
                        continue;
                    }

                    progress?.Report($"Writing {item.QualifiedName}'s css blocks...");
                    tx!.SnapshotFile(tokensAbs);
                    var css = await File.ReadAllTextAsync(tokensAbs, ct);
                    await File.WriteAllTextAsync(tokensAbs, ItemCssRegions.Apply(css, item.QualifiedName, item.Css!), ct);
                    files.Add(new WrittenFile(tokensPosix, WriteAction.Overwritten));
                    cssWritten.Add(item.QualifiedName);
                }
            }
        }

        if (request.Prune)
        {
            progress?.Report("Pruning orphaned files...");
            files.AddRange(Prune(graph.Items, config, Path.Combine(project.ProjectDir, outputDir),
                ComponentWriter.PagesDirFor(project.ProjectDir), request.DryRun,
                tx is null ? null : tx.SnapshotFile));
        }

        var importsUpdated = false;
        // Component writes only (perItem) - the font overlay landing in `files` is styling, and a
        // font-only add must not touch _Imports.razor.
        if (!request.DryRun && perItem.Values.SelectMany(w => w)
                .Any(f => f.Action is WriteAction.Created or WriteAction.Overwritten or WriteAction.Unchanged))
        {
            tx!.SnapshotFile(Path.Combine(project.ProjectDir, "_Imports.razor"));
            tx.SnapshotFile(Path.Combine(project.ProjectDir, outputDir, GlobalUsingsWriter.FileName));
            // Copied components reference the styled namespace AND the headless Base layer
            // (Blaze* primitives), so both @usings must be present for them to compile.
            var componentUsing = await ImportsUpdater.EnsureUsingAsync(project.ProjectDir, componentNamespace, ct);
            var baseNamespace = config.Aliases.TryGetValue("base", out var b) && !string.IsNullOrWhiteSpace(b) ? b : "Blaizio";
            var baseUsing = await ImportsUpdater.EnsureUsingAsync(project.ProjectDir, baseNamespace, ct);
            // Copied .cs files don't see _Imports.razor and no longer nest under Blaizio after the
            // rewrite, so emit a project-wide global using for the Base/Icons namespace.
            var globalUsing = await GlobalUsingsWriter.EnsureAsync(project.ProjectDir, outputDir, baseNamespace, ct);
            importsUpdated = componentUsing || baseUsing || globalUsing;
            // Namespaced items live one namespace segment down; their @using rides along too.
            foreach (var folder in graph.Items
                         .Select(i => ComponentWriter.FolderFor(i.SourceNamespace))
                         .OfType<string>()
                         .Distinct(StringComparer.Ordinal))
                importsUpdated |= await ImportsUpdater.EnsureUsingAsync(
                    project.ProjectDir, $"{componentNamespace}.{folder}", ct);
        }

        // NuGet install runs AFTER the files committed: a failed install then rolls back cleanly
        // copied files, instead of a failed copy leaving packages behind.
        var packages = (IReadOnlyList<NugetDependency>)[.. graph.NugetPackages, .. graph.DevNugetPackages];
        if (!request.NoDeps && !request.NoNuget && !request.DryRun && packages.Count > 0)
        {
            if (project.CsprojPath is null)
            {
                progress?.Report("No .csproj found - skipping NuGet install.");
            }
            else
            {
                // Ledger the ids this run actually introduces (pre-existing references are
                // user-owned) so uninstall - and this run's rollback - can undo exactly them.
                // The ledger works in bare ids: uninstall removes a package, not a version.
                var preExisting = PackageLedger.PreExisting(project.CsprojPath, packages.Select(d => d.Id));
                tx!.SnapshotFile(project.CsprojPath);
                tx.RecordPackages(packages.Select(d => d.Id).Where(id => !preExisting.Contains(id)));
                var install = await dotnet.AddPackagesAsync(
                    packages.Select(d => (d.Id, d.Version)), progress, ct);
                if (!install.Success)
                    throw new InvalidOperationException(
                        $"'dotnet add package' failed:{Environment.NewLine}{install.ErrorText}");
                // Dev-only packages must not flow to the app's own consumers. Only the references
                // THIS run introduced are marked - a pre-existing one is the user's to shape.
                dotnet.MarkPrivateAssets(
                    graph.DevNugetPackages.Select(d => d.Id).Where(id => !preExisting.Contains(id)));
                PackageLedger.Record(config, packages.Select(d => d.Id), preExisting);
            }
        }

        if (!request.DryRun)
        {
            // Record what's installed so `update` (no args) and `diff` know the project's contents.
            // Dependencies ride along so remove's guard has an offline graph to consult, and every
            // file this run put on disk records its hash as the new baseline. A file we did NOT
            // write (skipped - it existed, or the user kept their edits) keeps whatever baseline
            // it had: overwriting it here would silently declare their working copy pristine.
            foreach (var item in graph.Items)
            {
                var written = perItem[item.QualifiedName];
                config.Installed.TryGetValue(item.QualifiedName, out var prior);
                // A file the item stopped shipping but that stayed on disk keeps its record, so
                // uninstall still knows the CLI put it there. Deleted orphans just drop out.
                var stranded = prior?.Files.Where(f => leftBehind.Contains(f.Path)) ?? [];
                config.Installed[item.QualifiedName] = new InstalledItem
                {
                    Files = [
                        .. written.Select(f => new InstalledFile(f.Path, f.Hash ?? prior?.HashFor(f.Path))),
                        .. stranded],
                    Dependencies = [.. item.RegistryDependencies],
                    Version = item.Version,
                    Pin = item.RequestedVersion,
                    // A direct install remembers where it came from, so update goes back there.
                    // Re-adding by plain name from the default registry clears it - that IS the
                    // source now.
                    Source = item.SourceReference,
                    // Keep a prior region on record when this run could not rewrite it (no tokens
                    // file today) - the region may still sit in a file recorded earlier.
                    Css = cssWritten.Contains(item.QualifiedName) || (prior?.Css ?? false),
                };
            }
            // A prune covers the whole DEFAULT registry, so its items no longer in it are gone
            // from disk too. Namespaced records belong to other registries, and a record with a
            // source came from a file, a URL or a repository - never the default registry's scope.
            if (request.Prune)
                foreach (var stale in config.Installed
                             .Where(kv => !kv.Key.StartsWith('@') && kv.Value.Source is null && !perItem.ContainsKey(kv.Key))
                             .Select(kv => kv.Key).ToList())
                    config.Installed.Remove(stale);
            await ConfigStore.SaveAsync(project.ProjectDir, config, ct);
        }

        return new AddResult
        {
            Items = [.. graph.Items.Select(i => i.QualifiedName)],
            NugetPackages = [.. graph.NugetPackages.Select(d => d.ToString())],
            DevNugetPackages = [.. graph.DevNugetPackages.Select(d => d.ToString())],
            DocsNotes = [.. graph.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.Docs))
                .Select(i => new ItemDoc(i.QualifiedName, i.Docs!))],
            Files = files,
            Namespace = componentNamespace,
            ImportsUpdated = importsUpdated,
            DryRun = request.DryRun,
            Edited = edited,
            KeptLocal = keptLocal,
            LeftBehind = leftBehind,
            Skipped = graph.Skipped,
        };
    }

    /// <summary>
    /// Take back the files a re-installed item recorded last time but no longer ships (upstream
    /// renamed or split them). Only recorded paths are considered - undo-by-record, never a sweep
    /// of the output directory - and a path another installed item still ships is left alone.
    /// <para>
    /// A file whose content still matches the baseline recorded for it is provably untouched, so
    /// it goes. One that differs is work someone did, and a rename is no reason to throw it away:
    /// it stays, is reported through <paramref name="leftBehind"/>, and only <c>--force</c> takes
    /// it. A file with no baseline (recorded before the ledger existed) counts as unproven and is
    /// treated the same way - the first update after upgrading reports instead of deleting.
    /// </para>
    /// </summary>
    private async Task<List<WrittenFile>> SweepOrphansAsync(
        ResolvedGraph graph,
        Dictionary<string, IReadOnlyList<WrittenFile>> perItem,
        AddRequest request,
        string outputDir,
        List<string> leftBehind,
        AddTransaction? tx,
        CancellationToken ct)
    {
        var results = new List<WrittenFile>();
        var outputRoot = Path.Combine(project.ProjectDir, outputDir);

        // Every path still shipped by SOMETHING installed: this run's items plus the records of
        // items it isn't touching. Two components can share a file - one dropping it doesn't
        // entitle us to delete the other's copy.
        var stillShipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var written in perItem.Values)
            foreach (var file in written)
                stillShipped.Add(file.Path);
        foreach (var (name, installed) in config.Installed)
            if (!perItem.ContainsKey(name))
                foreach (var (path, _) in installed.Files)
                    stillShipped.Add(path);

        foreach (var item in graph.Items)
        {
            if (!config.Installed.TryGetValue(item.QualifiedName, out var record))
                continue;

            foreach (var (path, baseline) in record.Files)
            {
                if (stillShipped.Contains(path))
                    continue;

                // ~/ records resolve against the project root, everything else the output dir.
                var absolute = ComponentWriter.ResolveReported(project.ProjectDir, outputDir, path);
                if (!File.Exists(absolute))
                    continue;

                if (request.DryRun)
                {
                    results.Add(new WrittenFile(path, WriteAction.Planned));
                    continue;
                }

                var local = await ContentHash.OfFileAsync(absolute, ct);
                if (!request.Force && !ContentHash.Matches(baseline, local!))
                {
                    leftBehind.Add(path);
                    continue;
                }

                tx?.SnapshotFile(absolute);
                File.Delete(absolute);
                results.Add(new WrittenFile(path, WriteAction.Deleted));
            }
        }

        return results;
    }

    /// <summary>
    /// Delete files under <paramref name="outputRoot"/> that no resolved item ships and that aren't
    /// CLI-owned (the generated global-usings file), then drop directories left empty. Comparing
    /// against the resolved graph — not <c>blaizio.json</c> — keeps this correct even when the
    /// recorded state and the on-disk copy have drifted apart.
    /// </summary>
    private static List<WrittenFile> Prune(
        IReadOnlyList<RegistryItem> items, BlaizioConfig config, string outputRoot, string pagesDir, bool dryRun,
        Action<string>? beforeDelete = null)
    {
        var results = new List<WrittenFile>();
        var root = Path.GetFullPath(outputRoot);
        if (!Directory.Exists(root))
            return results;

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var expected = new HashSet<string>(comparer) { GlobalUsingsWriter.FileName };
        foreach (var item in items)
            foreach (var file in item.Files)
                // DestinationFor reports POSIX; the sweep compares OS-relative paths. ~/ rooted
                // destinations live outside the output dir, so they can never look like orphans.
                expected.Add(ComponentWriter
                    .DestinationFor(file, ComponentWriter.FolderFor(item.SourceNamespace), pagesDir)
                    .Replace('/', Path.DirectorySeparatorChar));

        // A whole-registry prune covers ONE registry. Installs recorded from other (namespaced)
        // registries are not in this graph - their files are still owned, not orphans.
        foreach (var (key, installed) in config.Installed)
            if (key.StartsWith('@') && !items.Any(i => string.Equals(i.QualifiedName, key, StringComparison.OrdinalIgnoreCase)))
                foreach (var (file, _) in installed.Files)
                    expected.Add(file.Replace('/', Path.DirectorySeparatorChar));

        foreach (var absolute in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, absolute);
            if (expected.Contains(relative))
                continue;
            if (!dryRun)
            {
                beforeDelete?.Invoke(absolute);
                File.Delete(absolute);
            }
            results.Add(new WrittenFile(relative.Replace(Path.DirectorySeparatorChar, '/'), WriteAction.Deleted));
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
