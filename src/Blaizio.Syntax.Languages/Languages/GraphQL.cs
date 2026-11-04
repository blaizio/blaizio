namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// GraphQL (<c>graphql</c>; <c>gql</c>): block strings across lines, <c>$variables</c>,
    /// directives (<c>@include</c>), <c>field:</c> keys and arguments, the built-in scalars, and the
    /// names operations, fragments and types declare.
    /// </summary>
    public static SyntaxLanguage GraphQL => field ??= new SyntaxGrammar("graphql", "gql")
        .LineComment("#")
        .String("\"\"\"", multiline: true)
        .String("\"")
        .Sigil('$', SyntaxKind.Variable)
        .Sigil('@', SyntaxKind.Meta)
        .Keys(":")
        .Keywords("query mutation subscription fragment on type interface union enum scalar input extend schema directive implements repeatable")
        .Constants("true false null")
        .Types("Int Float String Boolean ID")
        .Declarations("query mutation subscription", SyntaxKind.Function)
        .Declarations("fragment type interface union enum scalar input", SyntaxKind.Type)
        .Numbers()
        .PascalCaseTypes();
}
