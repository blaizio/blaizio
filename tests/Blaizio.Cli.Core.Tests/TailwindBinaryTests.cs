using System.Net;
using System.Security.Cryptography;
using Blaizio.Cli.Core.Styling.Pipelines;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>Checksum verification for the standalone Tailwind binary download.</summary>
public class TailwindChecksumTests
{
    private static readonly byte[] Payload = "fake tailwind binary"u8.ToArray();

    private static string PayloadHash() => Convert.ToHexStringLower(SHA256.HashData(Payload));

    /// <summary>Serves the binary asset and (optionally) a sha256sums.txt manifest.</summary>
    private sealed class FakeReleaseHandler(string? sumsText) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.AbsoluteUri;

            if (url.EndsWith(TailwindBinary.ChecksumAsset, StringComparison.Ordinal))
                return Task.FromResult(sumsText is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(sumsText) });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Payload),
            });
        }
    }

    private static HttpClient Client(string? sumsText) => new(new FakeReleaseHandler(sumsText));

    // --- ParseSha256Sums ---

    [Fact]
    public void Parses_a_plain_manifest_line()
    {
        var sums = $"{PayloadHash()}  tailwindcss-linux-x64\nother  file\n";
        Assert.Equal(PayloadHash(), TailwindBinary.ParseSha256Sums(sums, "tailwindcss-linux-x64"));
    }

    [Fact]
    public void Parses_a_star_marked_binary_line_case_insensitively()
    {
        var hex = PayloadHash().ToUpperInvariant();
        var sums = $"{hex} *Tailwindcss-Windows-X64.EXE\n";
        Assert.Equal(hex, TailwindBinary.ParseSha256Sums(sums, "tailwindcss-windows-x64.exe"));
    }

    [Fact]
    public void Parses_the_dot_slash_prefix_tailwind_actually_publishes()
    {
        // Real v4.1.11 manifest shape: <hex>  ./tailwindcss-windows-x64.exe
        var sums = $"{PayloadHash()}  ./tailwindcss-windows-x64.exe\n";
        Assert.Equal(PayloadHash(), TailwindBinary.ParseSha256Sums(sums, "tailwindcss-windows-x64.exe"));
    }

    [Fact]
    public void Returns_null_for_a_missing_asset_or_malformed_hex()
    {
        Assert.Null(TailwindBinary.ParseSha256Sums("deadbeef  other-asset\n", "tailwindcss-linux-x64"));
        Assert.Null(TailwindBinary.ParseSha256Sums("tooshort  tailwindcss-linux-x64\n", "tailwindcss-linux-x64"));
        Assert.Null(TailwindBinary.ParseSha256Sums("", "tailwindcss-linux-x64"));
    }

    // --- ComputeSha256Async ---

    [Fact]
    public async Task Computes_the_known_sha256_vector()
    {
        using var dir = new TempDir();
        dir.Write("abc.txt", "abc");
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            await TailwindBinary.ComputeSha256Async(dir.Combine("abc.txt")));
    }

    // --- FetchAsync verification ---

    [Fact]
    public async Task Fetch_verifies_a_matching_checksum()
    {
        using var dir = new TempDir();
        var sums = $"{PayloadHash()}  {TailwindBinary.AssetName()}\n";

        var result = await TailwindBinary.FetchAsync(
            dir.Path, TailwindBinary.DefaultVersion, musl: false, force: false, Client(sums));

        Assert.True(result.Verified);
        Assert.Equal(Payload, await File.ReadAllBytesAsync(result.Path));
    }

    [Fact]
    public async Task Fetch_throws_on_a_checksum_mismatch_and_installs_nothing()
    {
        using var dir = new TempDir();
        var sums = $"{new string('0', 64)}  {TailwindBinary.AssetName()}\n";

        await Assert.ThrowsAsync<InvalidOperationException>(() => TailwindBinary.FetchAsync(
            dir.Path, TailwindBinary.DefaultVersion, musl: false, force: false, Client(sums)));

        Assert.False(File.Exists(TailwindBinary.LocalPath(dir.Path)));
    }

    [Fact]
    public async Task Fetch_without_a_manifest_downgrades_to_unverified()
    {
        using var dir = new TempDir();

        var result = await TailwindBinary.FetchAsync(
            dir.Path, TailwindBinary.DefaultVersion, musl: false, force: false, Client(sumsText: null));

        Assert.False(result.Verified);
        Assert.True(File.Exists(result.Path));
    }

    [Fact]
    public void Default_version_is_a_pinned_tag_not_latest()
    {
        Assert.StartsWith("v", TailwindBinary.DefaultVersion, StringComparison.Ordinal);
        Assert.NotEqual("latest", TailwindBinary.DefaultVersion);
    }
}
