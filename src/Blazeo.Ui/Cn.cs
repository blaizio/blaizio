namespace Blazeo.Ui;

/// <summary>
/// Helper for the cn-* style contract: components render semantic class hooks
/// (e.g. <c>cn-button-variant-outline</c>) plus matching <c>data-variant</c>/<c>data-size</c>
/// attributes, and the active style sheet (Styles/style-*.css) gives them their look.
/// </summary>
internal static class Cn
{
    /// <summary>Kebab-case form of an enum value ("IconSm" → "icon-sm") for class suffixes and data values.</summary>
    public static string Kebab<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) sb.Append('-');
            sb.Append(char.ToLowerInvariant(name[i]));
        }

        return sb.ToString();
    }
}
