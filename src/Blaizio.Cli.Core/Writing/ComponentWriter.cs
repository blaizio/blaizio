using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Rewriting;

namespace Blaizio.Cli.Core.Writing;

/// <summary>
/// Writes a resolved item's files into the consumer project under the configured output directory,
/// applying the <see cref="NamespaceRewriter"/> to each file's contents on the way in. A
/// <paramref name="subdir"/> (the folder a namespaced registry's items nest under, e.g.
/// <c>Acme</c> for <c>@acme</c>) prefixes every destination.
/// </summary>
public sealed class ComponentWriter(
    string projectDir, string outputDir, NamespaceRewriter rewriter, string? subdir = null)
{
    /// <summary>
    /// Write every file of <paramref name="item"/>. Existing files are only replaced when
    /// <paramref name="overwrite"/> is set and the path is not listed in
    /// <paramref name="keepLocal"/> (the user's edits they chose to keep); when
    /// <paramref name="dryRun"/> is set nothing is touched and each file is reported as
    /// <see cref="WriteAction.Planned"/>. A file whose content already matches upstream is left
    /// alone as <see cref="WriteAction.Unchanged"/> - rewriting it would only churn its timestamp
    /// and trigger a rebuild. <paramref name="beforeWrite"/> (when given) receives each
    /// destination's absolute path before it is touched, so the caller can snapshot it for
    /// rollback. Each file lands via a temp-file-and-move, so a crash mid-write never leaves a
    /// half-written destination.
    /// </summary>
    public async Task<IReadOnlyList<WrittenFile>> WriteAsync(
        RegistryItem item,
        bool overwrite,
        bool dryRun,
        Action<string>? beforeWrite = null,
        IReadOnlySet<string>? keepLocal = null,
        CancellationToken ct = default)
    {
        var results = new List<WrittenFile>(item.Files.Count);

        foreach (var file in item.Files)
        {
            var reported = DestinationFor(file, subdir, PagesDirFor(projectDir));
            // outputDir comes from the user's own config (trusted); the file path/target comes
            // from the registry (untrusted) and must not escape its root - the output folder for
            // component files, the project itself for ~/ rooted ones.
            var absolute = ResolveReported(projectDir, outputDir, reported);
            var exists = File.Exists(absolute);

            if (dryRun)
            {
                results.Add(new WrittenFile(reported, WriteAction.Planned));
                continue;
            }

            var allowed = overwrite && keepLocal?.Contains(reported) != true;
            if (exists && !allowed)
            {
                results.Add(new WrittenFile(reported, WriteAction.Skipped));
                continue;
            }

            var contents = rewriter.Rewrite(file.Content
                ?? throw new InvalidOperationException(
                    $"Item '{item.Name}' file '{file.Path}' has no content; the registry item is not resolved."));
            var hash = ContentHash.Of(contents);

            if (exists && ContentHash.Matches(await ContentHash.OfFileAsync(absolute, ct), hash))
            {
                results.Add(new WrittenFile(reported, WriteAction.Unchanged) { Hash = hash });
                continue;
            }

            beforeWrite?.Invoke(absolute);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            var tmp = absolute + ".blaizio-tmp";
            try
            {
                await File.WriteAllTextAsync(tmp, contents, ct);
                File.Move(tmp, absolute, overwrite: true);
            }
            finally
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }

            results.Add(new WrittenFile(
                reported, exists ? WriteAction.Overwritten : WriteAction.Created) { Hash = hash });
        }

        return results;
    }

    /// <summary>
    /// Where one of <paramref name="item"/>'s files lands and what it would contain - the same
    /// mapping and namespace rewrite <see cref="WriteAsync"/> applies, without touching anything.
    /// Used by the local-edit scan to compare upstream against what is on disk.
    /// </summary>
    public (string Reported, string Absolute, string Content) Plan(RegistryItem item, RegistryFile file)
    {
        var reported = DestinationFor(file, subdir, PagesDirFor(projectDir));
        return (
            reported,
            ResolveReported(projectDir, outputDir, reported),
            rewriter.Rewrite(file.Content
                ?? throw new InvalidOperationException(
                    $"Item '{item.Name}' file '{file.Path}' has no content; the registry item is not resolved.")));
    }

    /// <summary>Marks a reported/recorded path as project-root-relative instead of output-relative.</summary>
    public const string RootPrefix = "~/";

    /// <summary>
    /// Destination for a file, POSIX separators, in the same shape it is reported and recorded:
    /// output-relative for component files (an explicit <see cref="RegistryFile.Target"/>,
    /// otherwise the source path minus its leading item-type folder, nested under
    /// <paramref name="subdir"/> when given), or <c>~/</c>-prefixed project-root-relative for a
    /// <see cref="FileType.File"/>/<see cref="FileType.Page"/> destination. A rooted destination
    /// never nests under the namespace subdir - it is already absolute within the project.
    /// Public so <c>diff</c> maps upstream files onto the same local paths <c>add</c> writes.
    /// </summary>
    public static string DestinationFor(RegistryFile file, string? subdir = null, string pagesDir = "Pages")
    {
        var target = file.Target;
        if (file.Type is FileType.File or FileType.Page)
        {
            // A page defaults into the project's pages folder; a loose file has no default - its
            // target IS its meaning (validated before anything mutates).
            target ??= file.Type == FileType.Page
                ? $"{RootPrefix}{pagesDir}/{ToPosix(Normalize(file.Path)).Split('/')[^1]}"
                : throw new InvalidOperationException(
                    $"File '{file.Path}' is registry:file but has no target. A loose file must say where it lands (e.g. \"~/wwwroot/robots.txt\").");
            if (!target.StartsWith(RootPrefix, StringComparison.Ordinal))
                target = RootPrefix + target.TrimStart('/');
            return RootPrefix + ToPosix(Normalize(target[RootPrefix.Length..]));
        }

        string relative;
        if (!string.IsNullOrEmpty(target))
        {
            relative = Normalize(target);
        }
        else
        {
            var path = Normalize(file.Path);
            var slash = path.IndexOf(Path.DirectorySeparatorChar);
            relative = slash >= 0 ? path[(slash + 1)..] : path;
        }

        return ToPosix(subdir is null ? relative : Path.Combine(subdir, relative));
    }

    /// <summary>
    /// The absolute path for a reported/recorded destination: <c>~/</c> rooted ones resolve under
    /// the project, everything else under the output folder - both contained (a crafted path
    /// cannot escape).
    /// </summary>
    public static string ResolveReported(string projectDir, string outputDir, string reported) =>
        reported.StartsWith(RootPrefix, StringComparison.Ordinal)
            ? SafePath.Resolve(projectDir, Normalize(reported[RootPrefix.Length..]))
            : SafePath.Resolve(Path.Combine(projectDir, outputDir), Normalize(reported));

    /// <summary>The project's routable-pages folder: <c>Components/Pages</c> when the project has
    /// one (Blazor Web App layout), else <c>Pages</c> (standalone WASM / Server).</summary>
    public static string PagesDirFor(string projectDir) =>
        Directory.Exists(Path.Combine(projectDir, "Components", "Pages")) ? "Components/Pages" : "Pages";

    private static string ToPosix(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>
    /// The output subfolder (and namespace segment) for a registry namespace: <c>@acme</c>
    /// becomes <c>Acme</c>, <c>@acme-ui</c> becomes <c>AcmeUi</c>. Null for the default registry -
    /// only namespaced installs nest.
    /// </summary>
    public static string? FolderFor(string? sourceNamespace)
    {
        if (string.IsNullOrEmpty(sourceNamespace))
            return null;

        var parts = sourceNamespace.TrimStart('@')
            .Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        var folder = string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        return folder.Length == 0 ? null : folder;
    }

    private static string Normalize(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
}
