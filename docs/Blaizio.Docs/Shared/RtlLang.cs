namespace Blaizio.Docs.Shared;

/// <summary>
/// The language picked in an <c>RtlDemo</c>, handed to the example content as the template
/// context so its copy can switch: <c>@t["حفظ", "שמור"]</c> (Arabic first, Hebrew second).
/// </summary>
public readonly record struct RtlLang(bool IsHebrew)
{
    /// <summary>The copy for the active language - Arabic first, Hebrew second.</summary>
    public string this[string arabic, string hebrew] => IsHebrew ? hebrew : arabic;

    /// <summary>BCP-47 tag of the active language (<c>ar</c> / <c>he</c>).</summary>
    public string Code => IsHebrew ? "he" : "ar";
}
