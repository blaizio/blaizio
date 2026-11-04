namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// R (<c>r</c>; <c>rscript</c>): dotted names (<c>is.na</c>), strings across lines, the
    /// built-in constants (<c>TRUE</c>, <c>NA</c>, <c>Inf</c>), and function calls.
    /// </summary>
    public static SyntaxLanguage R => field ??= new SyntaxGrammar("r", "rscript")
        .Line("#!", SyntaxKind.Meta, lineStart: true)
        .LineComment("#")
        .String("\"", multiline: true)
        .String("'", multiline: true)
        .IdentifierChars(".")
        .Keywords("if else repeat while function for in next break return switch library require")
        .Constants("TRUE FALSE NULL NA NA_integer_ NA_real_ NA_character_ NaN Inf")
        .Numbers()
        .Functions();
}
