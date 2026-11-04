using System.Diagnostics.CodeAnalysis;

namespace Blaizio;

/// <summary>
/// Splits source code into runs of one <see cref="SyntaxKind"/> each, one list per line - the
/// tokenizing half of syntax highlighting; the styled layer decides what each kind looks like.
/// </summary>
/// <remarks>
/// <para>Every built-in language is a small hand-written scanner in C#, so highlighting works the
/// same in static server rendering, on Blazor Server, in WebAssembly and while prerendering, with
/// no script. The scanners read code the way a reader does - keywords, strings with the code inside
/// their interpolation holes, numbers, comments, type and function names by their shape, and one
/// language inside another (C# and HTML inside Razor, CSS and JavaScript inside HTML, any language
/// inside a Markdown fence) - rather than compiling it, and they never throw: text they cannot
/// place stays plain.</para>
/// <para>Each line is tokenized from the state the line before it ended in (an open comment, a
/// multi-line string, a nested block). <see cref="SyntaxDocument"/> keeps those states, so code
/// that grows - output streaming in - only tokenizes the lines that are new or changed.</para>
/// <para>These members know the 19 built-in languages. An app adds more (Blaizio.Syntax.Languages,
/// or its own <see cref="SyntaxGrammar"/>) in <c>AddBlaizio(options => options.Syntax.Add(...))</c>
/// and reaches them through <see cref="SyntaxOptions"/>.</para>
/// </remarks>
public static class SyntaxText
{
    /// <summary>
    /// The canonical names of the built-in languages: <c>csharp</c>, <c>razor</c>, <c>json</c>,
    /// <c>bash</c>, <c>console</c>, <c>powershell</c>, <c>typescript</c>, <c>javascript</c>,
    /// <c>html</c>, <c>xml</c>, <c>css</c>, <c>sql</c>, <c>yaml</c>, <c>diff</c>, <c>http</c>,
    /// <c>dockerfile</c>, <c>ini</c>, <c>toml</c> and <c>markdown</c>. Common aliases (<c>cs</c>,
    /// <c>cshtml</c>, <c>sh</c>, <c>ps1</c>, <c>ts</c>, <c>yml</c>, <c>md</c>, ...) work too.
    /// </summary>
    public static IReadOnlyList<string> Languages => SyntaxRegistry.BuiltIn.Names;

    /// <summary>Whether <paramref name="language"/> (a name or alias, any case) is a built-in language.</summary>
    public static bool IsSupported([NotNullWhen(true)] string? language) => SyntaxRegistry.BuiltIn.Find(language) is not null;

    /// <summary>
    /// The canonical name for <paramref name="language"/> (<c>"cs"</c> gives <c>"csharp"</c>), or
    /// <see langword="null"/> when it is not a built-in language.
    /// </summary>
    public static string? GetLanguageName(string? language) => SyntaxRegistry.BuiltIn.Find(language)?.Name;

    /// <summary>
    /// Tokenizes <paramref name="code"/> (<c>\n</c>, <c>\r\n</c> or <c>\r</c> separated) as
    /// <paramref name="language"/>, one <see cref="SyntaxLine"/> per line.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="language"/> is not a built-in language.</exception>
    public static IReadOnlyList<SyntaxLine> Tokenize(string code, string language)
    {
        ArgumentNullException.ThrowIfNull(code);
        return new SyntaxDocument(language).Update(code);
    }

    /// <summary>Splits text into lines on <c>\n</c>, <c>\r\n</c> and a lone <c>\r</c>.</summary>
    internal static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
}

/// <summary>
/// Code that changes over time, tokenized incrementally: each <see cref="Update(string)"/> keeps
/// the lines whose text and starting state are unchanged (the same <see cref="SyntaxLine"/>
/// instances) and tokenizes only the rest. Appending output - code streaming in - therefore costs
/// the new lines, and an edit costs the lines from the edit until the state the scanner carries
/// across lines settles back to what it was.
/// </summary>
/// <remarks>
/// Not thread-safe: one document belongs to one consumer (one component instance). A line longer
/// than <see cref="MaxLineLength"/> characters (minified output, a data dump) stays one plain
/// run, as editors do, so it can never slow the rest down. A line whose scanner throws (a custom
/// <see cref="SyntaxLanguage"/> with a bug) stays one plain run too, and the next line starts
/// from nothing open.
/// </remarks>
public sealed class SyntaxDocument
{
    /// <summary>The longest line that is tokenized; a longer one is a single plain run.</summary>
    public const int MaxLineLength = 10_000;

    private readonly SyntaxLanguage _language;
    private readonly SyntaxWriter _writer;
    private SyntaxLine[] _lines = [];

    /// <summary>
    /// A document for <paramref name="language"/>: a name or alias of a built-in language (see
    /// <see cref="SyntaxText.Languages"/>) or, with <paramref name="syntax"/>, of any language the
    /// app added in <c>AddBlaizio</c>. A language inside it (a Markdown fence, a script element)
    /// is looked up in the same set.
    /// </summary>
    /// <param name="language">The language's name or alias, any case.</param>
    /// <param name="syntax">The app's languages (<c>BlaizioOptions.Syntax</c>); <see langword="null"/> for the built-in ones only.</param>
    /// <exception cref="ArgumentException"><paramref name="language"/> is not one of them.</exception>
    public SyntaxDocument(string language, SyntaxOptions? syntax = null)
    {
        ArgumentNullException.ThrowIfNull(language);
        var registry = syntax?.Registry ?? SyntaxRegistry.BuiltIn;
        _language = registry.Find(language)
            ?? throw new ArgumentException(
                $"'{language}' is not a supported language. Check IsSupported first, or add the language in AddBlaizio (options.Syntax.Add).",
                nameof(language));
        _writer = new SyntaxWriter(registry);
    }

