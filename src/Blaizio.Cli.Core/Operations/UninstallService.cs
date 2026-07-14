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
/// <c>@using</c> registrations, the managed CSS under <c>Styles/blaizio/</c>, a Blaizio-owned
/// <c>Styles/app.css</c> (a user-authored one only loses the managed <c>./blaizio/</c> imports),
/// the standalone Tailwind targets (<c>.blaizio/</c> + the csproj <c>&lt;Import&gt;</c>), the
/// host-page wiring (boot.js, stylesheet link, <c>style-*</c>/<c>preset-*</c> classes) and the
/// config itself. Anything the user authored — or referenced before the CLI ran — stays.
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

        void RemoveFile(string relative)
        {
            var abs = Path.Combine(projectDir, relative);
            if (!File.Exists(abs))
                return;
            if (!dryRun)
                File.Delete(abs);
            removed.Add(ToPosix(relative));
        }

        void RemoveDir(string relative)
        {
            var abs = Path.Combine(projectDir, relative);
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
                foreach (var file in item.Files)
                    RemoveFile(Path.Combine(config.Output, file));
            RemoveFile(Path.Combine(config.Output, GlobalUsingsWriter.FileName));

            var outputAbs = Path.Combine(projectDir, config.Output);
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

        // 1. Managed CSS assets.
        RemoveDir(Path.Combine(TailwindSetup.StylesDir, TailwindSetup.ManagedDir));

        // 2. The Tailwind input: delete when Blaizio owns it (marker), else strip only the lines
        //    that reference the (now removed) managed assets from the user's file.
        var inputRel = Path.Combine(TailwindSetup.StylesDir, TailwindSetup.InputName);
        var inputAbs = Path.Combine(projectDir, inputRel);
        if (File.Exists(inputAbs))
        {
            var text = await File.ReadAllTextAsync(inputAbs, ct);
            if (text.StartsWith(TailwindSetup.Marker, StringComparison.Ordinal))
            {
                RemoveFile(inputRel);
            }
            else
            {
                var kept = text.Split('\n')
                    .Where(line => !line.Contains($"./{TailwindSetup.ManagedDir}/", StringComparison.Ordinal)
                                   && !line.Contains(TailwindSetup.MarkerPrefix, StringComparison.Ordinal))
                    .ToArray();
                if (kept.Length != text.Split('\n').Length)
                {
                    if (!dryRun)
                        await File.WriteAllTextAsync(inputAbs, string.Join('\n', kept), ct);
                    changed.Add(ToPosix(inputRel));
                }
            }
        }

        // 2b. Project-owned inputs: the recorded bundler input (blaizio.json `css`) plus every
        //     discovered Tailwind input. The managed assets they import are gone after this run,
        //     so any line referencing Styles/blaizio (even a hand-written mirror) is now dead -
        //     strip exactly those lines and the marker comments; everything user-authored stays.
        var ownInputs = TailwindInputLocator.Discover(projectDir).ToList();
        if (config?.Css is { } customCss && !ownInputs.Contains(ToPosix(customCss), StringComparer.OrdinalIgnoreCase))
            ownInputs.Add(ToPosix(customCss));
        foreach (var inputRelPath in ownInputs)
        {
            var ownAbs = Path.GetFullPath(Path.Combine(projectDir, inputRelPath));
            if (!File.Exists(ownAbs) || string.Equals(ownAbs, Path.GetFullPath(inputAbs), StringComparison.OrdinalIgnoreCase))
                continue;

            var text = await File.ReadAllTextAsync(ownAbs, ct);
            var kept = text.Split('\n')
                .Where(line => !line.Contains($"{TailwindSetup.ManagedDir}/", StringComparison.Ordinal)
                               && !line.Contains(TailwindSetup.MarkerPrefix, StringComparison.Ordinal))
                .ToArray();
            if (kept.Length != text.Split('\n').Length)
            {
                if (!dryRun)
                    await File.WriteAllTextAsync(ownAbs, string.Join('\n', kept), ct);
                changed.Add(inputRelPath);
            }
        }

        // 3. The standalone pipeline: compiled output, targets dir, csproj import.
        if (standaloneWired)
            RemoveFile(Path.Combine("wwwroot", "app.css"));
        RemoveDir(StandalonePipeline.Dir);

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
