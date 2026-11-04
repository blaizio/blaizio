namespace Blaizio.Docs;

/// <summary>
/// The docs' own language, added in Program.cs to show a custom <see cref="SyntaxGrammar"/>: an app
/// log, one entry per line - a timestamp, a level, a [category] and the message - and the stack
/// trace under an error. The Code Block page shows this definition as
/// Snippets/CodeBlock/CustomGrammar.txt: keep the two the same.
/// </summary>
public static class LogGrammar
{
    public static readonly SyntaxGrammar Instance = new SyntaxGrammar("log", "logs")
        .Pattern(@"\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?Z?", SyntaxKind.Meta)
        .Pattern(@"\[[\w.]+\]", SyntaxKind.Type)
        .Pattern(@"https?://\S+", SyntaxKind.Link)
        .Pattern(@"(?:\w+\.)*\w+Exception\b", SyntaxKind.Type)
        .Words("TRACE DEBUG", SyntaxKind.Comment)
        .Words("INFO", SyntaxKind.Inserted)
        .Words("WARN WARNING", SyntaxKind.Constant)
        .Words("ERROR FATAL", SyntaxKind.Deleted)
        .Keywords("at in")
        .String("\"")
        .Numbers()
        .Functions();
}
