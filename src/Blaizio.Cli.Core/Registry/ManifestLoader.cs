using System.Text.Json;

namespace Blaizio.Cli.Core.Registry;

/// <summary>A manifest read from somewhere with its <c>include</c> list folded in.</summary>
/// <param name="Manifest">The flattened manifest: every item, every file path relative to the ROOT manifest.</param>
/// <param name="Problems">Everything wrong with the include graph, ready to print one per line.</param>
/// <param name="Sources">Each item name mapped to the manifest that declared it, for error messages.</param>
public sealed record ManifestLoadResult(
    RegistryIndex Manifest,
    IReadOnlyList<string> Problems,
    IReadOnlyDictionary<string, string> Sources);

/// <summary>
/// Where a manifest and its includes are read from. Paths are relative to the ROOT manifest's
/// folder and always POSIX, so the same folding works over a directory on disk and over a
/// repository served as raw files.
/// </summary>
public interface IManifestReader
{
    /// <summary>The root manifest, relative to its own folder (e.g. <c>registry.json</c>).</summary>
    string RootPath { get; }

    /// <summary>The file's text, or null when there is nothing there.</summary>
    Task<string?> ReadAsync(string relativePath, CancellationToken ct);

    /// <summary>True when the path names a folder rather than a file - a common include slip.</summary>
    bool IsFolder(string relativePath) => false;
}

/// <summary>Reads manifests from a directory on disk, refusing anything outside it.</summary>
public sealed class FileManifestReader(string rootDirectory, string rootFile) : IManifestReader
{
    /// <inheritdoc />
    public string RootPath { get; } = rootFile;

    /// <inheritdoc />
    public async Task<string?> ReadAsync(string relativePath, CancellationToken ct)
    {
        var full = Resolve(relativePath);
        if (full is null || !File.Exists(full))
            return null;
        return await File.ReadAllTextAsync(full, ct);
    }

    /// <inheritdoc />
    public bool IsFolder(string relativePath) => Resolve(relativePath) is { } full && Directory.Exists(full);

