namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Swift (<c>swift</c>): interpolation (<c>\(expr)</c>), multi-line and raw strings, attributes
    /// (<c>@MainActor</c>), compiler directives (<c>#if</c>, <c>#available</c>), nested block
    /// comments, and the names after <c>func</c>, <c>struct</c>, <c>class</c> and <c>protocol</c>.
    /// </summary>
    public static SyntaxLanguage Swift => field ??= new SyntaxGrammar("swift")
        .LineComment("//")
        .BlockComment("/*", "*/", nested: true)
        .String("\"\"\"", multiline: true, interpolation: SyntaxInterpolation.BackslashParens)
        .String("\"", interpolation: SyntaxInterpolation.BackslashParens)
        .String("#\"", "\"#", escape: '\0')
        .Keywords("associatedtype class deinit enum extension fileprivate func import init inout internal let open operator private precedencegroup protocol public rethrows static struct subscript typealias var break case catch continue default defer do else fallthrough for guard if in repeat return throw switch where while as await is self Self super throws try async actor some any convenience dynamic final indirect lazy mutating nonmutating optional override required unowned weak willSet didSet nonisolated isolated consuming borrowing macro package")
        .Constants("true false nil")
        .Declarations("func", SyntaxKind.Function)
        .Declarations("class struct enum protocol extension actor typealias associatedtype", SyntaxKind.Type)
        .Sigil('#', SyntaxKind.Keyword)
        .Sigil('@', SyntaxKind.Meta)
        .Numbers()
        .Functions()
        .ConstantCase()
        .PascalCaseTypes();
}
