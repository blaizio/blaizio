using System.Collections.Concurrent;

namespace Blaizio.Docs.Services;

/// <summary>
/// Resolves the exact .razor source of an example component (the files under Examples/, embedded
/// into this assembly by the csproj). One file is both the rendered demo AND the displayed code,
/// so the two can never drift - the compiler guarantees the example builds, and the displayed
/// text is byte-for-byte what compiled.
/// </summary>
public interface IExampleSource
{
    /// <summary>The .razor source of <paramref name="exampleType"/>. Throws when the file was not embedded.</summary>
    string GetSource(Type exampleType);
}

internal sealed class ExampleSource : IExampleSource
{
    private readonly ConcurrentDictionary<Type, string> _cache = new();

    public string GetSource(Type exampleType) => _cache.GetOrAdd(exampleType, static type =>
    {
        // Examples/Tooltip/TooltipDefaultExample.razor embeds (RootNamespace + folder dots) as
        // "Blaizio.Docs.Examples.Tooltip.TooltipDefaultExample.razor" - exactly FullName + ".razor",
        // because the folder path is also what derives the component's namespace.
        var name = type.FullName + ".razor";
        using var stream = type.Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(
                $"No embedded source for example '{type.FullName}'. Example components must live under " +
                "Examples/ so the csproj embeds their .razor file.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd('\n');
    });
}
