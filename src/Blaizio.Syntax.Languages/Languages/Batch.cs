namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Windows batch files (<c>batch</c>; <c>bat</c>, <c>cmd</c>), in any case: <c>REM</c> and
    /// <c>::</c> comments, <c>%VAR%</c>, <c>%~dp0</c>, <c>%%i</c> and <c>!VAR!</c> variables,
    /// <c>:labels</c>, and the commands and comparison operators.
    /// </summary>
    public static SyntaxLanguage Batch => field ??= new SyntaxGrammar("batch", "bat", "cmd")
        .IgnoreCase()
        .LineComment("REM")
        .LineComment("::", lineStart: true)
        .String("\"", escape: '\0')
        .Sigil('%', SyntaxKind.Variable, "~*", closed: true)
        .Sigil('!', SyntaxKind.Variable, closed: true)
        .Sigil(':', SyntaxKind.Function, lineStart: true)
        .Keywords("if else for in do goto call exit not exist defined errorlevel setlocal endlocal enabledelayedexpansion enableextensions shift pause cls echo set start title cd chdir pushd popd copy move del erase mkdir md rmdir rd type ren rename choice equ neq lss leq gtr geq off on nul")
        .Numbers();
}
