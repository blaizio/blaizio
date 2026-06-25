using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;

namespace Blazeo.Docs.Services;

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
}

/// <summary>
/// Builds the API tables shown on the docs pages. Parameter names/types/defaults come from
/// reflection (defaults by instantiating the component and reading the property); descriptions come
/// from <c>Blazeo.Ui.xml</c>, the same <c>///</c> comments the library ships - fetched once and
/// cached, so the tables can never drift from the code.
/// </summary>
internal sealed class ApiDocs(HttpClient http) : IApiDocs
{
    private const string XmlPath = "api/Blazeo.Ui.xml";

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

    private async Task<IReadOnlyDictionary<string, string>> LoadSummariesAsync()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await using var stream = await http.GetStreamAsync(XmlPath);
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            foreach (var member in doc.Root?.Element("members")?.Elements("member") ?? [])
            {
                var name = member.Attribute("name")?.Value;
                var summary = member.Element("summary");
                if (name is not null && summary is not null) map[name] = ToText(summary);
            }
        }
        catch
        {
            // No XML (e.g. a publish that didn't copy it) - tables still render names/types/defaults.
        }

        return map;
    }

    /// <summary>Flattens a doc element to text: resolves cref/langword refs and unwraps inline tags.</summary>
    private static string ToText(XElement element)
    {
        var sb = new StringBuilder();
        Walk(element, sb);
        return string.Join(' ', sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void Walk(XNode node, StringBuilder sb)
    {
        switch (node)
        {
            case XText text:
                sb.Append(text.Value);
                break;
            case XElement el when el.Name.LocalName is "see" or "seealso":
                var cref = el.Attribute("cref")?.Value;
                var langword = el.Attribute("langword")?.Value;
                // No padding: the surrounding text already carries its spaces, and gluing a space
                // before a trailing "." would read "Default ." Whitespace is normalised in ToText.
                sb.Append(langword ?? ShortName(cref) ?? el.Value);
                break;
            case XElement el:
                foreach (var child in el.Nodes()) Walk(child, sb);
                break;
        }
    }

    /// <summary>"F:Blazeo.Ui.ButtonVariant.Default" -&gt; "Default"; "T:Blazeo.Ui.BzButton" -&gt; "Button".</summary>
    private static string? ShortName(string? cref)
    {
        if (string.IsNullOrEmpty(cref)) return null;
        var name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        var paren = name.IndexOf('(');
        if (paren >= 0) name = name[..paren];
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[(dot + 1)..] : name;
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
