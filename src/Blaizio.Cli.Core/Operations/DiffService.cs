using System.Text.Json.Serialization;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Rewriting;
using Blaizio.Cli.Core.Writing;

namespace Blaizio.Cli.Core.Operations;

/// <summary>Per-file outcome of a diff.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DiffStatus>))]
public enum DiffStatus
{
    /// <summary>Local file matches the (namespace-rewritten) upstream content.</summary>
    Unchanged,

    /// <summary>Local file differs from upstream.</summary>
    Changed,

    /// <summary>Upstream ships the file but it does not exist locally.</summary>
    Missing,
}

/// <summary>One file compared against upstream.</summary>
public sealed record DiffFile(string Path, DiffStatus Status);

/// <summary>One item compared against upstream.</summary>
public sealed class DiffItem
{
    /// <summary>Registry item name.</summary>
    public required string Name { get; init; }

    /// <summary>Per-file comparison, paths relative to the output directory (POSIX separators).</summary>
    public required IReadOnlyList<DiffFile> Files { get; init; }

    /// <summary>True when any file changed or is missing.</summary>
    public bool Drifted => Files.Any(f => f.Status is not DiffStatus.Unchanged);
}

/// <summary>The outcome of a <c>diff</c> run.</summary>
public sealed class DiffResult
{
    /// <summary>Every compared item.</summary>
    public required IReadOnlyList<DiffItem> Items { get; init; }

    /// <summary>True when any item drifted from upstream.</summary>
    public bool HasDrift => Items.Any(i => i.Drifted);
}

/// <summary>
/// Compares installed components against their upstream registry versions. Upstream content gets
/// the same namespace rewrite <c>add</c> applies, so an untouched local copy diffs clean.
/// </summary>
public sealed class DiffService(IRegistryClient registry, ProjectContext project, BlaizioConfig config)
{
    /// <summary>
    /// Diff <paramref name="components"/> (or every installed item when empty) against upstream.
    /// </summary>
    public async Task<DiffResult> RunAsync(
        IReadOnlyList<string> components,
        CancellationToken ct = default)
    {
        // A pinned item diffs against its pin, not against whatever is current - drift from the
        // version the user chose is the only drift that means anything for it.
        IReadOnlyList<string> targets = components.Count > 0
            ? components
            : [.. config.Installed
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => kv.Value.Pin is { } pin ? $"{kv.Key}@{pin}" : kv.Key)];

        var componentNamespace = NamespaceResolver.Resolve(null, config, project);
        var rewriter = new NamespaceRewriter(componentNamespace);
        var outputRoot = Path.Combine(project.ProjectDir, config.Output);

        var items = new List<DiffItem>(targets.Count);
        foreach (var name in targets)
        {
            var upstream = await registry.GetItemAsync(name, ct);

            // Namespaced items nest under their registry's subfolder and namespace segment -
            // diff must look at the same paths and expect the same rewrite `add` produced.
            var folder = ComponentWriter.FolderFor(upstream.SourceNamespace);
            var itemRewriter = folder is null
                ? rewriter
                : new NamespaceRewriter($"{componentNamespace}.{folder}");

            var files = new List<DiffFile>(upstream.Files.Count);
            foreach (var file in upstream.Files)
            {
                var posixRelative = ComponentWriter.DestinationFor(
                    file, folder, ComponentWriter.PagesDirFor(project.ProjectDir));
                var local = ComponentWriter.ResolveReported(project.ProjectDir, config.Output, posixRelative);

                if (!File.Exists(local))
                {
                    files.Add(new DiffFile(posixRelative, DiffStatus.Missing));
                    continue;
                }

                var expected = itemRewriter.Rewrite(file.Content
                    ?? throw new InvalidOperationException(
                        $"Item '{upstream.Name}' file '{file.Path}' has no content; the registry item is not resolved."));
                var actual = await File.ReadAllTextAsync(local, ct);

                files.Add(new DiffFile(
                    posixRelative,
                    Normalize(expected) == Normalize(actual) ? DiffStatus.Unchanged : DiffStatus.Changed));
            }

            items.Add(new DiffItem { Name = upstream.QualifiedName, Files = files });
        }

        return new DiffResult { Items = items };
    }

    /// <summary>Line-ending-insensitive comparison — checkout EOL policy is not drift.</summary>
    private static string Normalize(string content) => content.Replace("\r\n", "\n");
}
