using System.Net;
using Blaizio.Cli.Core.Registry;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class GitHubAddressTests
{
    [Theory]
    [InlineData("acme/toolkit/tag", "acme", "toolkit", "tag", null)]
    [InlineData("acme/toolkit/tag#main", "acme", "toolkit", "tag", "main")]
    [InlineData("acme/toolkit/tag#v1.2.0", "acme", "toolkit", "tag", "v1.2.0")]
    [InlineData("acme/toolkit/tag#feature/thing", "acme", "toolkit", "tag", "feature/thing")]
    public void Parses_an_address(string reference, string owner, string repo, string item, string? gitRef)
    {
        Assert.True(GitHubAddress.TryParse(reference, out var address));
        Assert.Equal((owner, repo, item, gitRef), (address.Owner, address.Repo, address.Item, address.Ref));
    }

    [Theory]
    // Everything that is already something else: a name, a namespace, a URL, a path, a file.
    [InlineData("button")]
    [InlineData("@acme/tag")]
    [InlineData("https://acme.dev/r/tag.json")]
    [InlineData("./local/tag.json")]
    [InlineData("acme/toolkit/tag.json")]
    [InlineData("acme/toolkit")]
    [InlineData("acme/toolkit/nested/tag")]
    [InlineData("acme/toolkit/tag#")]
    public void Leaves_everything_else_alone(string reference)
        => Assert.False(GitHubAddress.TryParse(reference, out _));

    [Fact]
    public void An_unpinned_address_reads_the_default_branch()
    {
        Assert.True(GitHubAddress.TryParse("acme/toolkit/tag", out var address));

        Assert.Equal("HEAD", address.Reference);
        Assert.Equal("https://raw.githubusercontent.com/acme/toolkit/HEAD/", address.RawRoot);
    }

    [Fact]
    public void A_pinned_address_keeps_its_ref_in_the_raw_root()
    {
        Assert.True(GitHubAddress.TryParse("acme/toolkit/tag#v1.0.0", out var address));

        Assert.Equal("https://raw.githubusercontent.com/acme/toolkit/v1.0.0/", address.RawRoot);
        Assert.Equal("acme/toolkit/tag#v1.0.0", address.ToString());
    }
}

public class GitHubRegistryClientTests
{
    private const string Manifest = """
        {
          "name": "toolkit",
          "items": [
            { "name": "tag", "type": "registry:ui",
              "files": [{ "path": "Components/Tag/BzTag.razor", "type": "registry:ui" }] }
          ]
        }
        """;

    [Fact]
    public async Task Reads_the_manifest_and_inlines_the_files_from_the_same_ref()
    {
        var handler = new RepoHandler
        {
            Files =
            {
                ["registry.json"] = Manifest,
                ["Components/Tag/BzTag.razor"] = "@namespace Blaizio.Ui\n<span>tag</span>",
            },
        };
        GitHubAddress.TryParse("acme/toolkit/tag#v1.0.0", out var address);
        var client = new GitHubRegistryClient(new HttpClient(handler), address);

        var item = await client.GetItemAsync("tag");

        Assert.Equal("tag", item.Name);
        Assert.Contains("<span>tag</span>", item.Files.Single().Content);
        // Everything, manifest and sources alike, came from the pinned ref.
        Assert.All(handler.Requested, url => Assert.Contains("/acme/toolkit/v1.0.0/", url));
    }

    [Fact]
    public async Task Folds_includes_the_same_way_a_local_manifest_does()
    {
        var handler = new RepoHandler
        {
            Files =
            {
                ["registry.json"] = """{"name":"toolkit","include":["Components/registry.json"],"items":[]}""",
                ["Components/registry.json"] =
                    """{"items":[{"name":"tag","type":"registry:ui","files":[{"path":"Tag/BzTag.razor"}]}]}""",
                ["Components/Tag/BzTag.razor"] = "<span>tag</span>",
            },
        };
        GitHubAddress.TryParse("acme/toolkit/tag", out var address);
        var client = new GitHubRegistryClient(new HttpClient(handler), address);

        var item = await client.GetItemAsync("tag");

        Assert.Equal("Components/Tag/BzTag.razor", item.Files.Single().Path);
        Assert.Equal("<span>tag</span>", item.Files.Single().Content);
    }

    [Fact]
    public async Task An_unknown_item_lists_what_the_repository_does_have()
    {
        var handler = new RepoHandler { Files = { ["registry.json"] = Manifest } };
        GitHubAddress.TryParse("acme/toolkit/missing", out var address);
        var client = new GitHubRegistryClient(new HttpClient(handler), address);

        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.GetItemAsync("missing"));

        Assert.Equal(RegistryFailure.NotFound, ex.Reason);
        Assert.Contains("tag", ex.Message);
    }

    [Fact]
    public async Task A_missing_or_private_repository_says_so()
    {
        var handler = new RepoHandler();  // every path 404s, as a private repo does
        GitHubAddress.TryParse("acme/secret/tag", out var address);
        var client = new GitHubRegistryClient(new HttpClient(handler), address);

        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.GetItemAsync("tag"));

        Assert.Contains("registry.json", ex.Message);
    }

    [Fact]
    public async Task A_listed_file_that_is_not_there_names_the_file()
    {
        var handler = new RepoHandler { Files = { ["registry.json"] = Manifest } };
        GitHubAddress.TryParse("acme/toolkit/tag", out var address);
        var client = new GitHubRegistryClient(new HttpClient(handler), address);

        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.GetItemAsync("tag"));

        Assert.Contains("Components/Tag/BzTag.razor", ex.Message);
    }

    /// <summary>Serves a fake repository over raw.githubusercontent paths.</summary>
    private sealed class RepoHandler : HttpMessageHandler
    {
        public Dictionary<string, string> Files { get; } = [];
        public List<string> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            Requested.Add(url);
            // Everything after https://raw.githubusercontent.com/{owner}/{repo}/{ref}/
            var path = string.Join('/', request.RequestUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(3));

            return Task.FromResult(Files.TryGetValue(path, out var content)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
