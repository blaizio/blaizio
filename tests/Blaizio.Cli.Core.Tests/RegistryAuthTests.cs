using System.Net;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Registry;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class EnvTemplateTests
{
    [Fact]
    public void Expands_a_variable_from_the_environment()
    {
        using var _ = new EnvVar("BLAIZIO_TEST_TOKEN", "s3cret");

        var value = EnvTemplate.Expand("Bearer ${BLAIZIO_TEST_TOKEN}", out var missing);

        Assert.Equal("Bearer s3cret", value);
        Assert.Empty(missing);
    }

    [Fact]
    public void Reports_an_unset_variable_and_leaves_it_written()
    {
        var value = EnvTemplate.Expand("Bearer ${BLAIZIO_TEST_ABSENT}", out var missing);

        // Left verbatim on purpose: a half-filled credential must never reach the wire.
        Assert.Equal("Bearer ${BLAIZIO_TEST_ABSENT}", value);
        Assert.Equal(["BLAIZIO_TEST_ABSENT"], missing);
    }

    [Fact]
    public void Leaves_a_plain_value_alone()
    {
        Assert.Equal("literal", EnvTemplate.Expand("literal", out var missing));
        Assert.Empty(missing);
        Assert.False(EnvTemplate.ReferencesEnv("literal"));
        Assert.True(EnvTemplate.ReferencesEnv("${TOKEN}"));
    }
}

public class RegistrySourceTests
{
    [Fact]
    public void Reads_a_bare_url_and_writes_it_back_as_a_string()
    {
        var config = JsonSerializer.Deserialize(
            """{"namespace":"App","registries":{"@acme":"https://acme.dev/r"}}""",
            CoreJson.Default.BlaizioConfig)!;

        Assert.Equal("https://acme.dev/r", config.Registries["@acme"].Url);
        Assert.True(config.Registries["@acme"].IsPlain);

        // A plain entry must stay a STRING on disk: recording one registry with a token should not
        // rewrite every other line in the file.
        using var written = JsonDocument.Parse(JsonSerializer.Serialize(config, CoreJson.Default.BlaizioConfig));
        var entry = written.RootElement.GetProperty("registries").GetProperty("@acme");
        Assert.Equal(JsonValueKind.String, entry.ValueKind);
        Assert.Equal("https://acme.dev/r", entry.GetString());
    }

    [Fact]
    public void Reads_and_writes_the_object_form()
    {
        var config = JsonSerializer.Deserialize(
            """
            {
              "namespace": "App",
              "registries": {
                "@private": {
                  "url": "https://acme.dev/r",
                  "headers": { "Authorization": "Bearer ${ACME_TOKEN}" },
                  "params": { "channel": "stable" }
                }
              }
            }
            """,
            CoreJson.Default.BlaizioConfig)!;

        var source = config.Registries["@private"];
        Assert.Equal("https://acme.dev/r", source.Url);
        Assert.Equal("Bearer ${ACME_TOKEN}", source.Headers["Authorization"]);
        Assert.Equal("stable", source.Params["channel"]);
        Assert.False(source.IsPlain);

        var json = JsonSerializer.Serialize(config, CoreJson.Default.BlaizioConfig);
        using var written = JsonDocument.Parse(json);
        var entry = written.RootElement.GetProperty("registries").GetProperty("@private");
        Assert.Equal(JsonValueKind.Object, entry.ValueKind);
        // The variable NAME is what is stored - never a resolved token.
        Assert.Equal("Bearer ${ACME_TOKEN}", entry.GetProperty("headers").GetProperty("Authorization").GetString());
        Assert.Equal("stable", entry.GetProperty("params").GetProperty("channel").GetString());
    }

