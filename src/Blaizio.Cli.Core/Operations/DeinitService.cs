using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Core.Styling.Pipelines;

namespace Blaizio.Cli.Core.Operations;

/// <summary>The outcome of a <c>deinit</c> run.</summary>
public sealed class DeinitResult
{
    /// <summary>Files and directories deleted (project-relative, POSIX; directories end with <c>/</c>).</summary>
    public required IReadOnlyList<string> Removed { get; init; }

    /// <summary>Files edited in place (csproj import, host page wiring, a user-authored app.css).</summary>
    public required IReadOnlyList<string> Changed { get; init; }

    /// <summary>True when nothing Blaizio-related was found to remove.</summary>
    public bool NothingFound => Removed.Count == 0 && Changed.Count == 0;

    /// <summary>True when this was a preview: nothing was actually touched.</summary>
    public required bool DryRun { get; init; }
}

/// <summary>
/// Removes everything <c>init</c> wired into a project — the configuration, not the product:
/// <c>blaizio.json</c>, the managed CSS under <c>Styles/blaizio/</c>, a Blaizio-owned
/// <c>Styles/app.css</c> (a user-authored one only loses the managed <c>./blaizio/</c> imports),
/// the standalone Tailwind targets (<c>.blaizio/</c> + the csproj <c>&lt;Import&gt;</c>) and the
/// host-page wiring (boot.js, stylesheet link, <c>style-*</c>/<c>preset-*</c> classes).
/// Copied components, their <c>@using</c> registrations and the NuGet packages stay — pages that
/// reference Blaizio components keep compiling.
/// </summary>
public sealed class DeinitService
{
    /// <summary>Run the teardown for <paramref name="projectDir"/>.</summary>
    public async Task<DeinitResult> RunAsync(string projectDir, bool dryRun = false, CancellationToken ct = default)
    {
        var removed = new List<string>();
        var changed = new List<string>();

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
                                   && !line.Contains(TailwindSetup.Marker, StringComparison.Ordinal))
                    .ToArray();
                if (kept.Length != text.Split('\n').Length)
                {
                    if (!dryRun)
                        await File.WriteAllTextAsync(inputAbs, string.Join('\n', kept), ct);
                    changed.Add(ToPosix(inputRel));
                }
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

        return new DeinitResult { Removed = removed, Changed = changed, DryRun = dryRun };
    }

    private static string ToPosix(string path) => path.Replace('\\', '/');
}
