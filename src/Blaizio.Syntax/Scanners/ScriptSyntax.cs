namespace Blaizio;

/// <summary>
/// JavaScript and TypeScript: one scanner, the TypeScript flag adds its primitive type names.
/// Template literals (with code inside <c>${ }</c>) and block comments carry across lines; a slash
/// where an expression can start is a regular expression. Upper-case names read as types (the
/// language's own convention), ALL_CAPS names as constants, a name before <c>(</c> as a function.
/// </summary>
internal sealed class ScriptSyntax : SyntaxLanguage
{
    public static readonly ScriptSyntax TypeScript = new(typeScript: true);

    public static readonly ScriptSyntax JavaScript = new(typeScript: false);

    private readonly bool _typeScript;

    private ScriptSyntax(bool typeScript) => _typeScript = typeScript;

    public override string Name => _typeScript ? "typescript" : "javascript";

    public override IReadOnlyList<string> Aliases =>
        _typeScript ? ["ts", "mts", "cts", "tsx"] : ["js", "mjs", "cjs", "jsx", "node"];

    /// <summary>Open constructs at the end of a line, innermost first.</summary>
    internal sealed record State(Mode Mode, int Depth, State? Outer);

    internal enum Mode : byte
    {
        BlockComment,
        Template,
        Hole,
    }

    private static readonly WordSet Keywords = new(false,
        "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do",
        "else", "enum", "export", "extends", "finally", "for", "function", "if", "import", "in",
        "instanceof", "let", "new", "return", "static", "super", "switch", "this", "throw", "try",
        "typeof", "var", "void", "while", "with", "yield", "await");

    // Keywords only when something follows that makes them one (`type Foo`, but `event.type`).
    private static readonly WordSet Contextual = new(false,
        "abstract", "accessor", "as", "asserts", "async", "declare", "from", "get", "global",
        "implements", "infer", "interface", "is", "keyof", "module", "namespace", "of", "out",
        "override", "package", "private", "protected", "public", "readonly", "satisfies", "set",
        "type", "unique", "using");

    private static readonly WordSet Constants = new(false, "true", "false", "null", "undefined", "NaN", "Infinity");

    private static readonly WordSet PrimitiveTypes = new(false,
        "string", "number", "boolean", "any", "unknown", "never", "object", "symbol", "bigint");

    // After these words a slash starts a regular expression, not a division.
    private static readonly WordSet RegexAfter = new(false,
        "return", "typeof", "case", "do", "else", "in", "of", "new", "delete", "void", "throw",
        "instanceof", "yield", "await");

