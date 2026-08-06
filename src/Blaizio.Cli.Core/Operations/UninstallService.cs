using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Core.Styling.Pipelines;

namespace Blaizio.Cli.Core.Operations;

/// <summary>The outcome of a <c>uninstall</c> run.</summary>
public sealed class UninstallResult
{
    /// <summary>Files and directories deleted (project-relative, POSIX; directories end with <c>/</c>).</summary>
    public required IReadOnlyList<string> Removed { get; init; }

    /// <summary>Files edited in place (csproj import, host page wiring, a user-authored app.css).</summary>
    public required IReadOnlyList<string> Changed { get; init; }

    /// <summary>NuGet package ids uninstalled (the ones the CLI's ledger recorded).</summary>
    public required IReadOnlyList<string> Packages { get; init; }

    /// <summary>True when nothing Blaizio-related was found to remove.</summary>
    public bool NothingFound => Removed.Count == 0 && Changed.Count == 0 && Packages.Count == 0;

    /// <summary>True when this was a preview: nothing was actually touched.</summary>
    public required bool DryRun { get; init; }
}

/// <summary>
/// Undoes what init/add put in — strictly by record, never by pattern: the components and NuGet
/// packages tracked in <c>blaizio.json</c> (<c>installed</c> / <c>packages</c>), their
/// <c>@using</c> registrations, the tokens file when init created it (<c>cssCreated</c>; an
/// adopted file only loses the Blaizio import/source lines), any legacy v1 managed CSS under
/// <c>Styles/blaizio/</c>, the <c>.blaizio/</c> contract dir with its <c>.gitignore</c> entry
/// (plus the standalone Tailwind targets and csproj <c>&lt;Import&gt;</c> when wired), the
/// host-page wiring (boot.js, stylesheet link, stale <c>style-*</c>/<c>preset-*</c> classes) and
/// the config itself. Anything the user authored — or referenced before the CLI ran — stays.
/// </summary>
public sealed class UninstallService
{
    /// <summary>Run the teardown for <paramref name="projectDir"/>.</summary>
    public async Task<UninstallResult> RunAsync(string projectDir, bool dryRun = false, CancellationToken ct = default)
    {
        var removed = new List<string>();
        var changed = new List<string>();
        var packages = new List<string>();

        // Everything tracked lives in the config — read it before it gets deleted below.
        var config = await ConfigStore.LoadAsync(projectDir, ct);

        // Both helpers resolve through SafePath: teardown paths come partly from the persisted
        // config, and a crafted or corrupted blaizio.json must not delete outside the project.
        void RemoveFile(string relative)
        {
            var abs = SafePath.Resolve(projectDir, relative);
            if (!File.Exists(abs))
                return;
            if (!dryRun)
                File.Delete(abs);
            removed.Add(ToPosix(relative));
        }

        void RemoveDir(string relative)
        {
            var abs = SafePath.Resolve(projectDir, relative);
            if (!Directory.Exists(abs))
                return;
            foreach (var file in Directory.EnumerateFiles(abs, "*", SearchOption.AllDirectories))
                removed.Add(ToPosix(Path.GetRelativePath(projectDir, file)));
            if (!dryRun)
                Directory.Delete(abs, recursive: true);
        }

        // The standalone pipeline owned CSS compilation — its generated wwwroot output goes too.
        // Decided before .blaizio/ is deleted.
        var standaloneWired = File.Exists(
            Path.Combine(projectDir, StandalonePipeline.Dir, StandalonePipeline.TargetsFile));

        // 0. The tracked components: exactly the files `add` recorded per item, plus the generated
        //    global-usings file, then the directories the removals left empty. Files the user put
        //    under the output dir are untouched — removal is by record, not by sweep.
        if (config is not null && config.Installed.Count > 0)
        {
            foreach (var item in config.Installed.Values)
                foreach (var (file, _) in item.Files)
                    RemoveFile(file.StartsWith(Writing.ComponentWriter.RootPrefix, StringComparison.Ordinal)
                        ? file[Writing.ComponentWriter.RootPrefix.Length..]
                        : Path.Combine(config.Output, file));
            RemoveFile(Path.Combine(config.Output, GlobalUsingsWriter.FileName));

            var outputAbs = SafePath.ResolveDir(projectDir, config.Output);
            if (!dryRun && Directory.Exists(outputAbs))
            {
                foreach (var dir in Directory.EnumerateDirectories(outputAbs, "*", SearchOption.AllDirectories)
                             .Append(outputAbs)
                             .OrderByDescending(d => d.Length))
                    if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
            }

            // The @usings `add` registered: the component namespace and the Base layer.
            var baseNs = config.Aliases.TryGetValue("base", out var b) && !string.IsNullOrWhiteSpace(b) ? b : "Blaizio";
            var componentRemoved = await ImportsUpdater.RemoveUsingAsync(projectDir, config.Namespace, dryRun, ct);
            var baseRemoved = await ImportsUpdater.RemoveUsingAsync(projectDir, baseNs, dryRun, ct);
            if (componentRemoved || baseRemoved)
                changed.Add("_Imports.razor");
        }

        // 0b. The tracked NuGet packages — only ids the ledger recorded at install time; anything
        //     the project referenced before the CLI ran was never recorded and stays.
        if (config is not null && config.Packages.Count > 0
            && ProjectContext.Discover(projectDir).CsprojPath is not null)
        {
            var dotnet = new DotnetCli(projectDir);
            foreach (var id in config.Packages)
            {
                if (!dryRun)
                    await dotnet.RemovePackageAsync(id, ct);
                packages.Add(id);
            }
        }

        // 1. Legacy v1 managed CSS assets (Styles/blaizio/), when the project still has them.
        RemoveDir(Path.Combine(TailwindSetup.StylesDir, TailwindSetup.LegacyManagedDir));

        // 2. The tokens file: delete only what init created — a v3 file recorded as
        //    cssCreated, or a v1 file carrying the ownership marker. An adopted file is the
        //    user's: only the Blaizio lines inside it are stripped (2b below covers it).
        var inputRel = Path.Combine(TailwindSetup.StylesDir, TailwindSetup.InputName);
        var inputAbs = Path.Combine(projectDir, inputRel);
        if (File.Exists(inputAbs))
        {
            var text = await File.ReadAllTextAsync(inputAbs, ct);
            if (config?.CssCreated == true && config.Css is null
                || text.StartsWith(TailwindSetup.Marker, StringComparison.Ordinal))
            {
                RemoveFile(inputRel);
            }
        }

        // 2b. Project-owned inputs: the recorded bundler input (blaizio.json `css`) plus every
        //     discovered Tailwind input. Strip exactly the Blaizio lines — the contract/animate
        //     imports (.blaizio/), any stale v1 Styles/blaizio imports (even a hand-written
        //     mirror), the marker comments and the component @source globs the CLI injected;
        //     everything user-authored (including the token values) stays.
        var outputGlob = config is null ? null : ToPosix(config.Output);
        var ownInputs = TailwindInputLocator.Discover(projectDir).ToList();
        if (config?.Css is { } customCss && !ownInputs.Contains(ToPosix(customCss), StringComparer.OrdinalIgnoreCase))
            ownInputs.Add(ToPosix(customCss));
        foreach (var inputRelPath in ownInputs)
        {
            var ownAbs = SafePath.Resolve(projectDir, inputRelPath);
            if (!File.Exists(ownAbs)
                || (removed.Contains(ToPosix(inputRel)) && string.Equals(ownAbs, Path.GetFullPath(inputAbs), StringComparison.OrdinalIgnoreCase)))
                continue;

            bool Ours(string line)
            {
                if (line.Contains(TailwindSetup.MarkerPrefix, StringComparison.Ordinal))
                    return true;
                var trimmed = line.TrimStart();
                if ((trimmed.StartsWith("@import", StringComparison.Ordinal) || trimmed.StartsWith("@source", StringComparison.Ordinal))
                    && line.Contains($"{TailwindSetup.LegacyManagedDir}/", StringComparison.Ordinal))
                    return true;
                // The component @source globs the CLI injected (they point at the output dir).
                return outputGlob is not null
                    && trimmed.StartsWith("@source", StringComparison.Ordinal)
                    && line.Contains($"{outputGlob}/**/*", StringComparison.Ordinal);
            }

            var text = await File.ReadAllTextAsync(ownAbs, ct);
            // Items' managed css regions (multi-line, marker-fenced) go first; then the
            // line-shaped Blaizio imports/globs.
            var withoutRegions = Styling.ItemCssRegions.Items(text)
                .Aggregate(text, Styling.ItemCssRegions.Remove);
            var kept = string.Join('\n', withoutRegions.Split('\n').Where(line => !Ours(line)));
            if (!string.Equals(kept, text, StringComparison.Ordinal))
            {
                if (!dryRun)
                    await File.WriteAllTextAsync(ownAbs, kept, ct);
                changed.Add(inputRelPath);
            }
        }

        // 3. The .blaizio/ dir (the materialized contract and, when wired, the standalone
        //    pipeline's targets), the pipeline's compiled output, and the .gitignore entry init
        //    added for the dir.
        if (standaloneWired)
            RemoveFile(Path.Combine("wwwroot", "app.css"));
        RemoveDir(StandalonePipeline.Dir);

        var gitignoreAbs = Path.Combine(projectDir, ".gitignore");
        if (File.Exists(gitignoreAbs))
        {
            var lines = await File.ReadAllLinesAsync(gitignoreAbs, ct);
            var keptLines = lines.Where(l => l.Trim() is not ($"{StandalonePipeline.Dir}/" or StandalonePipeline.Dir)).ToArray();
            if (keptLines.Length != lines.Length)
            {
                if (keptLines.All(string.IsNullOrWhiteSpace))
                {
                    // Nothing but our entry (init created the file): remove it whole.
                    RemoveFile(".gitignore");
                }
                else
                {
                    if (!dryRun)
                        await File.WriteAllLinesAsync(gitignoreAbs, keptLines, ct);
                    changed.Add(".gitignore");
                }
            }
        }

        var project = ProjectContext.Discover(projectDir);
        if (project.CsprojPath is not null)
        {
            if (dryRun)
            {
                if (standaloneWired)
                    changed.Add(ToPosix(Path.GetRelativePath(projectDir, project.CsprojPath)));
            }
            else if (StandalonePipeline.RemoveImport(project.CsprojPath))
            {
                changed.Add(ToPosix(Path.GetRelativePath(projectDir, project.CsprojPath)));
            }
        }

        // 4. Host page wiring.
        var host = await new HostPageSetup().RemoveAsync(projectDir, dryRun: dryRun, ct: ct);
        if (host.HostPath is not null && host.Changes.Count > 0)
            changed.Add(host.HostPath);

        // 5. The config itself, last — everything above still worked if this run is interrupted.
        RemoveFile(BlaizioConfig.FileName);

        // A Styles/ dir left empty by the removals above is ours too.
        var stylesAbs = Path.Combine(projectDir, TailwindSetup.StylesDir);
        if (!dryRun && Directory.Exists(stylesAbs) && !Directory.EnumerateFileSystemEntries(stylesAbs).Any())
            Directory.Delete(stylesAbs);

        return new UninstallResult { Removed = removed, Changed = changed, Packages = packages, DryRun = dryRun };
    }

    private static string ToPosix(string path) => path.Replace('\\', '/');
}
