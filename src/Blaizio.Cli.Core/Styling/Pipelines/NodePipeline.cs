using System.Text.Json;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core.Projects;

namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>
/// Compiles via the Node Tailwind CLI (<c>@tailwindcss/cli</c>), driven by whichever package manager
/// the project's lockfile indicates. Wires <c>package.json</c> dev-dependencies and <c>css</c> scripts;
/// the actual install is left to the user (printed as a note) so the CLI never shells a package manager.
/// </summary>
public sealed class NodePipeline : ITailwindPipeline
{
    /// <inheritdoc />
    public string Id => "node";

    /// <inheritdoc />
    public string Title => "Node CLI";

    /// <inheritdoc />
    public string Summary => "@tailwindcss/cli via npm/pnpm/yarn/bun (uses your lockfile).";

    /// <inheritdoc />
    public bool CanSetup => true;

    /// <inheritdoc />
    public Detection Detect(ProjectContext project)
    {
        if (FindPackageRoot(project.ProjectDir) is not { } root)
            return Detection.Absent;

        var packageJson = Path.Combine(root, "package.json");
        var evidence = Path.GetRelativePath(project.ProjectDir, packageJson).Replace('\\', '/');
        var pm = PackageManagers.Detect(root);
        var text = SafeRead(packageJson);
        var hasTailwind = text.Contains("tailwindcss", StringComparison.OrdinalIgnoreCase);
        return hasTailwind
            ? Detection.Present($"{evidence} + {pm.ToString().ToLowerInvariant()} (tailwind present)")
            : Detection.Partial($"{evidence} + {pm.ToString().ToLowerInvariant()} (no tailwind yet)");
    }

    /// <inheritdoc />
    public string BuildHint(ProjectContext project, TailwindPaths paths)
        => PackageManagers.RunCommand(
            PackageManagers.Detect(FindPackageRoot(project.ProjectDir) ?? project.ProjectDir), "css:watch");

    /// <inheritdoc />
    public async Task<PipelineSetupResult> SetupAsync(ProjectContext project, TailwindPaths paths, CancellationToken ct = default)
    {
        // Edit the package.json the project already owns (wherever detection found it);
        // only a project with none at all gets a fresh one next to the csproj.
        var packageRoot = FindPackageRoot(project.ProjectDir) ?? project.ProjectDir;
        var pm = PackageManagers.Detect(packageRoot);
        var packageJsonPath = Path.Combine(packageRoot, "package.json");

        var root = File.Exists(packageJsonPath)
            ? JsonNode.Parse(await File.ReadAllTextAsync(packageJsonPath, ct)) as JsonObject ?? new JsonObject()
            : new JsonObject { ["name"] = SafeName(project.AssemblyName), ["private"] = true };

        // tw-animate-css materializes into .blaizio/ from Blaizio.Base, so only the CLI is needed.
        var devDeps = GetOrAdd(root, "devDependencies");
        devDeps["@tailwindcss/cli"] ??= "^4.0.0";

        // Scripts run from the package.json's directory — re-anchor the css paths when that
        // directory isn't the project dir (repo-root or lib/ package.json).
        var input = Rebase(packageRoot, project.ProjectDir, paths.Input);
        var output = Rebase(packageRoot, project.ProjectDir, paths.Output);
        var scripts = GetOrAdd(root, "scripts");
        scripts["css"] = $"tailwindcss -i {input} -o {output}";
        scripts["css:watch"] = $"tailwindcss -i {input} -o {output} --watch";

        await File.WriteAllTextAsync(packageJsonPath, root.ToJsonString(Indented), ct);

        return new PipelineSetupResult
        {
            PipelineId = Id,
            ChangedFiles = [Path.GetRelativePath(project.ProjectDir, packageJsonPath).Replace('\\', '/')],
            BuildHint = PackageManagers.RunCommand(pm, "css:watch"),
            Notes =
            [
                $"Install dependencies: {PackageManagers.AddDevCommand(pm, "@tailwindcss/cli")}",
                $"Then compile: {PackageManagers.RunCommand(pm, "css:watch")}",
            ],
        };
    }

    /// <summary>The nearest directory holding a <c>package.json</c>, per <see cref="PipelineSearch.Roots"/>.</summary>
    private static string? FindPackageRoot(string projectDir)
        => PipelineSearch.Roots(projectDir)
            .FirstOrDefault(root => File.Exists(Path.Combine(root, "package.json")));

    /// <summary>Re-anchor a project-relative css path onto <paramref name="packageRoot"/> (POSIX separators).</summary>
    private static string Rebase(string packageRoot, string projectDir, string projectRelative)
        => Path.GetRelativePath(packageRoot, Path.Combine(projectDir, projectRelative)).Replace('\\', '/');

    private static JsonObject GetOrAdd(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing)
            return existing;
        var created = new JsonObject();
        parent[key] = created;
        return created;
    }

    private static string SafeName(string assemblyName) => assemblyName.ToLowerInvariant().Replace(' ', '-');

    private static string SafeRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch (IOException) { return string.Empty; }
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
}
