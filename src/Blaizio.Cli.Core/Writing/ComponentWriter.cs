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
    /// <paramref name="overwrite"/> is set; when <paramref name="dryRun"/> is set nothing is
    /// touched and each file is reported as <see cref="WriteAction.Planned"/>.
    /// </summary>
    public async Task<IReadOnlyList<WrittenFile>> WriteAsync(
        RegistryItem item,
        bool overwrite,
        bool dryRun,
        CancellationToken ct = default)
    {
        var results = new List<WrittenFile>(item.Files.Count);

        foreach (var file in item.Files)
        {
            var relative = DestinationFor(file, subdir);
            // outputDir comes from the user's own config (trusted); the file path comes from the
            // registry (untrusted) and must not escape the output root.
            var absolute = SafePath.Resolve(Path.Combine(projectDir, outputDir), relative);
            var exists = File.Exists(absolute);

            if (dryRun)
            {
                results.Add(new WrittenFile(relative, absolute, WriteAction.Planned));
                continue;
            }

            if (exists && !overwrite)
            {
                results.Add(new WrittenFile(relative, absolute, WriteAction.Skipped));
                continue;
            }

            var contents = rewriter.Rewrite(file.Content
                ?? throw new InvalidOperationException(
                    $"Item '{item.Name}' file '{file.Path}' has no content; the registry item is not resolved."));

            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            await File.WriteAllTextAsync(absolute, contents, ct);

            results.Add(new WrittenFile(
                relative, absolute, exists ? WriteAction.Overwritten : WriteAction.Created));
        }

        return results;
    }

    /// <summary>
    /// Destination path relative to the output directory: an explicit <see cref="RegistryFile.Target"/>,
    /// otherwise the source path with its leading item-type folder (e.g. <c>Ui/</c>) stripped;
    /// either way nested under <paramref name="subdir"/> when one is given. Public so <c>diff</c>
    /// can map upstream files onto the same local paths <c>add</c> writes.
    /// </summary>
    public static string DestinationFor(RegistryFile file, string? subdir = null)
    {
        string relative;
        if (!string.IsNullOrEmpty(file.Target))
        {
            relative = Normalize(file.Target);
        }
        else
        {
            var path = Normalize(file.Path);
            var slash = path.IndexOf(Path.DirectorySeparatorChar);
            relative = slash >= 0 ? path[(slash + 1)..] : path;
        }

        return subdir is null ? relative : Path.Combine(subdir, relative);
    }

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
