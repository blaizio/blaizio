namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Perl (<c>perl</c>; <c>pl</c>, <c>pm</c>): <c>$scalar</c>, <c>@array</c> and <c>%hash</c>
    /// variables, interpolating strings, heredocs, POD blocks as comments, and the names after
    /// <c>sub</c> and <c>package</c>.
    /// </summary>
    public static SyntaxLanguage Perl => field ??= new SyntaxGrammar("perl", "pl", "pm")
        .Line("#!", SyntaxKind.Meta, lineStart: true)
        .LineComment("#")
        .BlockComment("=pod", "=cut")
        .BlockComment("=head1", "=cut")
        .BlockComment("=head2", "=cut")
        .BlockComment("=begin", "=cut")
        .BlockComment("=over", "=cut")
        .String("\"", multiline: true, interpolation: SyntaxInterpolation.DollarName)
        .String("'", multiline: true)
        .String("`")
        .Heredoc("<<~")
        .Heredoc("<<")
        .Sigil('$', SyntaxKind.Variable)
        .Sigil('@', SyntaxKind.Variable)
        .Sigil('%', SyntaxKind.Variable)
        .Keywords("my our local use no package sub return if elsif else unless while until for foreach do last next redo goto and or not xor eq ne lt gt le ge cmp require BEGIN END print printf say die warn eval wantarray defined undef ref bless qw q qq")
        .Constants("__PACKAGE__ __FILE__ __LINE__ __DATA__ __END__")
        .Declarations("sub", SyntaxKind.Function)
        .Declarations("package", SyntaxKind.Type)
        .Numbers()
        .Functions();
}
