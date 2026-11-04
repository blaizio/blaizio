namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// PHP (<c>php</c>): <c>$variables</c>, double-quoted strings with the variables inside them,
    /// heredocs and nowdocs, attributes (<c>#[Route]</c>), the open and close tags, and the names
    /// after <c>function</c>, <c>class</c> and <c>new</c>.
    /// </summary>
    public static SyntaxLanguage Php => field ??= new SyntaxGrammar("php")
        .Token("<?php", SyntaxKind.Meta)
        .Token("<?=", SyntaxKind.Meta)
        .Token("?>", SyntaxKind.Meta)
        .LineComment("//")
        .Line("#!", SyntaxKind.Meta, lineStart: true)
        .LineComment("#")
        .BlockComment("/*", "*/")
        .String("#[", "]", escape: '\0', kind: SyntaxKind.Meta)
        .String("'")
        .String("\"", interpolation: SyntaxInterpolation.DollarName | SyntaxInterpolation.DollarBraces)
        .Heredoc("<<<")
        .Sigil('$', SyntaxKind.Variable)
        .Keywords("abstract and array as break callable case catch class clone const continue declare default do echo else elseif empty enddeclare endfor endforeach endif endswitch endwhile enum extends final finally fn for foreach function global goto if implements include include_once instanceof insteadof interface isset list match namespace new or print private protected public readonly require require_once return static switch throw trait try unset use var while xor yield from self parent")
        .Constants("true false null TRUE FALSE NULL __CLASS__ __DIR__ __FILE__ __FUNCTION__ __LINE__ __METHOD__ __NAMESPACE__ __TRAIT__")
        .Types("int float bool string object mixed void never iterable")
        .Declarations("function fn", SyntaxKind.Function)
        .Declarations("class interface trait enum extends implements new", SyntaxKind.Type)
        .Numbers()
        .Functions()
        .ConstantCase()
        .PascalCaseTypes();
}
