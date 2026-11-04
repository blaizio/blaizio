namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Kotlin (<c>kotlin</c>; <c>kt</c>, <c>kts</c>): string templates (<c>$name</c>, <c>${expr}</c>),
    /// raw strings across lines, annotations, nested block comments, and the names after
    /// <c>fun</c>, <c>class</c> and <c>object</c>.
    /// </summary>
    public static SyntaxLanguage Kotlin => field ??= new SyntaxGrammar("kotlin", "kt", "kts")
        .LineComment("//")
        .BlockComment("/*", "*/", nested: true)
        .String("\"\"\"", escape: '\0', multiline: true, interpolation: SyntaxInterpolation.DollarBraces | SyntaxInterpolation.DollarName)
        .String("\"", interpolation: SyntaxInterpolation.DollarBraces | SyntaxInterpolation.DollarName)
        .Char()
        .Keywords("as break class continue do else for fun if in interface is object package return super this throw try typealias typeof val var when while by catch constructor finally import init where abstract actual annotation companion const crossinline enum expect external final infix inline inner internal lateinit noinline open operator out override private protected public reified sealed suspend tailrec vararg")
        .Constants("true false null")
        .Declarations("fun", SyntaxKind.Function)
        .Declarations("class interface object typealias", SyntaxKind.Type)
        .Sigil('@', SyntaxKind.Meta, ".")
        .Numbers()
        .Functions()
        .ConstantCase()
        .PascalCaseTypes();
}
