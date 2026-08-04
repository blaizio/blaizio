using System.Net;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Registry;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// A registry that does not serve items at <c>&lt;base&gt;/&lt;name&gt;.json</c> records a template
/// instead. These cover where each request actually goes.
/// </summary>
public class RegistryTemplateTests
{
    [Fact]
    public async Task An_item_lands_where_the_template_puts_it()
    {
        var handler = new CapturingHandler();
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/api/resources/{name}");

        await client.GetItemAsync("tag");

        // The name replaces the placeholder; nothing appends a .json leaf of its own.
        Assert.Equal("https://acme.dev/api/resources/tag", handler.Urls[^1]);
    }

    [Fact]
    public async Task The_catalogue_is_the_template_at_the_reserved_index_name()
    {
        var handler = new CapturingHandler();
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/r/{name}.json");

        await client.GetIndexAsync();

        Assert.Equal("https://acme.dev/r/index.json", handler.Urls[^1]);
    }

    [Fact]
    public async Task The_style_placeholder_takes_the_recorded_skin()
    {
        var handler = new CapturingHandler();
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/r/{style}/{name}.json", style: "ash");

        await client.GetItemAsync("tag");

        Assert.Equal("https://acme.dev/r/ash/tag.json", handler.Urls[^1]);
    }

    [Fact]
    public async Task A_style_placeholder_without_a_recorded_style_is_a_configuration_error()
    {
        var handler = new CapturingHandler();
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/r/{style}/{name}.json");

        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.GetItemAsync("tag"));

        Assert.Equal(RegistryFailure.Credentials, ex.Reason);
        Assert.Contains("{style}", ex.Message);
        Assert.Empty(handler.Urls);
    }

    [Fact]
    public async Task A_name_cannot_smuggle_path_segments_through_the_template()
    {
        var handler = new CapturingHandler();
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/r/{name}.json");

        await Assert.ThrowsAsync<RegistryException>(() => client.GetItemAsync("../../etc/passwd"));

        // The catalogue read on the way to normalizing the name is fine; the item request that
        // would have escaped the registry never happens.
        Assert.All(handler.Urls, url => Assert.DoesNotContain("passwd", url));
    }

    [Fact]
    public async Task A_plain_base_url_still_uses_the_default_layout()
    {
        var handler = new CapturingHandler();
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/r");

        await client.GetItemAsync("tag");

        Assert.Equal("https://acme.dev/r/tag.json", handler.Urls[^1]);
    }

    /// <summary>Records every URL requested and answers with an empty catalogue/item.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.ToString());
            // Serves as both an index and an item: the client only needs the shape to parse.
            var body = request.RequestUri.ToString().Contains("index")
                ? JsonSerializer.Serialize(new RegistryIndex { Name = "acme", Items = [] }, CoreJson.Default.RegistryIndex)
                : JsonSerializer.Serialize(new RegistryItem { Name = "tag" }, CoreJson.Default.RegistryItem);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
