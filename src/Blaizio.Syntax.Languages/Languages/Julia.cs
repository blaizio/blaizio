namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Julia (<c>julia</c>; <c>jl</c>): nested <c>#= =#</c> comments, interpolation (<c>$name</c>),
    /// triple-quoted strings, macros (<c>@time</c>), <c>:symbols</c>, <c>!</c> names
    /// (<c>push!</c>), types, and the names after <c>function</c> and <c>struct</c>.
    /// </summary>
    public static SyntaxLanguage Julia => field ??= new SyntaxGrammar("julia", "jl")
        .BlockComment("#=", "=#", nested: true)
        .Line("#!", SyntaxKind.Meta, lineStart: true)
        .LineComment("#")
        .String("\"\"\"", multiline: true, interpolation: SyntaxInterpolation.DollarName)
        .String("\"", interpolation: SyntaxInterpolation.DollarName)
        .Char()
        .Sigil('@', SyntaxKind.Meta, ".")
        .Sigil(':', SyntaxKind.Constant)
        .IdentifierChars("!")
        .Keywords("baremodule begin break catch const continue do else elseif end export finally for function global if import in isa let local macro module quote return struct try using where while abstract mutable primitive type")
        .Constants("true false nothing missing NaN Inf pi im")
        .Types("Int Int8 Int16 Int32 Int64 Int128 UInt UInt8 UInt16 UInt32 UInt64 Float16 Float32 Float64 Bool Char String Symbol Any Nothing Vector Matrix Array Dict Tuple")
        .Declarations("function macro", SyntaxKind.Function)
        .Declarations("struct type", SyntaxKind.Type)
        .Numbers()
        .Functions()
        .PascalCaseTypes();
}
