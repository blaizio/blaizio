using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Blaizio;

/// <summary>Where a string's interpolation holes open. Combine flags for a language with several.</summary>
[Flags]
public enum SyntaxInterpolation
{
    /// <summary>No holes: the string is all text.</summary>
    None = 0,

    /// <summary><c>{expr}</c>, with <c>{{</c> and <c>}}</c> as literal braces: Python f-strings, F# interpolated strings.</summary>
    Braces = 1,

    /// <summary><c>${expr}</c>: Kotlin, Dart, Groovy, Scala, HCL, Bicep.</summary>
    DollarBraces = 2,

    /// <summary><c>#{expr}</c>: Ruby, Elixir.</summary>
    HashBraces = 4,

    /// <summary><c>\(expr)</c>: Swift.</summary>
    BackslashParens = 8,

    /// <summary><c>$name</c>, a bare name read as a variable: Kotlin, Dart, Groovy, Scala, PHP.</summary>
    DollarName = 16,
}

/// <summary>
/// A language described as a table rather than written as a scanner: its comments, strings, word
/// lists and naming conventions. Enough for most languages at the depth highlighting needs, and
/// the way Blaizio.Syntax.Languages defines its own.
/// </summary>
/// <remarks>
/// <para>Build it with the methods below, then add it in <c>AddBlaizio</c>:</para>
/// <code>
/// options.Syntax.Add(new SyntaxGrammar("gleam", "gl")
///     .LineComment("//")
///     .String("\"")
///     .Keywords("as assert case const fn if import let opaque panic pub todo type use")
///     .Constants("True False Nil")
///     .Declarations("fn", SyntaxKind.Function)
///     .Numbers()
///     .Functions()
///     .PascalCaseTypes());
/// </code>
/// <para>At each position in code, the first of these that applies wins: a <see cref="Pattern"/>, in
/// the order they were declared; a marker that opens a comment, string, line, heredoc or token, the
/// longest first; a number; a <see cref="Sigil"/> word; then a name, classified in this order: a
/// <see cref="Keys"/> key, a listed word (<see cref="Keywords"/>, <see cref="Constants"/>,
/// <see cref="Types"/>, <see cref="Words"/>), the name after a <see cref="Declarations"/> keyword,
/// a function call, an ALL_CAPS constant, a PascalCase type, the first word of a line. A name right
/// after a dot is a member (<c>re.match</c>), so it is never a listed word. Anything else is plain
/// text. Nothing is on unless declared - numbers included.</para>
/// <para>Block comments and multi-line strings carry across lines, and so do interpolation holes
/// inside a multi-line string. A grammar is fixed once it is added to an app or first used; build it
/// completely before that.</para>
/// </remarks>
public sealed class SyntaxGrammar : SyntaxLanguage
{
    // Deeper nesting than this (a string in a hole in a string, ...) reads as plain text.
    private const int MaxDepth = 16;

    private readonly string _name;
    private readonly string[] _aliases;
    private readonly List<Rule> _rules = [];
    private readonly List<(Regex Regex, SyntaxKind[] Kinds)> _patterns = [];
    private readonly List<(string Words, SyntaxKind Kind)> _words = [];
    private readonly List<(string Words, SyntaxKind Kind)> _declarations = [];
    private readonly List<(string Separators, SyntaxKind Kind, bool LineStart)> _keys = [];
    private readonly List<SigilRule> _sigils = [];
    private string _identifierChars = "";
    private bool _ignoreCase;
    private bool _functions;
    private char _macro;
    private bool _pascalCase;
    private bool _constantCase;
    private bool _numbers;
    private SyntaxKind? _firstWord;
    private Tables? _tables;

