using Microsoft.AspNetCore.Components;

namespace Blaizio.Docs.Services;

/// <summary>Resolves a rendered part's <c>data-slot</c> value to the component type that owns it.</summary>
public interface ISlotCatalog
{
    /// <summary>The owning component type for a slot, or null when no component maps to it.</summary>
    Type? Resolve(string slot);
}

/// <summary>
/// The Inspect mode's slot map, built once by reflection over both component layers: every
/// <c>Bz*</c> styled component (compiled into this assembly under <c>Blaizio.Ui</c>) and every
/// <c>Base*</c> primitive from Blaizio.Base, keyed by the kebab-case of the name without its
/// prefix - the same convention the components use for their <c>data-slot</c> values
/// (<c>BzColorArea</c> -&gt; <c>color-area</c>, <c>BaseSliderThumb</c> -&gt; <c>slider-thumb</c>).
/// The styled layer wins a collision: it is the API surface a docs reader styles against.
/// </summary>
internal sealed class SlotCatalog : ISlotCatalog
{
    private readonly Dictionary<string, Type> _map = Build();

    public Type? Resolve(string slot) => _map.GetValueOrDefault(slot);

    private static Dictionary<string, Type> Build()
    {
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);

        // Base first, so the Ui pass below overwrites any name both layers claim.
        Add(map, typeof(global::Blaizio.BasePrimitive).Assembly, "Blaizio", "Base");
        Add(map, typeof(global::Blaizio.Ui.BzButton).Assembly, "Blaizio.Ui", "Bz");

        return map;
    }

    private static void Add(Dictionary<string, Type> map, System.Reflection.Assembly assembly, string ns, string prefix)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.Namespace != ns || !type.Name.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            if (!typeof(ComponentBase).IsAssignableFrom(type))
                continue;

            var name = type.Name[prefix.Length..];
            var tick = name.IndexOf('`'); // generic arity suffix (BzTree`1)
            if (tick >= 0) name = name[..tick];
            if (name.Length == 0) continue;

            map[ToKebab(name)] = type;
        }
    }

    private static string ToKebab(string pascal) =>
        string.Concat(pascal.Select((c, i) =>
            char.IsUpper(c) && i > 0 ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
