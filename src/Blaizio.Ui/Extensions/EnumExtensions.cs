using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Blaizio.Ui;

/// <summary>
/// Enum helpers for the bz-* style contract: enum members carry a
/// <see cref="DescriptionAttribute"/> with their kebab-case form (e.g. <c>icon-sm</c>), which
/// components render as class suffixes (<c>bz-button-size-icon-sm</c>) and <c>data-*</c> values.
/// </summary>
public static class EnumExtensions
{
    /// <summary>The member's <see cref="DescriptionAttribute"/> value, or its name when unannotated.</summary>
    public static string GetDescription(this Enum value) => s_descriptions.GetOrAdd(value, Describe);

    // Components call this on every render (class suffixes, data-* values). The answer for a member
    // never changes, so the reflection runs once per member instead of once per call.
    private static readonly ConcurrentDictionary<Enum, string> s_descriptions = new();

    // The lookup is by reflection over the value's own enum type. A trimmed app keeps it working:
    // the trimmer never removes the fields of an enum type it keeps, and a member that is there
    // keeps its attributes. Hence the suppression: the analyzer cannot know the type is an enum.
    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "The type is the value's enum type, and the fields of a kept enum are never trimmed.")]
    private static string Describe(Enum value)
    {
        var name = value.ToString();
        var member = value.GetType().GetField(name);
        return member?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? name;
    }
}
