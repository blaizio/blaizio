namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Java (<c>java</c>): text blocks, character literals, annotations, primitive types, and the
    /// names after <c>class</c>, <c>extends</c>, <c>new</c> and the like.
    /// </summary>
    public static SyntaxLanguage Java => field ??= new SyntaxGrammar("java")
        .LineComment("//")
        .BlockComment("/*", "*/")
        .String("\"\"\"", multiline: true)
        .String("\"")
        .Char()
        .Keywords("abstract assert break case catch class const continue default do else enum exports extends final finally for goto if implements import instanceof interface module native new open opens package permits private protected provides public record requires return sealed static strictfp super switch synchronized this throw throws to transient transitive try uses var when while with yield")
        .Constants("true false null")
        .Types("boolean byte char short int long float double void")
        .Declarations("class interface enum record extends implements new throws", SyntaxKind.Type)
        .Sigil('@', SyntaxKind.Meta, ".")
        .Numbers()
        .Functions()
        .ConstantCase()
        .PascalCaseTypes();
}
