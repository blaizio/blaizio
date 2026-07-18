using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>Progress of a binary download: bytes read so far and total when the server reports it.</summary>
public readonly record struct DownloadProgress(long BytesRead, long? TotalBytes)
{
    /// <summary>Fraction complete in [0,1], or null when the total size is unknown.</summary>
    public double? Fraction => TotalBytes is > 0 ? (double)BytesRead / TotalBytes.Value : null;
}

/// <summary>The outcome of fetching the standalone binary.</summary>
/// <param name="Path">Local path of the binary (in the shared cache).</param>
/// <param name="Verified">True when the binary's SHA-256 matched the release's published sums.</param>
/// <param name="FromCache">True when the binary was already cached — nothing was downloaded.</param>
public readonly record struct FetchResult(string Path, bool Verified, bool FromCache);

/// <summary>
/// Resolves and downloads the Tailwind standalone binary for the current OS/architecture into a
/// per-user shared cache (one download serves every project), so the <see cref="StandalonePipeline"/>
/// MSBuild target can run it. No Node involved; the binary is a single self-contained executable
/// from Tailwind's releases.
/// </summary>
public static class TailwindBinary
{
    private const string LatestBase = "https://github.com/tailwindlabs/tailwindcss/releases/latest/download";
    private const string TaggedBase = "https://github.com/tailwindlabs/tailwindcss/releases/download";

    /// <summary>
    /// The release this tool fetches by default. Pinned (not <c>latest</c>) so builds are
    /// reproducible and the checksum can't race a release being published.
    /// </summary>
    public const string DefaultVersion = "v4.1.11";

    /// <summary>Name of the checksum manifest Tailwind publishes with every release.</summary>
    public const string ChecksumAsset = "sha256sums.txt";

    /// <summary>The release asset name for the current platform (e.g. <c>tailwindcss-windows-x64.exe</c>).</summary>
    /// <param name="musl">Use the musl (Alpine) build on Linux.</param>
    public static string AssetName(bool musl = false)
    {
        var os = OperatingSystem.IsWindows() ? "windows"
            : OperatingSystem.IsMacOS() ? "macos"
            : OperatingSystem.IsLinux() ? "linux"
            : throw new PlatformNotSupportedException("Unsupported OS for the Tailwind standalone binary.");

        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            var other => throw new PlatformNotSupportedException(
                $"Unsupported architecture '{other}' for the Tailwind standalone binary."),
        };

