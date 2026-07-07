using System.Runtime.InteropServices;

namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>Progress of a binary download: bytes read so far and total when the server reports it.</summary>
public readonly record struct DownloadProgress(long BytesRead, long? TotalBytes)
{
    /// <summary>Fraction complete in [0,1], or null when the total size is unknown.</summary>
    public double? Fraction => TotalBytes is > 0 ? (double)BytesRead / TotalBytes.Value : null;
}

/// <summary>
/// Resolves and downloads the Tailwind standalone binary for the current OS/architecture into a
/// project's <c>.blaizio/</c> folder, so the <see cref="StandalonePipeline"/> MSBuild target can run
/// it. No Node involved; the binary is a single self-contained executable from Tailwind's releases.
/// </summary>
public static class TailwindBinary
{
    private const string LatestBase = "https://github.com/tailwindlabs/tailwindcss/releases/latest/download";
    private const string TaggedBase = "https://github.com/tailwindlabs/tailwindcss/releases/download";

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
    {
        var asset = AssetName(musl);
        return version.Equals("latest", StringComparison.OrdinalIgnoreCase)
            ? new Uri($"{LatestBase}/{asset}")
            : new Uri($"{TaggedBase}/{NormalizeTag(version)}/{asset}");
    }

    /// <summary>The local path the binary is written to (<c>.blaizio/tailwindcss[.exe]</c>).</summary>
    public static string LocalPath(string projectDir) => StandalonePipeline.BinaryPath(projectDir);

    /// <summary>
    /// Download the standalone binary into <c>.blaizio/</c>. Returns the local path. When the binary
    /// already exists and <paramref name="force"/> is false, nothing is downloaded.
    /// </summary>
    public static async Task<string> FetchAsync(
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
            return target;

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var url = DownloadUrl(version, musl);

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Download failed ({(int)response.StatusCode}) for {url}. Check the version and your network.");

        var total = response.Content.Headers.ContentLength;
        var tmp = target + ".download";

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

            // Atomic-ish swap so a half-written file can't masquerade as the real binary.
            if (File.Exists(target))
                File.Delete(target);
            File.Move(tmp, target);
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

        return target;
    }

    private static string NormalizeTag(string version)
        => version.StartsWith('v') ? version : $"v{version}";
}
