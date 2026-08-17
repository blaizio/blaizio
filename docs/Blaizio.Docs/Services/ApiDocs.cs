using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Blaizio.Docs.Services;

/// <summary>One row of a component's API table: a parameter's name, type, default, and summary.</summary>
/// <param name="Name">The parameter (property) name.</param>
/// <param name="Type">A friendly C# type name (e.g. <c>RenderFragment?</c>, <c>EventCallback&lt;bool&gt;</c>).</param>
/// <param name="Default">The default value rendered for display (<c>"-"</c> when null/none).</param>
/// <param name="Summary">The parameter's XML-doc summary, as plain text.</param>
public sealed record ApiParam(string Name, string Type, string Default, string Summary);

/// <summary>Reflects a component's <c>[Parameter]</c>s and joins them with the shipped XML-doc summaries.</summary>
public interface IApiDocs
{
    /// <summary>The component's parameters in declaration order. Empty if the type has none.</summary>
    Task<IReadOnlyList<ApiParam>> GetParametersAsync(Type componentType);

    /// <summary>The type's own XML-doc summary as plain text, or empty when unavailable.</summary>
    Task<string> GetTypeSummaryAsync(Type componentType);
}

/// <summary>
/// Builds the API tables shown on the docs pages. Parameter names/types/defaults come from
/// reflection (defaults by instantiating the component and reading the property); descriptions come
/// from <c>Blaizio.Ui.json</c>, which the build boils down from the same <c>///</c> comments the
/// library ships (the <c>BlaizioApiJson</c> task in the csproj) - so the tables can never drift
/// from the code. JSON instead of the raw compiler XML because parsing 600KB of XDocument on the
/// WASM interpreter stalled the first API page for over a second; the trimmed, pre-flattened map
/// deserializes in a fraction of that. Fetched once and cached per session.
/// </summary>
internal sealed class ApiDocs(HttpClient http) : IApiDocs
{
    private const string JsonPath = "api/Blaizio.Ui.json";

    private readonly ConcurrentDictionary<Type, IReadOnlyList<ApiParam>> _cache = new();
    private Task<IReadOnlyDictionary<string, string>>? _summaries;

    public async Task<IReadOnlyList<ApiParam>> GetParametersAsync(Type componentType)
    {
        if (_cache.TryGetValue(componentType, out var cached)) return cached;

        var summaries = await (_summaries ??= LoadSummariesAsync());
        var instance = TryCreate(componentType);

        var rows = componentType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(ParameterAttribute), inherit: true))
            .OrderBy(p => p.MetadataToken)
            .Select(p => new ApiParam(
                p.Name,
                FriendlyType(p.PropertyType),
                FormatDefault(TryGet(p, instance)),
                summaries.GetValueOrDefault($"P:{(p.DeclaringType ?? componentType).FullName}.{p.Name}", string.Empty)))
            .ToList();

        _cache[componentType] = rows;
        return rows;
    }

    public async Task<string> GetTypeSummaryAsync(Type componentType)
    {
        var summaries = await (_summaries ??= LoadSummariesAsync());
        // Generic components document under their CLR name (`BzTree`1`).
        return summaries.GetValueOrDefault($"T:{componentType.FullName}", string.Empty);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadSummariesAsync()
    {
        try
        {
            // Flattening (cref/langword resolution, whitespace) already happened at build time.
            return await http.GetFromJsonAsync<Dictionary<string, string>>(JsonPath)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch
        {
            // No JSON (e.g. a publish that didn't produce it) - tables still render names/types/defaults.
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static object? TryCreate(Type type)
    {
        try { return Activator.CreateInstance(type); }
        catch { return null; }
    }

    private static object? TryGet(PropertyInfo prop, object? instance)
    {
        if (instance is null || !prop.CanRead) return null;
        try { return prop.GetValue(instance); }
        catch { return null; }
    }

    private static string FormatDefault(object? value) => value switch
    {
        null => "-",
        bool b => b ? "true" : "false",
        string s => $"\"{s}\"",
        Enum e => e.ToString(),
        EventCallback => "-",
        _ when value.GetType().IsGenericType
               && value.GetType().GetGenericTypeDefinition() == typeof(EventCallback<>) => "-",
        _ => value.ToString() ?? "-",
    };

    private static readonly Dictionary<Type, string> Keywords = new()
    {
        [typeof(string)] = "string",
        [typeof(bool)] = "bool",
        [typeof(int)] = "int",
        [typeof(double)] = "double",
        [typeof(object)] = "object",
        [typeof(void)] = "void",
    };

    private static string FriendlyType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null) return $"{FriendlyType(underlying)}?";

        if (Keywords.TryGetValue(type, out var keyword)) return keyword;

        if (type.IsGenericType)
        {
            var name = type.Name[..type.Name.IndexOf('`')];
            var args = string.Join(", ", type.GetGenericArguments().Select(FriendlyType));
            return $"{name}<{args}>";
        }

        return type.Name;
    }
}