    private static readonly WordSet TypeIntroducers = new(false,
        "class", "interface", "type", "enum", "extends", "implements", "instanceof", "new", "namespace");

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var lexer = new Lexer(this, s, start, end, w, state as State);
        while (lexer.Pos < end) lexer.Next();
        return lexer.EndLine();
    }

    private sealed class Lexer
    {
        private readonly ScriptSyntax _language;
        private readonly string _s;
        private readonly int _end;
        private readonly int _lineStart;
        private readonly SyntaxWriter _w;
        private readonly List<(Mode Mode, int Depth)> _stack = [];
        private char _lastSig;
        private bool _sawSig;
        private bool _regexAllowed = true;
        private bool _typeNext;
        private bool _functionNext;
        private int _typeArgsEnd = -1;

        public Lexer(ScriptSyntax language, string s, int pos, int end, SyntaxWriter w, State? state)
        {
            _language = language;
            _s = s;
            Pos = pos;
            _lineStart = pos;
            _end = end;
            _w = w;
            for (var f = state; f is not null; f = f.Outer) _stack.Insert(0, (f.Mode, f.Depth));
        }

        public int Pos;

        public State? EndLine()
        {
            State? state = null;
            foreach (var (mode, depth) in _stack) state = new State(mode, depth, state);
            return state;
        }

        public void Next()
        {
            if (_stack.Count > 0)
            {
                switch (_stack[^1].Mode)
                {
                    case Mode.BlockComment:
                        BlockComment();
                        return;
                    case Mode.Template:
                        TemplateBody();
                        return;
                }
            }
            Code();
        }

        private void Code()
        {
            var s = _s;
            var i = Pos;
            var c = s[i];
            var next = Scan.At(s, i + 1, _end);

            if (Scan.IsSpace(c))
            {
                Pos = Scan.Spaces(s, i, _end);
                _w.Add(i, Pos, SyntaxKind.Text);
                return;
            }
            if (c == '/' && next == '/')
            {
                _w.Add(i, _end, SyntaxKind.Comment);
                Pos = _end;
                return;
            }
            if (c == '/' && next == '*')
            {
                Pos = i + 2;
                _w.Add(i, Pos, SyntaxKind.Comment);
                _stack.Add((Mode.BlockComment, 0));
                BlockComment();
                return;
            }
            if (c == '#' && next == '!' && i == _lineStart && _stack.Count == 0)
            {
                _w.Add(i, _end, SyntaxKind.Meta);
                Pos = _end;
                return;
            }
            if (c is '"' or '\'')
            {
                Pos = Scan.QuotedWithEscapes(s, i, _end, _w, out _);
                Significant('"', regex: false);
                return;
            }
            if (c == '`')
            {
                _w.Add(i, i + 1, SyntaxKind.String);
                Pos = i + 1;
                _stack.Add((Mode.Template, 0));
                Significant('`', regex: false);
                return;
            }
            if (c == '/' && _regexAllowed && TryRegex(i)) return;
            if (Scan.IsNumberStart(s, i, _end))
            {
                Pos = Scan.Number(s, i, _end);
                _w.Add(i, Pos, SyntaxKind.Number);
                Significant('0', regex: false);
                return;
            }
            if (Scan.IsIdentStart(c) || c == '$')
            {
                Word(i);
                return;
            }
            if (c == '@' && (Scan.IsIdentStart(next) || next == '$'))
            {
                // A decorator: @Component(...), @Input()
                var e = Scan.Word(s, i + 1, _end, "$.");
                _w.Add(i, e, SyntaxKind.Function);
                Pos = e;
                Significant('a', regex: false);
                return;
            }

            var inHole = _stack.Count > 0 && _stack[^1].Mode == Mode.Hole;
            if (inHole && c == '}')
            {
                var (_, depth) = _stack[^1];
                if (depth == 0)
                {
                    _w.Add(i, i + 1, SyntaxKind.Escape);
                    _stack.RemoveAt(_stack.Count - 1);
                    Pos = i + 1;
                    return;
                }
                _stack[^1] = (Mode.Hole, depth - 1);
            }
            else if (inHole && c == '{')
                _stack[^1] = (Mode.Hole, _stack[^1].Depth + 1);

            Pos = i + (char.IsHighSurrogate(c) && i + 1 < _end ? 2 : 1);
            _w.Add(i, Pos, SyntaxKind.Text);
            // After ) ] } a slash divides; after other punctuation an expression (a regex) can start.
            Significant(c, regex: c is not (')' or ']' or '}'));
        }

        private void Word(int start)
        {
            var s = _s;
            var end = Scan.Word(s, start, _end, "$");
            var word = s.AsSpan(start, end - start);
            var member = _sawSig && _lastSig == '.';

            SyntaxKind kind;
            bool typeNext = false, functionNext = false, regex = false;
            if (!member && Constants.Contains(word))
                kind = SyntaxKind.Constant;
            else if (!member && Keywords.Contains(word))
            {
                kind = SyntaxKind.Keyword;
                typeNext = TypeIntroducers.Contains(word);
                functionNext = word is "function";
                regex = RegexAfter.Contains(word);
            }
            else if (!member && Contextual.Contains(word) && IsContextualKeyword(word, end))
            {
                kind = SyntaxKind.Keyword;
                typeNext = TypeIntroducers.Contains(word);
                regex = word is "of";
            }
            else if (_language._typeScript && !member && PrimitiveTypes.Contains(word))
                kind = SyntaxKind.Type;
            else
                kind = Classify(start, end, word);

            _w.Add(start, end, kind);
            Pos = end;
            _typeNext = typeNext;
            _functionNext = functionNext;
            _lastSig = 'a';
            _sawSig = true;
            _regexAllowed = regex;
        }

        private SyntaxKind Classify(int start, int end, ReadOnlySpan<char> word)
        {
            var s = _s;
            if (start < _typeArgsEnd) return SyntaxKind.Type;
            if (_functionNext) return SyntaxKind.Function;

            var after = Scan.At(s, end, _end);
            if (after == '<' && GenericEnd(end) is var close and > 0)
            {
                _typeArgsEnd = close;
                return Scan.At(s, close + 1, _end) == '(' && !_typeNext ? SyntaxKind.Function : SyntaxKind.Type;
            }
            if (_typeNext) return SyntaxKind.Type;
            if (after == '(') return SyntaxKind.Function;
            if (Scan.IsConstantCase(word)) return SyntaxKind.Constant;
            if (char.IsUpper(word[0])) return SyntaxKind.Type;
            return SyntaxKind.Text;
        }

        private bool IsContextualKeyword(ReadOnlySpan<char> word, int end)
        {
            var s = _s;
            var k = Scan.Spaces(s, end, _end);
            var nc = Scan.At(s, k, _end);
            if (word is "async") return nc is '(' || Scan.IsIdentStart(nc);
            if (word is "from") return nc is '"' or '\'';
            return Scan.IsIdentStart(nc) || nc is '$' or '{' or '*' or '[' or '"' or '\'' or '#';
        }

        // The index of the '>' closing a plausible type argument list at lt, or -1.
        private int GenericEnd(int lt)
        {
            var s = _s;
            var depth = 0;
            for (var k = lt; k < _end; k++)
            {
                var c = s[k];
                if (c == '<') depth++;
                else if (c == '>')
                {
                    if (--depth == 0) return k;
                }
                else if (!(Scan.IsIdentPart(c) || c is ' ' or ',' or '.' or '|' or '&' or '[' or ']' or '$' or '\'' or '"'))
                    return -1;
            }
            return -1;
        }

        private bool TryRegex(int i)
        {
            var s = _s;
            var inClass = false;
            for (var k = i + 1; k < _end; k++)
            {
                var c = s[k];
                if (c == '\\')
                {
                    k++;
                    continue;
                }
                if (c == '[') inClass = true;
                else if (c == ']') inClass = false;
                else if (c == '/' && !inClass)
                {
                    if (k == i + 1) return false;
                    var e = k + 1;
                    while (e < _end && char.IsAsciiLetter(s[e])) e++;
                    _w.Add(i, e, SyntaxKind.Escape);
                    Pos = e;
                    Significant('0', regex: false);
                    return true;
                }
            }
            return false;
        }

        private void TemplateBody()
        {
            var s = _s;
            var i = Pos;
            var run = i;
            while (i < _end)
            {
                var c = s[i];
                if (c == '\\' && i + 1 < _end)
                {
                    _w.Add(run, i, SyntaxKind.String);
                    var e = Scan.EscapeEnd(s, i, _end);
                    _w.Add(i, e, SyntaxKind.Escape);
                    i = run = e;
                    continue;
                }
                if (c == '`')
                {
                    _w.Add(run, i + 1, SyntaxKind.String);
                    _stack.RemoveAt(_stack.Count - 1);
                    Pos = i + 1;
                    Significant('"', regex: false);
                    return;
                }
                if (c == '$' && Scan.At(s, i + 1, _end) == '{')
                {
                    _w.Add(run, i, SyntaxKind.String);
                    _w.Add(i, i + 2, SyntaxKind.Escape);
                    _stack.Add((Mode.Hole, 0));
                    Pos = i + 2;
                    _regexAllowed = true;
                    return;
                }
                i++;
            }
            _w.Add(run, _end, SyntaxKind.String);
            Pos = _end;
        }

        private void BlockComment()
        {
            var close = Scan.IndexOf(_s, Pos, _end, "*/");
            if (close < 0)
            {
                _w.Add(Pos, _end, SyntaxKind.Comment);
                Pos = _end;
                return;
            }
            _w.Add(Pos, close + 2, SyntaxKind.Comment);
            Pos = close + 2;
            _stack.RemoveAt(_stack.Count - 1);
        }

        private void Significant(char c, bool regex)
        {
            _lastSig = c;
            _sawSig = true;
            _regexAllowed = regex;
            if (c != '.') _typeNext = _functionNext = false;
        }
    }
}
