namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// CMake (<c>cmake</c>; <c>cmakelists</c>), in any case: commands, control flow, <c>${VAR}</c>
    /// and <c>$ENV{VAR}</c> references, bracket comments and strings (<c>#[[ ]]</c>, <c>[[ ]]</c>),
    /// and ALL_CAPS arguments (<c>PUBLIC</c>, <c>REQUIRED</c>) as constants.
    /// </summary>
    public static SyntaxLanguage CMake => field ??= new SyntaxGrammar("cmake", "cmakelists")
        .IgnoreCase()
        .BlockComment("#[[", "]]")
        .LineComment("#")
        .String("\"", multiline: true)
        .String("[[", "]]", escape: '\0', multiline: true)
        .String("${", "}", escape: '\0', kind: SyntaxKind.Variable)
        .String("$ENV{", "}", escape: '\0', kind: SyntaxKind.Variable)
        .Keywords("if elseif else endif foreach endforeach while endwhile function endfunction macro endmacro return break continue block endblock")
        .Constants("ON OFF TRUE FALSE YES NO NOTFOUND")
        .Numbers()
        .Functions()
        .ConstantCase();
}
