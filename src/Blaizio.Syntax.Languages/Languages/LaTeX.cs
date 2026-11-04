namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// LaTeX (<c>latex</c>; <c>tex</c>): commands (<c>\section</c>, <c>\begin</c>), <c>%</c> comments,
    /// inline and display math, and escaped characters (<c>\%</c>, <c>\$</c>, <c>\\</c>).
    /// </summary>
    public static SyntaxLanguage LaTeX => field ??= new SyntaxGrammar("latex", "tex")
        .LineComment("%")
        .Token("\\\\", SyntaxKind.Escape)
        .Token("\\%", SyntaxKind.Escape)
        .Token("\\$", SyntaxKind.Escape)
        .Token("\\&", SyntaxKind.Escape)
        .Token("\\#", SyntaxKind.Escape)
        .Token("\\_", SyntaxKind.Escape)
        .Token("\\{", SyntaxKind.Escape)
        .Token("\\}", SyntaxKind.Escape)
        .String("\\[", "\\]", escape: '\0', multiline: true)
        .String("\\(", "\\)", escape: '\0')
        .String("$$", "$$", escape: '\0', multiline: true)
        .String("$", "$", escape: '\0')
        .Sigil('\\', SyntaxKind.Keyword);
}
