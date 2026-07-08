using System.Collections.Concurrent;

namespace Blaizio.Docs.Services;

/// <summary>
/// Resolves non-component code snippets (Program.cs registration lines, service interfaces - things
/// that can't be a compilable example component) from files under Snippets/, embedded by the csproj.
/// The file-per-snippet counterpart of <see cref="IExampleSource"/>: pages carry no string literals.
/// </summary>
public interface ISnippetSource
{
    /// <summary>The snippet named <paramref name="name"/> (path under Snippets/ without extension, e.g. <c>"Toast/Setup"</c>).</summary>
    string Get(string name);
}

internal sealed class SnippetSource : ISnippetSource
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public string Get(string name) => _cache.GetOrAdd(name, static n =>
    {
        // Snippets/Toast/Setup.txt embeds as "Blaizio.Docs.Snippets.Toast.Setup.txt".
        var resource = "Blaizio.Docs.Snippets." + n.Replace('/', '.').Replace('\\', '.') + ".txt";
        using var stream = typeof(SnippetSource).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(
                $"No embedded snippet '{n}'. Snippet files live under Snippets/ as .txt so the csproj embeds them.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd('\n');
    });
}
