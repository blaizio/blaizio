namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// Resolves items straight out of a public repository: the repo's <c>registry.json</c> is the
/// manifest, and the files it names are fetched beside it. Nothing is built and nothing is hosted,
/// so what a consumer gets is the source as committed - no per-skin variants, and no
/// <c>content</c> field to trust: the CLI inlines each file itself, from the same ref.
/// </summary>
/// <remarks>
/// Private repositories are not supported. A repository that needs a credential is a private
/// registry, and those are recorded as a namespace with headers instead.
/// </remarks>
public sealed class GitHubRegistryClient(HttpClient http, GitHubAddress address) : IRegistryClient
{
    private ManifestLoadResult? _manifest;

    /// <inheritdoc />
    public async Task<RegistryIndex> GetIndexAsync(CancellationToken ct = default)
        => (await ManifestAsync(ct)).Manifest;

    /// <inheritdoc />
    public async Task<RegistryItem> GetItemAsync(string nameOrUrlOrPath, CancellationToken ct = default)
    {
        var loaded = await ManifestAsync(ct);
        var wanted = Generation.RegistryGenerator.ToKebab(nameOrUrlOrPath);

        var item =
            loaded.Manifest.Items.FirstOrDefault(i => Same(i.Name, nameOrUrlOrPath))
            ?? loaded.Manifest.Items.FirstOrDefault(i => Same(i.Name, wanted))
            ?? throw new RegistryException(
                $"'{nameOrUrlOrPath}' is not in {address.Repository} at {address.Reference}. " +
                $"Available: {Available(loaded.Manifest)}.", null, RegistryFailure.NotFound);

        // The repository ships sources, not a built registry, so the contents are fetched here -
        // from the same ref the manifest came from, so a pinned address stays pinned all the way
        // down.
        var files = new List<RegistryFile>(item.Files.Count);
        foreach (var file in item.Files)
        {
            files.Add(new RegistryFile
            {
                Path = file.Path,
                Type = file.Type,
                Target = file.Target,
                Content = file.Content ?? await FetchAsync(file.Path, ct),
            });
        }

        return new RegistryItem
        {
            Name = item.Name,
            Type = item.Type,
            Title = item.Title,
            Description = item.Description,
            NugetDependencies = item.NugetDependencies,
            RegistryDependencies = item.RegistryDependencies,
            Files = files,
            CssVars = item.CssVars,
            Font = item.Font,
        };
    }

    private async Task<ManifestLoadResult> ManifestAsync(CancellationToken ct)
    {
        if (_manifest is not null)
            return _manifest;

        var loaded = await ManifestLoader.LoadAsync(new RawReader(http, address), ct);
        if (loaded.Manifest.Items.Count == 0 && loaded.Problems.Count > 0)
        {
            // A private repository and a missing one look identical from here (both 404 on every
            // path), so the message covers the third possibility too: the repo is public and
            // simply is not a registry.
            var missingRoot = loaded.Problems[0].Contains("does not exist", StringComparison.Ordinal);
            var hint = missingRoot
                ? " Check the owner, the repository and the ref, and note that only PUBLIC repositories can be read this way."
                : "";
            throw new RegistryException(
                $"{address.Repository} at {address.Reference} has no usable registry.json: " +
                $"{loaded.Problems[0]}{hint}",
                null, missingRoot ? RegistryFailure.NotFound : RegistryFailure.Malformed);
        }

        return _manifest = loaded;
    }

    private async Task<string> FetchAsync(string path, CancellationToken ct)
        => await new RawReader(http, address).ReadAsync(path, ct)
            ?? throw new RegistryException(
                $"{address.Repository} lists {path} at {address.Reference}, but the file is not there.",
                null, RegistryFailure.NotFound);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Available(RegistryIndex manifest) =>
        manifest.Items.Count == 0 ? "(none)" : string.Join(", ", manifest.Items.Take(10).Select(i => i.Name));

    /// <summary>Reads a repository's files over raw.githubusercontent at one ref.</summary>
    private sealed class RawReader(HttpClient http, GitHubAddress address) : IManifestReader
    {
        public string RootPath => "registry.json";

        public async Task<string?> ReadAsync(string relativePath, CancellationToken ct)
        {
            var url = address.RawRoot + string.Join('/',
                relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

            using var response = await http.GetAsync(url, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                // A private or missing repository answers 404 for every path, so the message says
                // what a consumer can act on rather than guessing which of the two it was.
                throw new RegistryException(
                    $"Could not read {relativePath} from {address.Repository} at {address.Reference} " +
                    $"({(int)response.StatusCode}). Public repositories only.",
                    null, RegistryFailure.Unreachable);
            }

            return await response.Content.ReadAsStringAsync(ct);
        }
    }
}