    /// <summary>A grammar for the language <paramref name="name"/>, also known as <paramref name="aliases"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or has spaces in it.</exception>
    public SyntaxGrammar(string name, params string[] aliases)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Any(char.IsWhiteSpace))
            throw new ArgumentException($"A language name cannot contain spaces: '{name}'.", nameof(name));
        _name = name;
        _aliases = [.. (aliases ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim())];
    }

    /// <inheritdoc />
    public override string Name => _name;

    /// <inheritdoc />
    public override IReadOnlyList<string> Aliases => _aliases;

    // ---- spans that open with a marker

    /// <summary>A comment from <paramref name="marker"/> (<c>//</c>, <c>#</c>, <c>--</c>) to the end of the line.</summary>
    /// <param name="marker">What opens it. A marker that starts or ends with a letter only counts as a whole word (<c>REM</c>).</param>
    /// <param name="lineStart">Only as the first thing on a line, after indentation.</param>
    public SyntaxGrammar LineComment(string marker, bool lineStart = false) => Line(marker, SyntaxKind.Comment, lineStart);

    /// <summary>A comment from <paramref name="open"/> to <paramref name="close"/>, across lines.</summary>
    /// <param name="open">What opens it (<c>/*</c>).</param>
    /// <param name="close">What closes it (<c>*/</c>).</param>
    /// <param name="nested">Whether an inner <paramref name="open"/> needs its own <paramref name="close"/>, as in Rust, Swift, Haskell and F#.</param>
    public SyntaxGrammar BlockComment(string open, string close, bool nested = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(open);
        ArgumentException.ThrowIfNullOrEmpty(close);
        return AddRule(new Rule { Type = RuleType.Block, Open = open, Close = close, Kind = SyntaxKind.Comment, Nested = nested });
    }

    /// <summary>
    /// Everything from <paramref name="marker"/> to the end of the line as <paramref name="kind"/>:
    /// a preprocessor line (<c>#include &lt;stdio.h&gt;</c> as <see cref="SyntaxKind.Meta"/>), say.
    /// </summary>
    /// <param name="marker">What starts it. A marker that starts or ends with a letter only counts as a whole word.</param>
    /// <param name="kind">What the line is.</param>
    /// <param name="lineStart">Only as the first thing on a line, after indentation.</param>
    public SyntaxGrammar Line(string marker, SyntaxKind kind, bool lineStart = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(marker);
        return AddRule(new Rule { Type = RuleType.Line, Open = marker, Kind = kind, LineStart = lineStart });
    }

    /// <summary>
    /// A string from <paramref name="open"/> to <paramref name="close"/>. Also any other span with a
    /// start and an end, through <paramref name="kind"/>: a Rust attribute <c>#[...]</c> as
    /// <see cref="SyntaxKind.Meta"/>, a Makefile <c>$(VAR)</c> as <see cref="SyntaxKind.Variable"/>.
    /// </summary>
    /// <param name="open">What opens it: <c>"</c>, <c>'''</c>, <c>@"</c>, <c>[[</c>.</param>
    /// <param name="close">What closes it; the same as <paramref name="open"/> when left out.</param>
    /// <param name="escape">
    /// The escape character: <c>\</c> (the default) reads <c>\n</c>, <c>\x41</c>, <c>é</c> and the
    /// like; the closing quote itself means a doubled quote is one (<c>""</c> in VB and SQL);
    /// <c>'\0'</c> means none (a raw string).
    /// </param>
    /// <param name="multiline">Whether it continues on the next line when it is not closed; otherwise the line end closes it.</param>
    /// <param name="interpolation">Where code holes open inside it, tokenized with this grammar.</param>
    /// <param name="prefixes">
    /// Words that must come right before <paramref name="open"/> for this rule to apply, any case,
    /// space-separated: <c>"r br rb"</c> for Python's raw strings. A rule with prefixes never
    /// matches a bare <paramref name="open"/>; declare that one separately.
    /// </param>
    /// <param name="kind">What the span is. <see cref="SyntaxKind.String"/> by default.</param>
    public SyntaxGrammar String(
        string open,
        string? close = null,
        char escape = '\\',
        bool multiline = false,
        SyntaxInterpolation interpolation = SyntaxInterpolation.None,
        string? prefixes = null,
        SyntaxKind kind = SyntaxKind.String)
    {
        ArgumentException.ThrowIfNullOrEmpty(open);
        if (close is { Length: 0 }) throw new ArgumentException("A string needs something to close it.", nameof(close));
        return AddRule(new Rule
        {
            Type = RuleType.String,
            Open = open,
            Close = close ?? open,
            Kind = kind,
            Escape = escape,
            Multiline = multiline,
            Interpolation = interpolation,
            Prefixes = prefixes is null ? null : SplitWords(prefixes),
        });
    }

    /// <summary>
    /// A character literal between two <paramref name="quote"/>s: one character or one escape
    /// (<c>'a'</c>, <c>'\n'</c>, <c>'A'</c>). Anything longer is not one, so the quote stays
    /// free for a Rust lifetime (<c>'a</c>) or a generic parameter (<c>'T</c>) - see <see cref="Sigil"/>.
    /// </summary>
    public SyntaxGrammar Char(char quote = '\'')
    {
        if (quote == '\0') throw new ArgumentException("A character literal needs a quote.", nameof(quote));
        return AddRule(new Rule { Type = RuleType.Char, Open = quote.ToString(), Kind = SyntaxKind.String });
    }

    /// <summary>
    /// A heredoc: <paramref name="marker"/> (<c>&lt;&lt;~</c>, <c>&lt;&lt;&lt;</c>) right before a
    /// terminator word - quoted, or upper-case when bare (<c>&lt;&lt;~SQL</c>, <c>&lt;&lt;&lt;'EOT'</c>) - makes
    /// the following lines a string, up to the line that starts with the terminator.
    /// </summary>
    public SyntaxGrammar Heredoc(string marker)
    {
        ArgumentException.ThrowIfNullOrEmpty(marker);
        return AddRule(new Rule { Type = RuleType.Heredoc, Open = marker, Kind = SyntaxKind.String });
    }

    /// <summary>
    /// <paramref name="text"/> as <paramref name="kind"/> wherever it appears in code: <c>&lt;?php</c>
    /// as <see cref="SyntaxKind.Meta"/>, or F#'s <c>(*)</c> operator as plain text so it does not open a comment.
    /// </summary>
    public SyntaxGrammar Token(string text, SyntaxKind kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return AddRule(new Rule { Type = RuleType.Token, Open = text, Kind = kind });
    }

    /// <summary>
    /// The escape hatch: a regular expression tried at each position in code before anything else,
    /// in the order patterns are declared. One kind colors the whole match; several color the
    /// pattern's capture groups in order, the rest of the match staying plain. A match never runs
    /// past the line, and an empty match does not count.
    /// </summary>
    /// <param name="pattern">A .NET regular expression, anchored at the position for you (no <c>^</c> needed).</param>
    /// <param name="kinds">The kind of the whole match, or one kind per capture group.</param>
    /// <exception cref="ArgumentException">The pattern is not a valid regular expression.</exception>
    /// <remarks>Patterns are the slowest rule, and a match that takes more than 100 ms throws - its line then reads plain.</remarks>
    public SyntaxGrammar Pattern([StringSyntax(StringSyntaxAttribute.Regex)] string pattern, params SyntaxKind[] kinds)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        if (kinds is not { Length: > 0 }) throw new ArgumentException("A pattern needs at least one kind.", nameof(kinds));
        ThrowIfSealed();
        var regex = new Regex(@"\G(?:" + pattern + ")", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        _patterns.Add((regex, [.. kinds]));
        return this;
    }

    // ---- words

    /// <summary>Keywords, space-separated: <c>"if else for while return"</c>.</summary>
    public SyntaxGrammar Keywords(string words) => Words(words, SyntaxKind.Keyword);

    /// <summary>Built-in constants, space-separated: <c>"true false null"</c>.</summary>
    public SyntaxGrammar Constants(string words) => Words(words, SyntaxKind.Constant);

    /// <summary>Built-in type names, space-separated: <c>"int float bool string"</c>.</summary>
    public SyntaxGrammar Types(string words) => Words(words, SyntaxKind.Type);

    /// <summary>Words of any <paramref name="kind"/>, space-separated. A word listed twice keeps its first kind.</summary>
    public SyntaxGrammar Words(string words, SyntaxKind kind)
    {
        ArgumentNullException.ThrowIfNull(words);
        ThrowIfSealed();
        _words.Add((words, kind));
        return this;
    }

    /// <summary>
    /// Keywords that name what follows them: the next name is <paramref name="kind"/>
    /// (<c>Declarations("def", SyntaxKind.Function)</c>, <c>Declarations("class struct", SyntaxKind.Type)</c>).
    /// The keywords themselves are <see cref="SyntaxKind.Keyword"/> unless listed otherwise.
    /// </summary>
    public SyntaxGrammar Declarations(string keywords, SyntaxKind kind)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        ThrowIfSealed();
        _declarations.Add((keywords, kind));
        return this;
    }

    /// <summary>
    /// A name directly followed by one of <paramref name="separators"/> (space-separated:
    /// <c>":"</c>, <c>"= :="</c>) is a key: HCL attributes, Bicep properties, GraphQL arguments. A
    /// separator followed by <c>=</c>, <c>:</c>, <c>&gt;</c> or <c>~</c> is another operator and does not count.
    /// </summary>
    /// <param name="separators">What follows a key.</param>
    /// <param name="kind">What a key is. <see cref="SyntaxKind.Property"/> by default.</param>
    /// <param name="lineStart">Only the first name on a line, which may then be followed by spaces before the separator (aligned assignments).</param>
    public SyntaxGrammar Keys(string separators, SyntaxKind kind = SyntaxKind.Property, bool lineStart = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(separators);
        ThrowIfSealed();
        _keys.Add((separators, kind, lineStart));
        return this;
    }

    /// <summary>The first word of a line, when nothing else claims it, is <paramref name="kind"/>: nginx directives.</summary>
    public SyntaxGrammar FirstWord(SyntaxKind kind)
    {
        ThrowIfSealed();
        _firstWord = kind;
        return this;
    }

    /// <summary>
    /// <paramref name="sigil"/> right before a name makes it <paramref name="kind"/>: <c>$name</c> as a
    /// variable, <c>@Override</c> as metadata, <c>:ok</c> as a constant. A run of sigils counts as one
    /// (<c>@@count</c>); a sigil right after a name does not count (<c>user@host</c>, <c>Foo::Bar</c>)
    /// unless that name was a sigil word itself (<c>$host$uri</c>).
    /// </summary>
    /// <param name="sigil">The marker.</param>
    /// <param name="kind">What the marked name is.</param>
    /// <param name="nameChars">Characters a name may hold besides letters, digits and <c>_</c> (<c>"."</c> for <c>@app.route</c>).</param>
    /// <param name="lineStart">Only as the first thing on a line (a batch file's <c>:label</c>).</param>
    /// <param name="closed">The name ends with the sigil too (<c>%PATH%</c>).</param>
    public SyntaxGrammar Sigil(char sigil, SyntaxKind kind, string? nameChars = null, bool lineStart = false, bool closed = false)
    {
        if (sigil == '\0') throw new ArgumentException("A sigil needs a character.", nameof(sigil));
        ThrowIfSealed();
        _sigils.Add(new SigilRule(sigil, kind, nameChars ?? "", lineStart, closed));
        return this;
    }

    /// <summary>
    /// Characters a name may hold besides letters, digits and <c>_</c>, after its first character:
    /// <c>"?!"</c> for Ruby's <c>empty?</c>, <c>"-"</c> for Lisp's <c>when-not</c>. One followed by <c>=</c> ends the name (<c>a!=b</c>).
    /// </summary>
    public SyntaxGrammar IdentifierChars(string chars)
    {
        ArgumentNullException.ThrowIfNull(chars);
        ThrowIfSealed();
        _identifierChars += chars;
        return this;
    }

    /// <summary>A name directly followed by <c>(</c> is a function, and with <paramref name="macro"/>, so is one followed by it (Rust's <c>println!</c>).</summary>
    public SyntaxGrammar Functions(char macro = '\0')
    {
        ThrowIfSealed();
        _functions = true;
        _macro = macro;
        return this;
    }

    /// <summary>A name that starts with an upper-case letter is a type (when nothing before it in the order claims it).</summary>
    public SyntaxGrammar PascalCaseTypes()
    {
        ThrowIfSealed();
        _pascalCase = true;
        return this;
    }

    /// <summary>An ALL_CAPS name (two letters or more) is a constant.</summary>
    public SyntaxGrammar ConstantCase()
    {
        ThrowIfSealed();
        _constantCase = true;
        return this;
    }

    /// <summary>Numbers: integers, decimals, exponents, <c>0x</c> / <c>0b</c> / <c>0o</c> prefixes, <c>_</c> separators and letter suffixes (<c>10L</c>, <c>1u8</c>).</summary>
    public SyntaxGrammar Numbers()
    {
        ThrowIfSealed();
        _numbers = true;
        return this;
    }

    /// <summary>Word lists, and markers made of letters (<c>REM</c>), match in any case: Visual Basic, batch files.</summary>
    public SyntaxGrammar IgnoreCase()
    {
        ThrowIfSealed();
        _ignoreCase = true;
        return this;
    }

    // ---- tokenizing

    /// <inheritdoc />
    public override object? Tokenize(string line, int start, int end, object? state, SyntaxWriter writer)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(writer);
        end = Math.Clamp(end, 0, line.Length);
        return new Scanner(Compile(), line, Math.Clamp(start, 0, end), end, writer, state as State).Run();
    }

    internal override void Seal() => Compile();

    private Tables Compile()
    {
        var tables = Volatile.Read(ref _tables);
        if (tables is not null) return tables;
        lock (_rules)
        {
            _tables ??= new Tables(this);
            return _tables;
        }
    }

    private SyntaxGrammar AddRule(Rule rule)
    {
        ThrowIfSealed();
        rule.Index = _rules.Count;
        _rules.Add(rule);
        return this;
    }

    private void ThrowIfSealed()
    {
        if (Volatile.Read(ref _tables) is not null)
            throw new InvalidOperationException(
                $"The '{_name}' grammar is already in use and cannot change. Finish building it before adding it to options.Syntax or tokenizing with it.");
    }

    private static string[] SplitWords(string words) => words.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private enum RuleType : byte
    {
        Line,
        Block,
        String,
        Char,
        Heredoc,
        Token,
    }

    private sealed class Rule
    {
        public RuleType Type;
        public int Index;
        public string Open = "";
        public string Close = "";
        public SyntaxKind Kind;
        public bool LineStart;
        public bool Nested;
        public char Escape;
        public bool Multiline;
        public SyntaxInterpolation Interpolation;
        public string[]? Prefixes;

        // Strings: the characters that can end a plain stretch of the body.
        public SearchValues<char>? Stops;
    }

    private sealed record SigilRule(char Sigil, SyntaxKind Kind, string NameChars, bool LineStart, bool Closed);

    /// <summary>The grammar compiled for lookups: fixed once built, shared by every line and thread.</summary>
    private sealed class Tables
    {
        public Tables(SyntaxGrammar g)
        {
            IgnoreCase = g._ignoreCase;
            var comparer = g._ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            Rules = [.. g._rules];
            Patterns = [.. g._patterns];
            IdentifierChars = g._identifierChars;
            Functions = g._functions;
            Macro = g._macro;
            PascalCase = g._pascalCase;
            ConstantCase = g._constantCase;
            Numbers = g._numbers;
            FirstWord = g._firstWord;
            Sigils = [.. g._sigils];
            Keys = [.. g._keys
                .SelectMany(k => SplitWords(k.Separators).Select(s => (Separator: s, k.Kind, k.LineStart)))
                .OrderByDescending(k => k.Separator.Length)];

            var words = new Dictionary<string, SyntaxKind>(comparer);
            foreach (var (list, kind) in g._words)
            foreach (var word in SplitWords(list))
                words.TryAdd(word, kind);
            var declarations = new Dictionary<string, SyntaxKind>(comparer);
            foreach (var (list, kind) in g._declarations)
            foreach (var word in SplitWords(list))
            {
                declarations.TryAdd(word, kind);
                words.TryAdd(word, SyntaxKind.Keyword);
            }
            Words = words.GetAlternateLookup<ReadOnlySpan<char>>();
            Declarations = declarations.GetAlternateLookup<ReadOnlySpan<char>>();

            // Openers by their first character, longest first so <<~ beats << and """ beats ".
            // Prefixed strings are found from the word before them instead.
            var byChar = new Dictionary<char, List<Rule>>();
            var prefixed = new Dictionary<string, List<Rule>>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in Rules)
            {
                if (rule.Type == RuleType.String)
                {
                    var stops = new List<char> { rule.Close[0] };
                    if (rule.Escape != '\0') stops.Add(rule.Escape);
                    if (rule.Interpolation.HasFlag(SyntaxInterpolation.Braces)) stops.AddRange("{}");
                    if (rule.Interpolation.HasFlag(SyntaxInterpolation.DollarBraces) || rule.Interpolation.HasFlag(SyntaxInterpolation.DollarName)) stops.Add('$');
                    if (rule.Interpolation.HasFlag(SyntaxInterpolation.HashBraces)) stops.Add('#');
                    if (rule.Interpolation.HasFlag(SyntaxInterpolation.BackslashParens)) stops.Add('\\');
                    rule.Stops = SearchValues.Create([.. stops.Distinct()]);
                }
                if (rule.Prefixes is { } prefixes)
                {
                    foreach (var prefix in prefixes)
                    {
                        if (!prefixed.TryGetValue(prefix, out var list)) prefixed[prefix] = list = [];
                        list.Add(rule);
                    }
                    continue;
                }
                var first = rule.Open[0];
                char[] keys = g._ignoreCase && char.IsLetter(first) ? [char.ToLowerInvariant(first), char.ToUpperInvariant(first)] : [first];
                foreach (var c in keys.Distinct())
                {
                    if (!byChar.TryGetValue(c, out var list)) byChar[c] = list = [];
                    list.Add(rule);
                }
            }
            foreach (var (c, list) in byChar)
            {
                var sorted = list.OrderByDescending(r => r.Open.Length).ToArray();
                if (c < 128) Ascii[c] = sorted;
                else (Other ??= [])[c] = sorted;
            }
            Prefixed = prefixed.Count == 0
                ? null
                : prefixed.ToDictionary(p => p.Key, p => p.Value.OrderByDescending(r => r.Open.Length).ToArray(), StringComparer.OrdinalIgnoreCase)
                    .GetAlternateLookup<ReadOnlySpan<char>>();
        }

        public Rule[] Rules { get; }

        public (Regex Regex, SyntaxKind[] Kinds)[] Patterns { get; }

        public Rule[]?[] Ascii { get; } = new Rule[]?[128];

        public Dictionary<char, Rule[]>? Other { get; }

        public Dictionary<string, Rule[]>.AlternateLookup<ReadOnlySpan<char>>? Prefixed { get; }

        public Dictionary<string, SyntaxKind>.AlternateLookup<ReadOnlySpan<char>> Words { get; }

        public Dictionary<string, SyntaxKind>.AlternateLookup<ReadOnlySpan<char>> Declarations { get; }

        public (string Separator, SyntaxKind Kind, bool LineStart)[] Keys { get; }

        public SigilRule[] Sigils { get; }

        public string IdentifierChars { get; }

        public bool IgnoreCase { get; }

        public bool Functions { get; }

        public char Macro { get; }

        public bool PascalCase { get; }

        public bool ConstantCase { get; }

        public bool Numbers { get; }

        public SyntaxKind? FirstWord { get; }

        public Rule[]? OpenersFor(char c) => c < 128 ? Ascii[c] : Other?.GetValueOrDefault(c);
    }

    internal enum Mode : byte
    {
        Comment,
        String,
        Hole,
        Heredoc,
    }

    /// <summary>
    /// What is open at the end of a line, innermost first: a block comment (<paramref name="Depth"/>
    /// = its nesting), a string, a code hole inside a string (<paramref name="Close"/> = its closing
    /// bracket, <paramref name="Depth"/> = brackets open inside it) or a heredoc
    /// (<paramref name="Word"/> = its terminator). <paramref name="Rule"/> is the rule that opened it.
    /// </summary>
    internal sealed record State(Mode Mode, int Rule, int Depth, char Close, string? Word, State? Outer);

    private struct Frame
    {
        public Mode Mode;
        public int Rule;
        public int Depth;
        public char Close;
        public string? Word;
    }

    private sealed class Scanner
    {
        private readonly Tables _t;
        private readonly string _s;
        private readonly int _start;
        private readonly int _end;
        private readonly SyntaxWriter _w;
        private readonly List<Frame> _stack = [];
        private int _i;
        private bool _seen;
        private SyntaxKind? _pending;
        private string? _heredoc;
        private int _heredocRule;
        private int _sigilEnd = -1;

        public Scanner(Tables t, string s, int start, int end, SyntaxWriter w, State? state)
        {
            _t = t;
            _s = s;
            _start = _i = start;
            _end = end;
            _w = w;
            for (var f = state; f is not null; f = f.Outer)
            {
                // A state from another version of the grammar: start fresh rather than misread it.
                if (f.Rule < 0 || f.Rule >= t.Rules.Length)
                {
                    _stack.Clear();
                    break;
                }
                _stack.Insert(0, new Frame { Mode = f.Mode, Rule = f.Rule, Depth = f.Depth, Close = f.Close, Word = f.Word });
            }
        }

        private ref Frame Top => ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_stack)[^1];

        public State? Run()
        {
            while (_i < _end)
            {
                if (_stack.Count == 0 || Top.Mode == Mode.Hole)
                {
                    Code();
                    continue;
                }
                switch (Top.Mode)
                {
                    case Mode.Comment:
                        Comment();
                        break;
                    case Mode.String:
                        StringBody();
                        break;
                    case Mode.Heredoc:
                        HeredocLine();
                        break;
                }
            }

            // What does not cross a line end closes with it: a one-line string, and a hole in one.
            while (_stack.Count > 0 && Top.Mode is Mode.String or Mode.Hole && !_t.Rules[Top.Rule].Multiline)
                _stack.RemoveAt(_stack.Count - 1);
            if (_heredoc is not null && _stack.Count < MaxDepth)
                _stack.Add(new Frame { Mode = Mode.Heredoc, Rule = _heredocRule, Word = _heredoc });

            State? state = null;
            foreach (var f in _stack) state = new State(f.Mode, f.Rule, f.Depth, f.Close, f.Word, state);
            return state;
        }

        private void Emit(int start, int end, SyntaxKind kind)
        {
            _w.Add(start, end, kind);
            _seen = true;
            _pending = null;
        }

        private char At(int i) => Scan.At(_s, i, _end);

        private bool IsNameChar(char c) => Scan.IsIdentPart(c) || (c != '\0' && _t.IdentifierChars.Contains(c));

        private int NameEnd(int k)
        {
            while (k < _end)
            {
                var c = _s[k];
                if (Scan.IsIdentPart(c) || (_t.IdentifierChars.Contains(c) && At(k + 1) != '=')) k++;
                else break;
            }
            return k;
        }

        private bool OnlySpacesBefore(int i) => Scan.OnlySpacesBefore(_s, _start, i);

        // ---- code

        private void Code()
        {
            var s = _s;
            var c = s[_i];
            if (Scan.IsSpace(c))
            {
                var e = Scan.Spaces(s, _i, _end);
                _w.Add(_i, e, SyntaxKind.Text);
                _i = e;
                return;
            }

            if (_stack.Count > 0 && Top.Mode == Mode.Hole)
            {
                var open = Top.Close == ')' ? '(' : '{';
                if (c == open)
                {
                    Top.Depth++;
                    Emit(_i, ++_i, SyntaxKind.Text);
                    return;
                }
                if (c == Top.Close)
                {
                    if (Top.Depth == 0)
                    {
                        Emit(_i, ++_i, SyntaxKind.Escape);
                        _stack.RemoveAt(_stack.Count - 1);
                        return;
                    }
                    Top.Depth--;
                    Emit(_i, ++_i, SyntaxKind.Text);
                    return;
                }
            }

            foreach (var (regex, kinds) in _t.Patterns)
                if (TryPattern(regex, kinds)) return;

            if (_t.OpenersFor(c) is { } openers)
                foreach (var rule in openers)
                    if (TryOpen(rule, _i, _i)) return;

            if (_t.Numbers && Scan.IsNumberStart(s, _i, _end) && !(_i > _start && IsNameChar(s[_i - 1])))
            {
                // A suffix with digits in it is still the number's: 10u8, 1_i64, 2.0f32.
                var e = Scan.Number(s, _i, _end);
                while (e < _end && Scan.IsIdentPart(s[e])) e++;
                Emit(_i, e, SyntaxKind.Number);
                _i = e;
                return;
            }

            if (_t.Sigils.Length > 0 && TrySigil(c)) return;

            if (Scan.IsIdentStart(c))
            {
                Name();
                return;
            }

            Emit(_i, ++_i, SyntaxKind.Text);
        }

        private bool TryPattern(Regex regex, SyntaxKind[] kinds)
        {
            var m = regex.Match(_s, _i);
            if (!m.Success || m.Length == 0 || m.Index != _i) return false;
            var end = Math.Min(m.Index + m.Length, _end);
            if (kinds.Length == 1)
                Emit(_i, end, kinds[0]);
            else
            {
                for (var g = 1; g < m.Groups.Count && g <= kinds.Length; g++)
                {
                    var group = m.Groups[g];
                    if (group.Success && group.Length > 0) Emit(group.Index, Math.Min(group.Index + group.Length, end), kinds[g - 1]);
                }
                _w.Add(_i, end, SyntaxKind.Text);
            }
            _i = end;
            return true;
        }

        // A rule's marker at p (or, for a prefixed string, at markerAt after its prefix word at p).
        private bool TryOpen(Rule rule, int p, int markerAt)
        {
            var s = _s;
            var open = rule.Open;
            var at = _t.IgnoreCase && rule.Type is RuleType.Line or RuleType.Token
                ? Scan.AtIgnoreCase(s, markerAt, _end, open)
                : Scan.At(s, markerAt, _end, open);
            if (!at) return false;
            if (p == markerAt && Scan.IsIdentPart(open[0]) && p > _start && IsNameChar(s[p - 1])) return false;
            var after = markerAt + open.Length;
            if (Scan.IsIdentPart(open[^1]) && IsNameChar(At(after))) return false;
            if (rule.LineStart && !OnlySpacesBefore(p)) return false;

            switch (rule.Type)
            {
                case RuleType.Line:
                    Emit(p, _end, rule.Kind);
                    _i = _end;
                    return true;
                case RuleType.Token:
                    Emit(p, after, rule.Kind);
                    _i = after;
                    return true;
                case RuleType.Block:
                    if (_stack.Count >= MaxDepth) return false;
                    Emit(p, after, rule.Kind);
                    _i = after;
                    _stack.Add(new Frame { Mode = Mode.Comment, Rule = rule.Index });
                    return true;
                case RuleType.String:
                    if (_stack.Count >= MaxDepth) return false;
                    Emit(p, after, rule.Kind);
                    _i = after;
                    _stack.Add(new Frame { Mode = Mode.String, Rule = rule.Index });
                    return true;
                case RuleType.Char:
                    return TryChar(rule, p);
                default:
                    return TryHeredoc(rule, p, after);
            }
        }

        private bool TryChar(Rule rule, int p)
        {
            var s = _s;
            var quote = rule.Open[0];
            var k = p + 1;
            if (k >= _end || s[k] == quote) return false;
            var body = k;
            k = s[k] == '\\' ? Scan.EscapeEnd(s, k, _end) : k + (char.IsHighSurrogate(s[k]) && k + 1 < _end ? 2 : 1);
            if (At(k) != quote) return false;
            Emit(p, body, rule.Kind);
            _w.Add(body, k, s[body] == '\\' ? SyntaxKind.Escape : rule.Kind);
            _w.Add(k, k + 1, rule.Kind);
            _i = k + 1;
            return true;
        }

        private bool TryHeredoc(Rule rule, int p, int k)
        {
            var s = _s;
            var quote = At(k) is '\'' or '"' ? s[k] : '\0';
            var word = quote == '\0' ? k : k + 1;
            if (!Scan.IsIdentStart(At(word)) || (quote == '\0' && !char.IsUpper(s[word]))) return false;
            var wordEnd = Scan.Identifier(s, word, _end);
            if (quote != '\0' && At(wordEnd) != quote) return false;
            var end = quote == '\0' ? wordEnd : wordEnd + 1;
            Emit(p, end, rule.Kind);
            if (_heredoc is null)
            {
                _heredoc = s[word..wordEnd];
                _heredocRule = rule.Index;
            }
            _i = end;
            return true;
        }

        private bool TrySigil(char c)
        {
            var s = _s;
            foreach (var sigil in _t.Sigils)
            {
                if (sigil.Sigil != c) continue;
                // Not right after a name (user@host, Foo::Bar), unless the name was a sigil word ($host$uri).
                if (_i > _start && _sigilEnd != _i && (IsNameChar(s[_i - 1]) || s[_i - 1] == c)) continue;
                if (sigil.LineStart && !OnlySpacesBefore(_i)) continue;
                var k = _i;
                while (k < _end && s[k] == c) k++;
                var name = k;
                while (k < _end && (Scan.IsIdentPart(s[k]) || sigil.NameChars.Contains(s[k]))) k++;
                if (k == name) continue;
                if (sigil.Closed && At(k) == c) k++;
                Emit(_i, k, sigil.Kind);
                _sigilEnd = k;
                _i = k;
                return true;
            }
            return false;
        }

        private void Name()
        {
            var s = _s;
            var i = _i;
            var k = NameEnd(i + 1);
            var word = s.AsSpan(i, k - i);

            // A prefixed string: r"\d+", f"{x}", b'..'.
            if (_t.Prefixed is { } prefixed && k < _end && prefixed.TryGetValue(word, out var rules))
                foreach (var rule in rules)
                    if (TryOpen(rule, i, k)) return;

            // A name after a dot is a member (re.match, obj.class): never one of the listed words.
            var member = i > _start && s[i - 1] == '.' && (i - 1 == _start || s[i - 2] != '.');
            var end = k;
            SyntaxKind kind;
            if (KeyAfter(i, k) is { } key) kind = key;
            else if (!member && _t.Words.TryGetValue(word, out var listed)) kind = listed;
            else if (_pending is { } pending) kind = pending;
            else if (_t.Macro != '\0' && At(k) == _t.Macro && At(k + 1) != '=')
            {
                kind = SyntaxKind.Function;
                end = k + 1;
            }
            else if (_t.Functions && At(k) == '(') kind = SyntaxKind.Function;
            else if (_t.ConstantCase && Scan.IsConstantCase(word)) kind = SyntaxKind.Constant;
            else if (_t.PascalCase && char.IsUpper(word[0])) kind = SyntaxKind.Type;
            else if (_t.FirstWord is { } first && !_seen) kind = first;
            else kind = SyntaxKind.Text;

            Emit(i, end, kind);
            if (!member && _t.Declarations.TryGetValue(word, out var declared)) _pending = declared;
            _i = end;
        }

        // The key kind when the name at i..k is followed by a separator, else null.
        private SyntaxKind? KeyAfter(int i, int k)
        {
            foreach (var (separator, kind, lineStart) in _t.Keys)
            {
                if (lineStart && (_seen || !OnlySpacesBefore(i))) continue;
                var at = lineStart ? Scan.Spaces(_s, k, _end) : k;
                if (!Scan.At(_s, at, _end, separator)) continue;
                if (At(at + separator.Length) is '=' or ':' or '>' or '~') continue;
                return kind;
            }
            return null;
        }

        // ---- comments, strings, heredocs

        private void Comment()
        {
            var s = _s;
            var rule = _t.Rules[Top.Rule];
            var i = _i;
            while (true)
            {
                var close = Scan.IndexOf(s, i, _end, rule.Close);
                var open = rule.Nested ? Scan.IndexOf(s, i, _end, rule.Open) : -1;
                if (open >= 0 && (close < 0 || open < close))
                {
                    Top.Depth++;
                    i = open + rule.Open.Length;
                    continue;
                }
                if (close < 0)
                {
                    Emit(_i, _end, rule.Kind);
                    _i = _end;
                    return;
                }
                i = close + rule.Close.Length;
                if (Top.Depth > 0)
                {
                    Top.Depth--;
                    continue;
                }
                Emit(_i, i, rule.Kind);
                _i = i;
                _stack.RemoveAt(_stack.Count - 1);
                return;
            }
        }

        private void StringBody()
        {
            var s = _s;
            var rule = _t.Rules[Top.Rule];
            var interpolation = rule.Interpolation;
            var run = _i;
            while (_i < _end)
            {
                var c = s[_i];
                var next = At(_i + 1);
                if (c == '\\' && next == '(' && interpolation.HasFlag(SyntaxInterpolation.BackslashParens))
                {
                    OpenHole(run, 2, ')');
                    return;
                }
                if (c == rule.Escape && c != '\0' && _i + 1 < _end && (rule.Escape != rule.Close[0] || next == rule.Escape))
                {
                    _w.Add(run, _i, rule.Kind);
                    var e = rule.Escape == '\\' ? Scan.EscapeEnd(s, _i, _end) : _i + 2;
                    Emit(_i, e, SyntaxKind.Escape);
                    _i = run = e;
                    continue;
                }
                if (Scan.At(s, _i, _end, rule.Close))
                {
                    var e = _i + rule.Close.Length;
                    Emit(run, e, rule.Kind);
                    _i = e;
                    _stack.RemoveAt(_stack.Count - 1);
                    return;
                }
                if (interpolation != SyntaxInterpolation.None)
                {
                    if (interpolation.HasFlag(SyntaxInterpolation.Braces) && (c, next) is ('{', '{') or ('}', '}'))
                    {
                        _w.Add(run, _i, rule.Kind);
                        Emit(_i, _i + 2, SyntaxKind.Escape);
                        _i = run = _i + 2;
                        continue;
                    }
                    if (c == '{' && interpolation.HasFlag(SyntaxInterpolation.Braces))
                    {
                        OpenHole(run, 1, '}');
                        return;
                    }
                    if ((c == '$' && next == '{' && interpolation.HasFlag(SyntaxInterpolation.DollarBraces))
                        || (c == '#' && next == '{' && interpolation.HasFlag(SyntaxInterpolation.HashBraces)))
                    {
                        OpenHole(run, 2, '}');
                        return;
                    }
                    if (c == '$' && Scan.IsIdentStart(next) && interpolation.HasFlag(SyntaxInterpolation.DollarName))
                    {
                        _w.Add(run, _i, rule.Kind);
                        var e = Scan.Identifier(s, _i + 1, _end);
                        Emit(_i, e, SyntaxKind.Variable);
                        _i = run = e;
                        continue;
                    }
                }
                var stop = s.AsSpan(_i + 1, _end - _i - 1).IndexOfAny(rule.Stops!);
                _i = stop < 0 ? _end : _i + 1 + stop;
            }
            Emit(run, _end, rule.Kind);
        }

        private void OpenHole(int run, int length, char close)
        {
            var rule = Top.Rule;
            _w.Add(run, _i, _t.Rules[rule].Kind);
            Emit(_i, _i + length, SyntaxKind.Escape);
            _i += length;
            if (_stack.Count < MaxDepth) _stack.Add(new Frame { Mode = Mode.Hole, Rule = rule, Close = close });
        }

        private void HeredocLine()
        {
            var word = Top.Word!;
            var k = Scan.Spaces(_s, _i, _end);
            if (Scan.At(_s, k, _end, word) && !IsNameChar(At(k + word.Length)))
            {
                var e = k + word.Length;
                Emit(_i, e, _t.Rules[Top.Rule].Kind);
                _i = e;
                _stack.RemoveAt(_stack.Count - 1);
                return;
            }
            Emit(_i, _end, _t.Rules[Top.Rule].Kind);
            _i = _end;
        }
    }
}
