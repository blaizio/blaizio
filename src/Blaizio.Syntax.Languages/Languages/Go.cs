namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Go (<c>go</c>; <c>golang</c>): raw backtick strings across lines, runes, built-in types, and
    /// the names after <c>type</c> and <c>func</c>.
    /// </summary>
    public static SyntaxLanguage Go => field ??= new SyntaxGrammar("go", "golang")
        .LineComment("//")
        .BlockComment("/*", "*/")
        .String("\"")
        .String("`", escape: '\0', multiline: true)
        .Char()
        .Keywords("break case chan const continue default defer else fallthrough for func go goto if import interface map package range return select struct switch type var")
        .Constants("true false nil iota")
        .Types("bool byte complex64 complex128 error float32 float64 int int8 int16 int32 int64 rune string uint uint8 uint16 uint32 uint64 uintptr any comparable")
        .Declarations("type", SyntaxKind.Type)
        .Declarations("func", SyntaxKind.Function)
        .Numbers()
        .Functions()
        .PascalCaseTypes();
}
