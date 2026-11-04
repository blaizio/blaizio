namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Scala (<c>scala</c>; <c>sc</c>, <c>sbt</c>): interpolated strings (<c>s"$name ${expr}"</c>,
    /// <c>f""</c>, <c>raw""</c>), triple-quoted strings across lines, annotations, nested block
    /// comments, and the names after <c>def</c>, <c>class</c>, <c>object</c> and <c>trait</c>.
    /// </summary>
    public static SyntaxLanguage Scala => field ??= new SyntaxGrammar("scala", "sc", "sbt")
        .LineComment("//")
        .BlockComment("/*", "*/", nested: true)
        .String("\"\"\"", escape: '\0', multiline: true)
        .String("\"\"\"", escape: '\0', multiline: true, interpolation: SyntaxInterpolation.DollarBraces | SyntaxInterpolation.DollarName, prefixes: "s f raw")
        .String("\"")
        .String("\"", interpolation: SyntaxInterpolation.DollarBraces | SyntaxInterpolation.DollarName, prefixes: "s f")
        .String("\"", escape: '\0', interpolation: SyntaxInterpolation.DollarBraces | SyntaxInterpolation.DollarName, prefixes: "raw")
        .Char()
        .Keywords("abstract case catch class def do else enum export extends final finally for forSome given if implicit import lazy match new object override package private protected return sealed super then this throw trait try type using val var while with yield end extension derives transparent inline opaque infix open")
        .Constants("true false null None Nil")
        .Declarations("def", SyntaxKind.Function)
        .Declarations("class object trait enum type extends with new", SyntaxKind.Type)
        .Sigil('@', SyntaxKind.Meta, ".")
        .Numbers()
        .Functions()
        .PascalCaseTypes();
}
