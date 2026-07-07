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
/// <param name="Path">Local path of the binary.</param>
/// <param name="Verified">True when the download's SHA-256 matched the release's published sums.</param>
public readonly record struct FetchResult(string Path, bool Verified);

/// <summary>
/// Resolves and downloads the Tailwind standalone binary for the current OS/architecture into a
/// project's <c>.blaizio/</c> folder, so the <see cref="StandalonePipeline"/> MSBuild target can run
/// it. No Node involved; the binary is a single self-contained executable from Tailwind's releases.
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

    /// <summary>
    /// Find the SHA-256 hex for <paramref name="assetName"/> in a <c>sha256sums.txt</c> manifest
    /// (<c>&lt;hex&gt;  &lt;name&gt;</c> per line, optional <c>*</c> binary marker). Null when absent.
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

    /// <summary>The local path the binary is written to (<c>.blaizio/tailwindcss[.exe]</c>).</summary>
    public static string LocalPath(string projectDir) => StandalonePipeline.BinaryPath(projectDir);

    /// <summary>
    /// Download the standalone binary into <c>.blaizio/</c> and verify its SHA-256 against the
    /// release's published <c>sha256sums.txt</c>. A checksum mismatch throws and leaves nothing
    /// behind; a missing/unreachable manifest downgrades to unverified (reported in the result).
    /// When the binary already exists and <paramref name="force"/> is false, nothing is downloaded.
    /// </summary>
    public static async Task<FetchResult> FetchAsync(
        string projectDir,
        string version,
        bool musl,
        bool force,
        HttpClient http,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var target = LocalPath(projectDir);
        if (File.Exists(target) && !force)
            return new FetchResult(target, Verified: false);

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var asset = AssetName(musl);
        var url = AssetUrl(version, asset);

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Download failed ({(int)response.StatusCode}) for {url}. Check the version and your network.");

        var total = response.Content.Headers.ContentLength;
        var tmp = target + ".download";
        bool verified;

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

            verified = await VerifyAsync(tmp, version, asset, http, ct);

            // Atomic swap so a half-written file can't masquerade as the real binary and there is
            // no delete-then-move window with no binary at all.
            File.Move(tmp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp))
                File.Delete(tmp);
        }

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(target,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        return new FetchResult(target, verified);
    }

    /// <summary>
    /// Check <paramref name="filePath"/> against the release's checksum manifest. True when the
    /// hash matched; false when the manifest is unavailable or doesn't list the asset. Throws on
    /// an actual mismatch — that's a corrupted or tampered download, never acceptable.
    /// </summary>
    private static async Task<bool> VerifyAsync(
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
                return false;
            sumsText = await sums.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException)
        {
            return false; // manifest unreachable — download stands, but unverified
        }

        var expected = ParseSha256Sums(sumsText, asset);
        if (expected is null)
            return false;

        var actual = await ComputeSha256Async(filePath, ct);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Checksum mismatch for {asset}: expected {expected}, got {actual}. " +
                "The download is corrupted or tampered with — not installing it.");

        return true;
    }

    /// <summary>SHA-256 of a file as lowercase hex.</summary>
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(hash);
    }

    private static string NormalizeTag(string version)
        => version.StartsWith('v') ? version : $"v{version}";
}