    [Fact]
    public void Resolve_expands_env_references()
    {
        using var _ = new EnvVar("BLAIZIO_TEST_TOKEN", "s3cret");
        var source = new RegistrySource
        {
            Url = "https://acme.dev/r",
            Headers = { ["Authorization"] = "Bearer ${BLAIZIO_TEST_TOKEN}" },
            Params = { ["token"] = "${BLAIZIO_TEST_TOKEN}" },
        };

        var resolved = source.Resolve("@acme");

        Assert.Equal("Bearer s3cret", resolved.Headers["Authorization"]);
        Assert.Equal("s3cret", resolved.Params["token"]);
    }

    [Fact]
    public void Resolve_names_the_missing_variable_and_the_registry()
    {
        var source = new RegistrySource
        {
            Url = "https://acme.dev/r",
            Headers = { ["Authorization"] = "Bearer ${BLAIZIO_TEST_ABSENT}" },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => source.Resolve("@acme"));

        Assert.Contains("@acme", ex.Message);
        Assert.Contains("${BLAIZIO_TEST_ABSENT}", ex.Message);
        Assert.Contains("Authorization", ex.Message);
    }
}

public class RegistryClientAuthTests
{
    private static readonly RegistryIndex Index = new() { Name = "acme", Items = [] };

    [Fact]
    public async Task Sends_the_configured_headers_and_params()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, Index));
        var client = new RegistryClient(
            new HttpClient(handler), "https://acme.dev/r", style: null,
            credentials: () => new ResolvedRegistrySource(
                new Dictionary<string, string> { ["Authorization"] = "Bearer s3cret" },
                new Dictionary<string, string> { ["channel"] = "stable" }));

        await client.GetIndexAsync();

        Assert.Equal("Bearer s3cret", handler.LastRequest!.Headers.GetValues("Authorization").Single());
        Assert.Contains("channel=stable", handler.LastRequest.RequestUri!.Query);
    }

    [Fact]
    public async Task A_401_reads_as_an_authorization_failure_and_carries_the_registry_message()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"message":"Token expired."}"""),
        });
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/r");

        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.GetIndexAsync());

        Assert.Equal(RegistryFailure.Unauthorized, ex.Reason);
        Assert.Contains("Token expired.", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, RegistryFailure.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests, RegistryFailure.RateLimited)]
    [InlineData(HttpStatusCode.NotFound, RegistryFailure.NotFound)]
    public async Task Maps_the_status_to_a_reason(HttpStatusCode status, RegistryFailure expected)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(status));
        var client = new RegistryClient(new HttpClient(handler), "https://acme.dev/r");

        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.GetIndexAsync());

        Assert.Equal(expected, ex.Reason);
    }

    [Fact]
    public async Task A_credential_in_a_query_parameter_stays_out_of_the_error_message()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = new RegistryClient(
            new HttpClient(handler), "https://acme.dev/r", style: null,
            credentials: () => new ResolvedRegistrySource(
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["token"] = "s3cret" }));

        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.GetIndexAsync());

        Assert.DoesNotContain("s3cret", ex.Message);
        Assert.Contains("index.json", ex.Message);
    }

    [Fact]
    public async Task An_unset_variable_fails_before_anything_is_sent()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, Index));
        var client = new RegistryClient(
            new HttpClient(handler), "https://acme.dev/r", style: null,
            credentials: () => new RegistrySource
            {
                Url = "https://acme.dev/r",
                Headers = { ["Authorization"] = "Bearer ${BLAIZIO_TEST_ABSENT}" },
            }.Resolve("@acme"));

        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.GetIndexAsync());

        Assert.Equal(RegistryFailure.Credentials, ex.Reason);
        Assert.Null(handler.LastRequest);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, RegistryIndex index) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(index, CoreJson.Default.RegistryIndex)) };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }
}

/// <summary>Sets an environment variable for the duration of a test, restoring it after.</summary>
public sealed class EnvVar : IDisposable
{
    private readonly string _name;
    private readonly string? _previous;

    public EnvVar(string name, string value)
    {
        _name = name;
        _previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
}
