using System.ComponentModel;
using System.Reflection;

namespace Blazeo.Ui;

/// <summary>
/// Enum helpers for the bz-* style contract: enum members carry a
/// <see cref="DescriptionAttribute"/> with their kebab-case form (e.g. <c>icon-sm</c>), which
/// components render as class suffixes (<c>bz-button-size-icon-sm</c>) and <c>data-*</c> values.
/// </summary>
public static class EnumExtensions
{
    /// <summary>The member's <see cref="DescriptionAttribute"/> value, or its name when unannotated.</summary>
    public static string GetDescription(this Enum value)
    {
        var enumMember = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        var descriptionAttribute = enumMember?.GetCustomAttribute<DescriptionAttribute>();
        return descriptionAttribute is null ? value.ToString() : descriptionAttribute.Description;
    }
}
