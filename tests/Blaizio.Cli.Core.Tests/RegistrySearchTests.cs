using System.Net;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Registry;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// Server-side search: the search travels as query parameters on the catalogue request, and a
/// response carrying <c>pagination</c> means the registry filtered. What matters here is where
/// the request went and what stayed out of the cache.
/// </summary>
public class RegistrySearchTests
{
    [Fact]
    public async Task The_search_travels_as_query_parameters()
    {
        var handler = new CapturingHandler();
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/r");

        await client.SearchAsync(new RegistrySearch(
            Query: "button", Types: ["registry:ui", "registry:theme"], Limit: 20, Offset: 40));

        var url = new Uri(handler.Urls.Single());
        Assert.Equal("/r/index.json", url.AbsolutePath);
        Assert.Contains("q=button", url.Query);
        Assert.Contains("type=registry%3Aui%2Cregistry%3Atheme", url.Query);
        Assert.Contains("limit=20", url.Query);
        Assert.Contains("offset=40", url.Query);
    }

    [Fact]
    public async Task An_empty_search_is_the_catalogue_and_shares_its_cache()
    {
        var handler = new CapturingHandler();
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/r");

        await client.SearchAsync(new RegistrySearch());
        await client.GetIndexAsync();

        var url = new Uri(handler.Urls.Single());
        Assert.Equal("", url.Query);
    }

    [Fact]
    public async Task A_filtered_page_never_poisons_the_catalogue_cache()
    {
        var handler = new CapturingHandler();
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/r");

        await client.SearchAsync(new RegistrySearch(Query: "button"));
        await client.GetIndexAsync();

        // Two requests: the filtered page, then the real catalogue.
        Assert.Equal(2, handler.Urls.Count);
        Assert.Equal("", new Uri(handler.Urls[1]).Query);
    }

    [Fact]
    public async Task A_local_registry_answers_with_its_full_catalogue()
    {
        using var dir = new TempDir();
        dir.Write("index.json", """{"name":"local","items":[{"name":"tag","type":"registry:ui"}]}""");
        var client = new RegistryClient(new HttpClient(), dir.Path);

        var index = await client.SearchAsync(new RegistrySearch(Query: "anything"));

        // No server to ask: the caller filters. The missing pagination is the signal.
        Assert.Null(index.Pagination);
        Assert.Single(index.Items);
    }

    [Fact]
    public async Task A_templated_registry_searches_through_its_template()
    {
        var handler = new CapturingHandler();
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/api/{name}");

        await client.SearchAsync(new RegistrySearch(Query: "button"));

        var url = new Uri(handler.Urls.Single());
        Assert.Equal("/api/index", url.AbsolutePath);
        Assert.Contains("q=button", url.Query);
    }

    [Fact]
    public async Task Search_parameters_compose_with_credential_parameters()
    {
        var handler = new CapturingHandler();
        var client = new RegistryClient(
            new HttpClient(handler), "https://acme.dev/r", style: null,
            credentials: () => new ResolvedRegistrySource(
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["token"] = "s3cret" }));

        await client.SearchAsync(new RegistrySearch(Query: "button"));

        var query = new Uri(handler.Urls.Single()).Query;
        Assert.Contains("q=button", query);
        Assert.Contains("token=s3cret", query);
    }

    [Fact]
    public async Task The_pagination_object_survives_the_round_trip()
    {
        var handler = new CapturingHandler
        {
            Body = """
                {
                  "name": "acme",
                  "items": [{ "name": "button", "type": "registry:ui" }],
                  "pagination": { "total": 12, "offset": 0, "limit": 1, "hasMore": true }
                }
                """,
        };
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/r");

        var index = await client.SearchAsync(new RegistrySearch(Query: "button"));

        Assert.NotNull(index.Pagination);
        Assert.Equal((12, 0, 1, true),
            (index.Pagination.Total, index.Pagination.Offset, index.Pagination.Limit, index.Pagination.HasMore));
    }

    /// <summary>Records every URL and answers with a plain, unpaginated catalogue by default.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        public string Body { get; set; } = """{"name":"acme","items":[]}""";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body) });
        }
    }
}
