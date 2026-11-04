namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Makefile (<c>makefile</c>; <c>make</c>, <c>mk</c>, <c>mak</c>): targets, variable assignments
    /// (<c>=</c>, <c>:=</c>, <c>?=</c>, <c>+=</c>), <c>$(VAR)</c> and automatic variables
    /// (<c>$@</c>, <c>$&lt;</c>), conditionals and special targets (<c>.PHONY</c>), and the command
    /// starting each recipe line.
    /// </summary>
    public static SyntaxLanguage Makefile => field ??= new SyntaxGrammar("makefile", "make", "mk", "mak")
        .LineComment("#")
        .String("$(", ")", escape: '\0', kind: SyntaxKind.Variable)
        .String("${", "}", escape: '\0', kind: SyntaxKind.Variable)
        .Sigil('$', SyntaxKind.Variable, "@<^?*%+")
        .String("\"")
        .String("'")
        .Token(".PHONY", SyntaxKind.Keyword)
        .Token(".DEFAULT_GOAL", SyntaxKind.Keyword)
        .Token(".SUFFIXES", SyntaxKind.Keyword)
        .Token(".ONESHELL", SyntaxKind.Keyword)
        .Token(".SILENT", SyntaxKind.Keyword)
        .Token(".DELETE_ON_ERROR", SyntaxKind.Keyword)
        .Keys(":= ::= ?= += != =", SyntaxKind.Variable, lineStart: true)
        .Keys(":", SyntaxKind.Function, lineStart: true)
        .Keywords("ifeq ifneq ifdef ifndef else endif include sinclude define endef export unexport override private vpath")
        .FirstWord(SyntaxKind.Function);
}
