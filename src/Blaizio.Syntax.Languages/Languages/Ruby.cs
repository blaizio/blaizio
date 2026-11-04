namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Ruby (<c>ruby</c>; <c>rb</c>, <c>rake</c>, <c>gemspec</c>, <c>podspec</c>): interpolation
    /// (<c>#{expr}</c>), heredocs (<c>&lt;&lt;~SQL</c>), symbols and <c>key:</c> hash keys,
    /// <c>@instance</c> and <c>$global</c> variables, <c>=begin</c> comments, and the names after
    /// <c>def</c>, <c>class</c> and <c>module</c>.
    /// </summary>
    public static SyntaxLanguage Ruby => field ??= new SyntaxGrammar("ruby", "rb", "rake", "gemspec", "podspec")
        .Line("#!", SyntaxKind.Meta, lineStart: true)
        .LineComment("#")
        .BlockComment("=begin", "=end")
        .String("\"", multiline: true, interpolation: SyntaxInterpolation.HashBraces)
        .String("'", multiline: true)
        .String("`", interpolation: SyntaxInterpolation.HashBraces)
        .String("%w[", "]", escape: '\0')
        .String("%w(", ")", escape: '\0')
        .String("%i[", "]", escape: '\0')
        .String("%i(", ")", escape: '\0')
        .Heredoc("<<~")
        .Heredoc("<<-")
        .Heredoc("<<")
        .Sigil('@', SyntaxKind.Variable)
        .Sigil('$', SyntaxKind.Variable)
        .Sigil(':', SyntaxKind.Constant, "?!")
        .Keys(":", SyntaxKind.Constant)
        .IdentifierChars("?!")
        .Keywords("BEGIN END alias and begin break case class def defined? do else elsif end ensure for if in module next not or redo rescue retry return self super then undef unless until when while yield require require_relative include extend prepend attr_reader attr_writer attr_accessor private protected public raise lambda proc loop __method__ __dir__")
        .Constants("true false nil __FILE__ __LINE__")
        .Words("puts print printf pp", SyntaxKind.Function)
        .Declarations("def", SyntaxKind.Function)
        .Declarations("class module", SyntaxKind.Type)
        .Numbers()
        .Functions()
        .PascalCaseTypes();
}
