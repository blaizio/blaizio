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
        var packageJson = Path.Combine(project.ProjectDir, "package.json");
        if (!File.Exists(packageJson))
            return Detection.Absent;

        var pm = PackageManagers.Detect(project.ProjectDir);
        var text = SafeRead(packageJson);
        var hasTailwind = text.Contains("tailwindcss", StringComparison.OrdinalIgnoreCase);
        return hasTailwind
            ? Detection.Present($"package.json + {pm.ToString().ToLowerInvariant()} (tailwind present)")
            : Detection.Partial($"package.json + {pm.ToString().ToLowerInvariant()} (no tailwind yet)");
    }

    /// <inheritdoc />
    public string BuildHint(ProjectContext project, TailwindPaths paths)
        => PackageManagers.RunCommand(PackageManagers.Detect(project.ProjectDir), "css:watch");

    /// <inheritdoc />
    public async Task<PipelineSetupResult> SetupAsync(ProjectContext project, TailwindPaths paths, CancellationToken ct = default)
    {
        var pm = PackageManagers.Detect(project.ProjectDir);
        var packageJsonPath = Path.Combine(project.ProjectDir, "package.json");

        var root = File.Exists(packageJsonPath)
            ? JsonNode.Parse(await File.ReadAllTextAsync(packageJsonPath, ct)) as JsonObject ?? new JsonObject()
            : new JsonObject { ["name"] = SafeName(project.AssemblyName), ["private"] = true };

        // tw-animate-css is vendored into Styles/blaizio/ by TailwindSetup, so only the CLI is needed.
        var devDeps = GetOrAdd(root, "devDependencies");
        devDeps["@tailwindcss/cli"] ??= "^4.0.0";

        var scripts = GetOrAdd(root, "scripts");
        scripts["css"] = $"tailwindcss -i {paths.Input} -o {paths.Output}";
        scripts["css:watch"] = $"tailwindcss -i {paths.Input} -o {paths.Output} --watch";

        await File.WriteAllTextAsync(packageJsonPath, root.ToJsonString(Indented), ct);

        return new PipelineSetupResult
        {
            PipelineId = Id,
            ChangedFiles = ["package.json"],
            BuildHint = PackageManagers.RunCommand(pm, "css:watch"),
            Notes =
            [
                $"Install dependencies: {PackageManagers.AddDevCommand(pm, "@tailwindcss/cli")}",
                $"Then compile: {PackageManagers.RunCommand(pm, "css:watch")}",
            ],
        };
    }

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
