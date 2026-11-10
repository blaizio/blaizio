using System.Collections.Concurrent;
using System.ComponentModel;
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

    private static string Describe(Enum value)
    {
        var name = value.ToString();
        var member = value.GetType().GetMember(name).FirstOrDefault();
        return member?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? name;
    }
}
