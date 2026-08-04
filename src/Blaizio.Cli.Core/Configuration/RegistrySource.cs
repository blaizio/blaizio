using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blaizio.Cli.Core.Configuration;

/// <summary>
/// One recorded registry: where it lives, plus whatever a request to it has to carry. A public
/// registry is a bare URL and stays one on disk; adding headers or query parameters turns the
/// entry into an object without changing anything about the ones beside it.
/// </summary>
/// <remarks>
/// Values may reference environment variables as <c>${VAR}</c> and should: a token written
/// literally here is a token committed to the repository. Expansion happens at request time
/// (<see cref="Resolve"/>), never at write time, so <c>blaizio.json</c> only ever holds the
/// variable's NAME.
/// </remarks>
[JsonConverter(typeof(RegistrySourceConverter))]
public sealed class RegistrySource
{
    /// <summary>The registry base URL, or a local directory path.</summary>
    public string Url { get; set; } = "";

    /// <summary>Headers added to every request to this registry (values may be <c>${VAR}</c>).</summary>
    public Dictionary<string, string> Headers
    {
        get => field;
        set => field = value ?? [];
    } = [];

    /// <summary>Query parameters added to every request to this registry (values may be <c>${VAR}</c>).</summary>
    public Dictionary<string, string> Params
    {
        get => field;
        set => field = value ?? [];
    } = [];

    /// <summary>True when the entry carries nothing but a URL, and so round-trips as a plain string.</summary>
    [JsonIgnore]
    public bool IsPlain => Headers.Count == 0 && Params.Count == 0;

    /// <summary>A bare URL, the shape every registry recorded before authentication existed has.</summary>
    public static implicit operator RegistrySource(string url) => new() { Url = url };

    /// <summary>
    /// The headers and parameters to send, with <c>${VAR}</c> replaced from the environment.
    /// Throws when a referenced variable is unset: a request sent without the credential would
    /// come back 401 and blame the registry for a local misconfiguration.
    /// </summary>
    public ResolvedRegistrySource Resolve(string @namespace)
    {
        return new ResolvedRegistrySource(Expand(Headers, "header"), Expand(Params, "query parameter"));

        Dictionary<string, string> Expand(Dictionary<string, string> values, string kind)
        {
            var expanded = new Dictionary<string, string>(values.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in values)
            {
                expanded[key] = EnvTemplate.Expand(value, out var missing);
                if (missing.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Registry '{@namespace}' needs {string.Join(", ", missing.Select(m => $"${{{m}}}"))} " +
                        $"for its '{key}' {kind}, and the environment does not set it.");
                }
            }
            return expanded;
        }
    }
}

/// <summary>A <see cref="RegistrySource"/> with its environment references expanded.</summary>
/// <param name="Headers">Headers to add to each request.</param>
/// <param name="Params">Query parameters to add to each request.</param>
public sealed record ResolvedRegistrySource(
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Params)
{
    /// <summary>Nothing to add - the shape a public registry resolves to.</summary>
    public static readonly ResolvedRegistrySource None =
        new(new Dictionary<string, string>(), new Dictionary<string, string>());

    /// <summary>True when a request needs no decoration at all.</summary>
    public bool IsEmpty => Headers.Count == 0 && Params.Count == 0;
}

/// <summary>
/// Reads a registry entry written either as a bare URL string or as an object, and writes it back
/// in whichever of the two it needs: a plain entry stays a string, so recording one registry with
/// a token does not rewrite every other line in the file.
/// </summary>
public sealed class RegistrySourceConverter : JsonConverter<RegistrySource>
{
    /// <inheritdoc />
    public override RegistrySource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new RegistrySource { Url = reader.GetString() ?? "" };

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A registry entry must be a URL string or an object with a \"url\".");

        var source = new RegistrySource();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return source;
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "url":
                    source.Url = reader.GetString() ?? "";
                    break;
                case "headers":
                    source.Headers = ReadMap(ref reader);
                    break;
                case "params":
                    source.Params = ReadMap(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Unterminated registry entry.");
    }

    private static Dictionary<string, string> ReadMap(ref Utf8JsonReader reader)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return map;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return map;
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var key = reader.GetString()!;
            reader.Read();
            map[key] = reader.TokenType == JsonTokenType.String ? reader.GetString() ?? "" : "";
        }

        throw new JsonException("Unterminated registry headers or params.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, RegistrySource value, JsonSerializerOptions options)
    {
        if (value.IsPlain)
        {
            writer.WriteStringValue(value.Url);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("url", value.Url);
        WriteMap("headers", value.Headers);
        WriteMap("params", value.Params);
        writer.WriteEndObject();

        void WriteMap(string name, Dictionary<string, string> map)
        {
            if (map.Count == 0) return;
            writer.WriteStartObject(name);
            foreach (var (key, entry) in map)
                writer.WriteString(key, entry);
            writer.WriteEndObject();
        }
    }
}
