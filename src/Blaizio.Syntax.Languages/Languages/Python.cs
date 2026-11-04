namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Python (<c>python</c>; <c>py</c>, <c>py3</c>, <c>python3</c>, <c>pyi</c>, <c>pyw</c>): f-strings with
    /// the code in their holes, raw and byte strings, triple-quoted strings across lines, decorators,
    /// and the names after <c>def</c> and <c>class</c>.
    /// </summary>
    public static SyntaxLanguage Python => field ??= CreatePython();

    private static SyntaxGrammar CreatePython()
    {
        var python = new SyntaxGrammar("python", "py", "py3", "python3", "pyi", "pyw")
            .Line("#!", SyntaxKind.Meta, lineStart: true)
            .LineComment("#")
            .Keywords("and as assert async await break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield match case self cls")
            .Constants("True False None NotImplemented Ellipsis __name__ __file__ __main__")
            .Types("int float complex str bytes bytearray bool list dict set frozenset tuple object range memoryview")
            .Declarations("def", SyntaxKind.Function)
            .Declarations("class", SyntaxKind.Type)
            .Sigil('@', SyntaxKind.Meta, ".")
            .Numbers()
            .Functions()
            .ConstantCase()
            .PascalCaseTypes();
        foreach (var quote in (string[])["\"\"\"", "'''", "\"", "'"])
        {
            var multiline = quote.Length == 3;
            python
                .String(quote, multiline: multiline)
                .String(quote, multiline: multiline, prefixes: "u b")
                .String(quote, escape: '\0', multiline: multiline, prefixes: "r br rb")
                .String(quote, multiline: multiline, interpolation: SyntaxInterpolation.Braces, prefixes: "f")
                .String(quote, escape: '\0', multiline: multiline, interpolation: SyntaxInterpolation.Braces, prefixes: "rf fr");
        }
        return python;
    }
}
