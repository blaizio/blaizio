using Microsoft.AspNetCore.Components;

namespace Blaizio.Docs.Services;

/// <summary>Resolves a rendered part's <c>data-slot</c> value to the component type that owns it.</summary>
public interface ISlotCatalog
{
    /// <summary>The owning component type for a slot, or null when no component maps to it.</summary>
    Type? Resolve(string slot);
}

/// <summary>
/// The Inspect mode's slot map. The build scans the library sources for every
/// <c>data-slot="..."</c> a component's markup emits and generates <see cref="SlotMap"/> (slot to
/// full type name) - so <c>input</c> resolves to <c>BzInputText</c> and <c>toc-link</c> to
/// <c>BzTableOfContents</c>, which no naming convention could tell. Type names are looked up over
/// both component layers: every <c>Bz*</c> styled component (compiled into this assembly under
/// <c>Blaizio.Ui</c>) and every <c>Base*</c> primitive from Blaizio.Base. A slot the generated map
/// does not know (a third-party part) falls back to the kebab-case-of-the-name convention
/// (<c>color-area</c> to <c>BzColorArea</c>), the styled layer winning a collision.
/// </summary>
internal sealed class SlotCatalog : ISlotCatalog
{
    private readonly Dictionary<string, Type> _map = Build();

    public Type? Resolve(string slot) => _map.GetValueOrDefault(slot);

    private static Dictionary<string, Type> Build()
    {
        var byName = new Dictionary<string, Type>(StringComparer.Ordinal);   // full name, arity stripped
        var byKebab = new Dictionary<string, Type>(StringComparer.Ordinal);

        // Base first, so the Ui pass below overwrites any kebab name both layers claim.
        Add(byName, byKebab, typeof(global::Blaizio.BasePrimitive).Assembly, "Blaizio", "Base");
        Add(byName, byKebab, typeof(global::Blaizio.Ui.BzButton).Assembly, "Blaizio.Ui", "Bz");

        var map = new Dictionary<string, Type>(byKebab, StringComparer.Ordinal);
        foreach (var (slot, owner) in SlotMap.Owners)
        {
            if (byName.TryGetValue(owner, out var type))
                map[slot] = type; // the generated map is exact - it beats the convention
        }
        return map;
    }

    private static void Add(Dictionary<string, Type> byName, Dictionary<string, Type> byKebab,
        System.Reflection.Assembly assembly, string ns, string prefix)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.Namespace != ns || !type.Name.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            if (!typeof(ComponentBase).IsAssignableFrom(type))
                continue;

            var name = type.Name;
            var tick = name.IndexOf('`'); // generic arity suffix (BzTree`1)
            if (tick >= 0) name = name[..tick];
            if (name.Length == prefix.Length) continue;

            byName[$"{ns}.{name}"] = type;
            byKebab[ToKebab(name[prefix.Length..])] = type;
        }
    }

    private static string ToKebab(string pascal) =>
        string.Concat(pascal.Select((c, i) =>
            char.IsUpper(c) && i > 0 ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
