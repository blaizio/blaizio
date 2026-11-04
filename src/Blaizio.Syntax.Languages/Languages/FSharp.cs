namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// F# (<c>fsharp</c>; <c>fs</c>, <c>f#</c>, <c>fsx</c>, <c>fsi</c>): interpolated (<c>$"{x}"</c>),
    /// verbatim and triple-quoted strings, nested <c>(* *)</c> comments (with <c>(*)</c> left an
    /// operator), generic parameters (<c>'T</c>), attributes (<c>[&lt;Literal&gt;]</c>), script
    /// directives (<c>#r</c>), and the names after <c>type</c> and <c>module</c>.
    /// </summary>
    public static SyntaxLanguage FSharp => field ??= new SyntaxGrammar("fsharp", "fs", "f#", "fsx", "fsi", "fsscript")
        .LineComment("//")
        .Token("(*)", SyntaxKind.Text)
        .BlockComment("(*", "*)", nested: true)
        .Line("#", SyntaxKind.Meta, lineStart: true)
        .String("$\"\"\"", "\"\"\"", escape: '\0', multiline: true, interpolation: SyntaxInterpolation.Braces)
        .String("\"\"\"", escape: '\0', multiline: true)
        .String("$\"", "\"", multiline: true, interpolation: SyntaxInterpolation.Braces)
        .String("@\"", "\"", escape: '"', multiline: true)
        .String("\"", multiline: true)
        .String("[<", ">]", escape: '\0', kind: SyntaxKind.Meta)
        .Char()
        .Sigil('\'', SyntaxKind.Type)
        .Keywords("abstract and as assert base begin class default delegate do done downcast downto elif else end exception extern finally fixed for fun function global if in inherit inline interface internal lazy let match member module mutable namespace new not of open or override private public rec return select sig static struct then to try type upcast use val void when while with yield const")
        .Constants("true false null")
        .Types("int int8 int16 int32 int64 uint8 uint16 uint32 uint64 byte sbyte float float32 double decimal bool char string unit obj list seq option array exn bigint nativeint")
        .Words("printfn printf sprintf failwith failwithf raise ignore", SyntaxKind.Function)
        .Declarations("type module", SyntaxKind.Type)
        .Numbers()
        .Functions()
        .PascalCaseTypes();
}
