using System.Security.Cryptography;
using System.Text;

namespace Blaizio.Cli.Core.Writing;

/// <summary>
/// Fingerprints of file CONTENT, used as the baseline <c>add</c> records for every file it writes
/// (<c>blaizio.json</c> <c>installed[].hashes</c>). That baseline is what lets a later
/// <c>update</c> tell "the user edited this" apart from "upstream moved on": comparing the local
/// copy against upstream alone can't - both look like a difference.
/// <para>
/// SHA-256, not a CRC: this decides whether someone's edits get destroyed, and a 32-bit
/// error-detection checksum is the wrong tool for that. The files are kilobytes, so the cost is
/// noise. Line endings are normalized first - a CRLF checkout is not an edit.
/// </para>
/// </summary>
public static class ContentHash
{
    /// <summary>Algorithm tag on every recorded hash, so the format can change later without ambiguity.</summary>
    public const string Prefix = "sha256:";

    /// <summary>Hash of <paramref name="content"/>, line-ending normalized. Format: <c>sha256:&lt;hex&gt;</c>.</summary>
    public static string Of(string content) =>
        Prefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(content))));

    /// <summary>Hash of a file's contents, or <see langword="null"/> when it does not exist.</summary>
    public static async Task<string?> OfFileAsync(string path, CancellationToken ct = default) =>
        File.Exists(path) ? Of(await File.ReadAllTextAsync(path, ct)) : null;

    /// <summary>Line-ending-insensitive form - the checkout's EOL policy is not a content change.</summary>
    public static string Normalize(string content) => content.Replace("\r\n", "\n");

    /// <summary>True when a recorded baseline exists and matches <paramref name="hash"/>.</summary>
    public static bool Matches(string? recorded, string hash) =>
        recorded is not null && string.Equals(recorded, hash, StringComparison.OrdinalIgnoreCase);
}
