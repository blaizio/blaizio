namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Elixir (<c>elixir</c>; <c>ex</c>, <c>exs</c>): interpolation (<c>#{expr}</c>), heredoc strings
    /// (<c>"""</c>), atoms and <c>key:</c> keyword lists, module attributes (<c>@doc</c>), regex sigils,
    /// <c>?</c> and <c>!</c> names, and the names after <c>def</c> and <c>defmodule</c>.
    /// </summary>
    public static SyntaxLanguage Elixir => field ??= new SyntaxGrammar("elixir", "ex", "exs")
        .Line("#!", SyntaxKind.Meta, lineStart: true)
        .LineComment("#")
        .String("\"\"\"", multiline: true, interpolation: SyntaxInterpolation.HashBraces)
        .String("'''", multiline: true, interpolation: SyntaxInterpolation.HashBraces)
        .String("\"", multiline: true, interpolation: SyntaxInterpolation.HashBraces)
        .String("'", interpolation: SyntaxInterpolation.HashBraces)
        .String("~r/", "/", kind: SyntaxKind.Escape)
        .String("~w(", ")", escape: '\0')
        .String("~w[", "]", escape: '\0')
        .Sigil(':', SyntaxKind.Constant, "?!")
        .Keys(":", SyntaxKind.Constant)
        .Sigil('@', SyntaxKind.Meta)
        .IdentifierChars("?!")
        .Keywords("after alias and catch cond def defdelegate defexception defguard defimpl defmacro defmacrop defmodule defoverridable defp defprotocol defstruct do else end fn for if import in not or quote raise receive require rescue try unless unquote use when with")
        .Constants("true false nil")
        .Declarations("def defp defmacro defmacrop defguard defdelegate", SyntaxKind.Function)
        .Declarations("defmodule defprotocol defimpl", SyntaxKind.Type)
        .Numbers()
        .Functions()
        .PascalCaseTypes();
}