        var name = $"tailwindcss-{os}-{arch}";
        if (os == "linux" && musl)
            name += "-musl";
        if (os == "windows")
            name += ".exe";
        return name;
    }

    /// <summary>True when the host looks like Alpine (musl libc), so the musl build is needed.</summary>
    public static bool IsMusl() => OperatingSystem.IsLinux() && File.Exists("/etc/alpine-release");

    /// <summary>Build the download URL for a version (<c>"latest"</c> or a tag like <c>v4.1.11</c>).</summary>
    public static Uri DownloadUrl(string version, bool musl)
        => AssetUrl(version, AssetName(musl));

    /// <summary>Build the URL of any release asset for a version.</summary>
    public static Uri AssetUrl(string version, string asset)
        => version.Equals("latest", StringComparison.OrdinalIgnoreCase)
            ? new Uri($"{LatestBase}/{asset}")
            : new Uri($"{TaggedBase}/{NormalizeTag(version)}/{asset}");

    // --- shared cache ---

    /// <summary>
    /// The per-user cache root, honoring platform conventions:
    /// <c>BLAIZIO_CACHE_DIR</c> env override, else <c>%LOCALAPPDATA%\blaizio\cache</c> (Windows),
    /// <c>~/Library/Caches/blaizio</c> (macOS), <c>$XDG_CACHE_HOME/blaizio</c> or
    /// <c>~/.cache/blaizio</c> (Linux).
    /// </summary>
    public static string CacheRoot()
    {
        var overridden = Environment.GetEnvironmentVariable("BLAIZIO_CACHE_DIR");
        if (!string.IsNullOrWhiteSpace(overridden))
            return overridden;

        if (OperatingSystem.IsWindows())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "blaizio", "cache");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return Path.Combine(home, "Library", "Caches", "blaizio");

        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        return !string.IsNullOrWhiteSpace(xdg)
            ? Path.Combine(xdg, "blaizio")
            : Path.Combine(home, ".cache", "blaizio");
    }

    /// <summary>Cache path of the binary for a version + platform (asset name keeps OSes apart).</summary>
    public static string CachedBinaryPath(string version, bool musl, string? cacheRoot = null)
        => Path.Combine(cacheRoot ?? CacheRoot(), "tailwind", CacheTag(version), AssetName(musl));

    /// <summary>The project-local override path (<c>.blaizio/tailwindcss[.exe]</c>) — wins when present.</summary>
    public static string LocalPath(string projectDir) => StandalonePipeline.BinaryPath(projectDir);

    /// <summary>
    /// Resolve the binary to run for a project: the project-local override when present, else the
    /// cached binary for <paramref name="version"/> when present, else null (fetch needed).
    /// </summary>
    public static string? Resolve(string projectDir, string version = DefaultVersion, bool? musl = null, string? cacheRoot = null)
    {
        var local = LocalPath(projectDir);
        if (File.Exists(local))
            return local;

        var cached = CachedBinaryPath(version, musl ?? IsMusl(), cacheRoot);
        return File.Exists(cached) ? cached : null;
    }

    // --- checksum helpers ---

    /// <summary>
    /// Find the SHA-256 hex for <paramref name="assetName"/> in a <c>sha256sums.txt</c> manifest
    /// (<c>&lt;hex&gt;  &lt;name&gt;</c> per line, optional <c>*</c> binary marker, names may be
    /// <c>./</c>-prefixed). Null when absent.
    /// </summary>
    public static string? ParseSha256Sums(string sumsText, string assetName)
    {
        foreach (var raw in sumsText.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            var space = line.IndexOf(' ');
            if (space <= 0)
                continue;

            var hex = line[..space].Trim();
            var name = line[space..].Trim().TrimStart('*');
            if (name.StartsWith("./", StringComparison.Ordinal))
                name = name[2..]; // Tailwind's manifest lists assets as ./tailwindcss-...
            if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase) && hex.Length == 64)
                return hex;
        }
        return null;
    }

    /// <summary>SHA-256 of a file as lowercase hex.</summary>
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(hash);
    }

    // --- fetch ---

    /// <summary>The download size in bytes, from a HEAD request. Null when the server doesn't say.</summary>
    public static async Task<long?> GetDownloadSizeAsync(
        string version,
        bool musl,
        HttpClient http,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, AssetUrl(version, AssetName(musl)));
        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? response.Content.Headers.ContentLength : null;
    }

    /// <summary>
    /// Ensure the standalone binary is in the shared cache, downloading it once for all projects.
    /// The download's SHA-256 is verified against the release's published <c>sha256sums.txt</c>:
    /// a mismatch throws and installs nothing; a missing manifest downgrades to unverified. A
    /// <c>.sha256</c> sidecar remembers the verified hash so cache reuse re-verifies for free.
    /// When the binary is already cached and <paramref name="force"/> is false, nothing is downloaded.
    /// </summary>
    public static async Task<FetchResult> FetchAsync(
        string version,
        bool musl,
        bool force,
        HttpClient http,
        IProgress<DownloadProgress>? progress = null,
        string? cacheRoot = null,
        CancellationToken ct = default)
    {
        var asset = AssetName(musl);
        var target = CachedBinaryPath(version, musl, cacheRoot);
        var sidecar = target + ".sha256";

        if (!force && File.Exists(target))
        {
            // Reuse the cached binary; re-verify it against the sidecar so silent corruption of the
            // cache can't propagate into builds. A bad cache entry falls through to a fresh download.
            if (File.Exists(sidecar))
            {
                var expected = (await File.ReadAllTextAsync(sidecar, ct)).Trim();
                var actual = await ComputeSha256Async(target, ct);
                if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    return new FetchResult(target, Verified: true, FromCache: true);
                File.Delete(target);
                File.Delete(sidecar);
            }
            else
            {
                return new FetchResult(target, Verified: false, FromCache: true);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var url = AssetUrl(version, asset);

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Download failed ({(int)response.StatusCode}) for {url}. Check the version and your network.");

        var total = response.Content.Headers.ContentLength;
        var tmp = target + ".download";
        bool verified;
        string? verifiedHash = null;

        try
        {
            await using (var http_stream = await response.Content.ReadAsStreamAsync(ct))
            await using (var file = File.Create(tmp))
            {
                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await http_stream.ReadAsync(buffer, ct)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, n), ct);
                    read += n;
                    progress?.Report(new DownloadProgress(read, total));
                }
            }

            (verified, verifiedHash) = await VerifyAsync(tmp, version, asset, http, ct);

            // Atomic swap so a half-written file can't masquerade as the real binary and there is
            // no delete-then-move window with no binary at all.
            File.Move(tmp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp))
                File.Delete(tmp);
        }

        if (verified && verifiedHash is not null)
            await File.WriteAllTextAsync(sidecar, verifiedHash, ct);
        else if (File.Exists(sidecar))
            File.Delete(sidecar); // stale sidecar from a previous version must not vouch for this file

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(target,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        return new FetchResult(target, verified, FromCache: false);
    }

    /// <summary>
    /// Check <paramref name="filePath"/> against the release's checksum manifest. Returns the
    /// verification state and the matched hash. Throws on an actual mismatch — that's a corrupted
    /// or tampered download, never acceptable.
    /// </summary>
    private static async Task<(bool Verified, string? Hash)> VerifyAsync(
        string filePath,
        string version,
        string asset,
        HttpClient http,
        CancellationToken ct)
    {
        string sumsText;
        try
        {
            using var sums = await http.GetAsync(AssetUrl(version, ChecksumAsset), ct);
            if (!sums.IsSuccessStatusCode)
                return (false, null);
            sumsText = await sums.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException)
        {
            return (false, null); // manifest unreachable — download stands, but unverified
        }

        var expected = ParseSha256Sums(sumsText, asset);
        if (expected is null)
            return (false, null);

        var actual = await ComputeSha256Async(filePath, ct);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Checksum mismatch for {asset}: expected {expected}, got {actual}. " +
                "The download is corrupted or tampered with - not installing it.");

        return (true, actual);
    }

    /// <summary>Cache folder name for a version (tags normalized; <c>latest</c> kept as-is).</summary>
    private static string CacheTag(string version)
        => version.Equals("latest", StringComparison.OrdinalIgnoreCase) ? "latest" : NormalizeTag(version);

    private static string NormalizeTag(string version)
        => version.StartsWith('v') ? version : $"v{version}";
}
