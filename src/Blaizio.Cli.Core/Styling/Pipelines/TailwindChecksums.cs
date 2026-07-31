namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>
/// SHA-256 hashes of every standalone-binary release asset for
/// <see cref="TailwindBinary.DefaultVersion"/>, embedded so the generated MSBuild fetch target can
/// verify its download with <c>VerifyFileHash</c> (MSBuild's <c>DownloadFile</c> cannot checksum on
/// its own, and a build must never execute an unverified executable). The explicit
/// <c>blaizio tailwind fetch</c> path verifies dynamically against the release's published
/// <c>sha256sums.txt</c>; this table is the offline, build-time mirror of that manifest.
/// </summary>
/// <remarks>
/// When bumping <see cref="TailwindBinary.DefaultVersion"/>, regenerate from the release manifest:
/// <c>curl -sL https://github.com/tailwindlabs/tailwindcss/releases/download/{tag}/sha256sums.txt</c>.
/// </remarks>
public static class TailwindChecksums
{
    /// <summary>Asset name to lowercase SHA-256 hex, for <see cref="TailwindBinary.DefaultVersion"/>.</summary>
    public static readonly IReadOnlyDictionary<string, string> Pinned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["tailwindcss-linux-arm64"] = "0409aa4222969f47fa6f4160fe5387e79bf7269e7afe0e8b22f7532c98e1d314",
        ["tailwindcss-linux-arm64-musl"] = "bbb75c8802861546925bba34bb1e53deca6ab9f33ab95a40e0ca7f10380ab744",
        ["tailwindcss-linux-x64"] = "64805b84af4292e043ea6f86d242f191c0ac75359c1a498455dfe6c642afdbab",
        ["tailwindcss-linux-x64-musl"] = "0fb5f1a24dc5237914b9a4375fa0e7520a99da28aa62c36a00c42c34b3df1146",
        ["tailwindcss-macos-arm64"] = "f5984b9c005c3e67841c33906c7a7c92e85e405f61e029e9bb62e880dd662e79",
        ["tailwindcss-macos-x64"] = "76e27326506d10d50e65b751795f0537f9304ecb100abe835ec138c41774f38c",
        ["tailwindcss-windows-x64.exe"] = "6631a41de25a96eb8506f07b07ab56192df117a29849c7d7a995bd343329e900",
    };
}