    private string? Resolve(string relativePath)
    {
        try
        {
            return SafePath.Resolve(rootDirectory, relativePath);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>
/// Reads a manifest and folds in whatever it lists under <c>include</c>, so a registry can keep
/// one file per folder instead of one enormous list. Paths inside an included manifest are
/// relative to THAT file, and are rewritten to the root manifest's frame of reference on the way
/// in - after which nothing downstream needs to know an include ever existed.
/// </summary>
public static class ManifestLoader
{
    /// <summary>Read a manifest on disk and everything it includes, transitively.</summary>
    public static Task<ManifestLoadResult> LoadAsync(string manifestPath, CancellationToken ct = default)
    {
        var root = Path.GetFullPath(manifestPath);
        return LoadAsync(
            new FileManifestReader(Path.GetDirectoryName(root)!, Path.GetFileName(root)), ct);
    }

    /// <summary>Read a manifest from any source and everything it includes, transitively.</summary>
    public static async Task<ManifestLoadResult> LoadAsync(IManifestReader reader, CancellationToken ct = default)
    {
        var problems = new List<string>();
        var items = new List<RegistryItem>();
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Visited by path: a diamond (two manifests including the same third) folds once, and a
        // cycle would otherwise recurse forever.
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var root = Normalize(reader.RootPath);
        var manifest = await ReadAsync(root, problems, ct);
        if (manifest is null)
            return new ManifestLoadResult(new RegistryIndex(), problems, sources);

        await FoldAsync(manifest, root, ct);

        return new ManifestLoadResult(
            new RegistryIndex
            {
                Schema = manifest.Schema,
                Name = manifest.Name,
                Items = items,
                Styles = manifest.Styles,
            },
            problems,
            sources);

        async Task FoldAsync(RegistryIndex current, string path, CancellationToken token)
        {
            if (!visited.Add(path))
                return;

            var dir = Parent(path);
            foreach (var item in current.Items)
            {
                var name = item.Name;
                if (!string.IsNullOrWhiteSpace(name) && sources.TryGetValue(name, out var first))
                {
                    problems.Add($"duplicate item name '{name}': in {first} and {path}.");
                    continue;
                }

                // Every path is re-expressed against the root manifest, so a file listed as
                // "BzTag.razor" inside Components/registry.json becomes "Components/BzTag.razor".
                var rebased = new List<RegistryFile>(item.Files.Count);
                foreach (var file in item.Files)
                {
                    if (Combine(dir, file.Path) is not { } rebasedPath)
                    {
                        // A path that escapes is reported HERE only when it came from an included
                        // manifest, where nothing downstream could name the file it was written in.
                        // In the root manifest the path is passed through untouched, so the
                        // validate and build checks report it exactly as they always have.
                        if (path == root)
                        {
                            rebased.Add(file);
                            continue;
                        }
                        problems.Add($"'{name}' in {path} has a file path that escapes the registry: {file.Path}");
                        continue;
                    }
                    rebased.Add(new RegistryFile
                    {
                        Path = rebasedPath,
                        Type = file.Type,
                        Content = file.Content,
                        Target = file.Target,
                    });
                }

                if (!string.IsNullOrWhiteSpace(name))
                    sources[name] = path;

                items.Add(new RegistryItem
                {
                    Schema = item.Schema,
                    Name = item.Name,
                    Type = item.Type,
                    Title = item.Title,
                    Version = item.Version,
                    Description = item.Description,
                    Author = item.Author,
                    Categories = item.Categories,
                    Docs = item.Docs,
                    Meta = item.Meta,
                    NugetDependencies = item.NugetDependencies,
                    MinBase = item.MinBase,
                    DevDependencies = item.DevDependencies,
                    RegistryDependencies = item.RegistryDependencies,
                    Files = rebased,
                    CssVars = item.CssVars,
                    Css = item.Css,
                    Font = item.Font,
                });
            }

            foreach (var include in current.Include ?? [])
            {
                if (Combine(dir, include) is not { } included)
                {
                    problems.Add($"{path} includes a path outside the registry: {include}");
                    continue;
                }
                if (reader.IsFolder(included))
                {
                    problems.Add(
                        $"{path} includes a folder: {include}. Include the manifest file itself " +
                        "(e.g. Components/registry.json).");
                    continue;
                }

                var child = await ReadAsync(included, problems, token);
                if (child is not null)
                    await FoldAsync(child, included, token);
            }
        }

        async Task<RegistryIndex?> ReadAsync(string path, List<string> into, CancellationToken token)
        {
            string? text;
            try
            {
                text = await reader.ReadAsync(path, token);
            }
            catch (IOException ex)
            {
                into.Add($"{path} could not be read: {ex.Message}");
                return null;
            }
            catch (HttpRequestException ex)
            {
                into.Add($"{path} could not be fetched: {ex.Message}");
                return null;
            }

            if (text is null)
            {
                into.Add(path == root
                    ? $"{path} does not exist."
                    : $"an include points at a manifest that does not exist: {path}");
                return null;
            }

            try
            {
                var manifest = JsonSerializer.Deserialize(text, CoreJson.Default.RegistryIndex);
                if (manifest is null)
                    into.Add($"{path} is empty.");
                return manifest;
            }
            catch (JsonException ex)
            {
                into.Add($"{path} is not valid JSON: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>A path relative to the root manifest, POSIX and with no <c>.</c>/<c>..</c> left in it.</summary>
    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    /// <summary>The folder part of a root-relative path ("" for a file at the root).</summary>
    private static string Parent(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }

    /// <summary>
    /// <paramref name="relative"/> resolved against <paramref name="dir"/>, both root-relative.
    /// Null when the result would climb out of the registry - a manifest is a self-contained tree,
    /// and a path leaving it has nowhere legitimate to land.
    /// </summary>
    private static string? Combine(string dir, string relative)
    {
        var segments = new List<string>();
        if (dir.Length > 0)
            segments.AddRange(dir.Split('/', StringSplitOptions.RemoveEmptyEntries));

        foreach (var segment in Normalize(relative).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (segment)
            {
                case ".":
                    continue;
                case "..":
                    if (segments.Count == 0)
                        return null;
                    segments.RemoveAt(segments.Count - 1);
                    continue;
                default:
                    segments.Add(segment);
                    continue;
            }
        }

        return segments.Count == 0 ? null : string.Join('/', segments);
    }
}
