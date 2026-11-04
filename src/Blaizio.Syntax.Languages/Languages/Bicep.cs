namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Bicep (<c>bicep</c>): single-quoted strings with <c>${expr}</c> interpolation, multi-line
    /// strings, decorators (<c>@description</c>), <c>name:</c> properties, the names after
    /// <c>param</c>, <c>var</c>, <c>resource</c> and <c>output</c>, and function calls.
    /// </summary>
    public static SyntaxLanguage Bicep => field ??= new SyntaxGrammar("bicep")
        .LineComment("//")
        .BlockComment("/*", "*/")
        .String("'''", escape: '\0', multiline: true)
        .String("'", interpolation: SyntaxInterpolation.DollarBraces)
        .Sigil('@', SyntaxKind.Meta)
        .Keys(":")
        .Keywords("targetScope metadata param var resource module output existing import using extension type func for in if as with assert provider")
        .Constants("true false null")
        .Types("string int bool object array")
        .Declarations("param var resource module output", SyntaxKind.Variable)
        .Numbers()
        .Functions();
}
