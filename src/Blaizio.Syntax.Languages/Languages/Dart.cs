namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Dart (<c>dart</c>): string interpolation (<c>$name</c>, <c>${expr}</c>), raw and triple-quoted
    /// strings, annotations (<c>@override</c>), nested block comments, and the names after
    /// <c>class</c>, <c>mixin</c>, <c>extends</c> and <c>with</c>.
    /// </summary>
    public static SyntaxLanguage Dart => field ??= CreateDart();

    private static SyntaxGrammar CreateDart()
    {
        var dart = new SyntaxGrammar("dart")
            .LineComment("//")
            .BlockComment("/*", "*/", nested: true)
            .Keywords("abstract as assert async await base break case catch class const continue covariant default deferred do dynamic else enum export extends extension external factory final finally for get hide if implements import in interface is late library mixin new on operator part required rethrow return sealed set show static super switch sync this throw try typedef var void when while with yield")
            .Constants("true false null")
            .Types("int double num bool void dynamic Never Object String List Map Set Future Stream Iterable Function")
            .Declarations("class mixin enum extension typedef extends implements with", SyntaxKind.Type)
            .Sigil('@', SyntaxKind.Meta)
            .Numbers()
            .Functions()
            .PascalCaseTypes();
        foreach (var quote in (string[])["'''", "\"\"\"", "'", "\""])
        {
            var multiline = quote.Length == 3;
            dart
                .String(quote, multiline: multiline, interpolation: SyntaxInterpolation.DollarBraces | SyntaxInterpolation.DollarName)
                .String(quote, escape: '\0', multiline: multiline, prefixes: "r");
        }
        return dart;
    }
}
