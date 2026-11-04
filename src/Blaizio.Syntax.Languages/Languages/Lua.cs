namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Lua (<c>lua</c>): <c>--[[ ]]</c> block comments, <c>[[ ]]</c> long strings across lines,
    /// <c>self</c>, and calls.
    /// </summary>
    public static SyntaxLanguage Lua => field ??= new SyntaxGrammar("lua")
        .BlockComment("--[[", "]]")
        .LineComment("--")
        .String("[[", "]]", escape: '\0', multiline: true)
        .String("\"")
        .String("'")
        .Keywords("and break do else elseif end for function goto if in local not or repeat return then until while self")
        .Constants("true false nil")
        .Words("require print pairs ipairs", SyntaxKind.Function)
        .Numbers()
        .Functions();
}
