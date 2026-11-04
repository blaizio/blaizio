namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// nginx configuration (<c>nginx</c>; <c>nginxconf</c>): block names (<c>server</c>,
    /// <c>location</c>, ...) as keywords, the directive starting each line as a key,
    /// <c>$variables</c>, sizes and durations as numbers.
    /// </summary>
    public static SyntaxLanguage Nginx => field ??= new SyntaxGrammar("nginx", "nginxconf")
        .LineComment("#")
        .String("\"")
        .String("'")
        .Sigil('$', SyntaxKind.Variable)
        .Keywords("server location upstream events stream mail map geo types if include return rewrite set limit_except split_clients")
        .Constants("on off")
        .FirstWord(SyntaxKind.Property)
        .Numbers();
}