    /// <summary>The canonical name of the document's language.</summary>
    public string Language => _language.Name;

    /// <summary>The lines from the last update.</summary>
    public IReadOnlyList<SyntaxLine> Lines => _lines;

    /// <summary>How many lines the document has tokenized so far, across all updates (for tests).</summary>
    internal int TokenizedLineCount { get; private set; }

    /// <summary>Replaces the document's text (<c>\n</c>, <c>\r\n</c> or <c>\r</c> separated) and returns its lines.</summary>
    public IReadOnlyList<SyntaxLine> Update(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return Update(SyntaxText.SplitLines(code));
    }

    /// <summary>Replaces the document's lines and returns them, tokenized.</summary>
    public IReadOnlyList<SyntaxLine> Update(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var previous = _lines;
        var next = new SyntaxLine[lines.Count];
        object? state = null;
        for (var i = 0; i < next.Length; i++)
        {
            var text = lines[i] ?? "";
            var line = i < previous.Length ? previous[i] : null;
            if (line is null || !Equals(line.StartState, state) || !string.Equals(line.Text, text, StringComparison.Ordinal))
            {
                line = text.Length > MaxLineLength ? Plain(text, state, state) : Tokenize(text, state);
                TokenizedLineCount++;
            }
            next[i] = line;
            state = line.EndState;
        }
        _lines = next;
        return next;
    }

    private SyntaxLine Tokenize(string text, object? state)
    {
        _writer.Reset(text);
        try
        {
            var end = _language.Tokenize(text, 0, text.Length, state, _writer);
            return new SyntaxLine(text, _writer.Finish(), state, end);
        }
        catch (Exception)
        {
            // The built-in scanners never throw; an added language might. Its line reads plain and
            // the next one starts from nothing open, rather than the whole render failing.
            return Plain(text, state, null);
        }
    }

    private static SyntaxLine Plain(string text, object? start, object? end) =>
        new(text, text.Length == 0 ? [] : [new SyntaxRun(text, SyntaxKind.Text)], start, end);
}

/// <summary>One tokenized line: its text and the runs it splits into, in order.</summary>
public sealed class SyntaxLine
{
    internal SyntaxLine(string text, SyntaxRun[] runs, object? startState, object? endState)
    {
        Text = text;
        Runs = runs;
        StartState = startState;
        EndState = endState;
    }

    /// <summary>The line's text, without its line break.</summary>
    public string Text { get; }

    /// <summary>The runs, in order; their texts join back into <see cref="Text"/>. Empty for an empty line.</summary>
    public IReadOnlyList<SyntaxRun> Runs { get; }

    // What the scanner carried into and out of this line (an open comment, a nested block, ...).
    // Immutable, compared by value: an unchanged line from an unchanged state tokenizes the same.
    internal object? StartState { get; }

    internal object? EndState { get; }
}

/// <summary>A stretch of a line with one <see cref="SyntaxKind"/>, produced by <see cref="SyntaxText"/>.</summary>
/// <param name="Text">The source text of the run.</param>
/// <param name="Kind">What the text is.</param>
public readonly record struct SyntaxRun(string Text, SyntaxKind Kind);

/// <summary>What a run of source code is. The styled layer gives each kind its color.</summary>
public enum SyntaxKind
{
    /// <summary>Plain text: identifiers, punctuation, operators, whitespace.</summary>
    Text,

    /// <summary>A comment.</summary>
    Comment,

    /// <summary>A keyword: control flow, declarations, modifiers, SQL and shell keywords, CSS at-rules, Razor transitions and directives.</summary>
    Keyword,

    /// <summary>A string or character literal, a heredoc or block scalar body, inline code in Markdown.</summary>
    String,

    /// <summary>An escape sequence, the braces around an interpolation hole, a regular expression literal, an HTML entity.</summary>
    Escape,

    /// <summary>A number, including CSS dimensions and hex colors.</summary>
    Number,

    /// <summary>A built-in constant: <c>true</c>, <c>false</c>, <c>null</c>, <c>undefined</c>, <c>$true</c>, ...</summary>
    Constant,

    /// <summary>A type name: classes, interfaces, generic arguments, C# attributes, Razor components, SQL data types, TOML tables.</summary>
    Type,

    /// <summary>A function or method name where it is declared or called, a shell command, a PowerShell cmdlet.</summary>
    Function,

    /// <summary>A key: JSON, YAML, TOML and INI keys, CSS properties, HTTP header names.</summary>
    Property,

    /// <summary>An attribute name in markup, a command-line flag, a PowerShell parameter, a CSS class selector.</summary>
    Attribute,

    /// <summary>A markup element name.</summary>
    Tag,

    /// <summary>A variable: <c>$NAME</c> in shells and PowerShell, <c>@name</c> in SQL, CSS custom properties, <c>{{name}}</c> in HTTP files.</summary>
    Variable,

    /// <summary>Metadata: preprocessor directives, a shebang, diff file headers, document markers, console prompts.</summary>
    Meta,

    /// <summary>A line a diff adds.</summary>
    Inserted,

    /// <summary>A line a diff removes.</summary>
    Deleted,

    /// <summary>A Markdown heading.</summary>
    Heading,

    /// <summary>Emphasized (italic) Markdown text.</summary>
    Emphasis,

    /// <summary>Strong (bold) Markdown text.</summary>
    Strong,

    /// <summary>A link: Markdown link text, a URL, the target of an HTTP request.</summary>
    Link,
}
