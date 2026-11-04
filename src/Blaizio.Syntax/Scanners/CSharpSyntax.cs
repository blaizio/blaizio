using System.Runtime.InteropServices;

namespace Blaizio;

/// <summary>C#: the language, and the lexer Razor drives token by token inside its code.</summary>
internal sealed class CSharpSyntax : SyntaxLanguage
{
    public static readonly CSharpSyntax Instance = new();

    public override string Name => "csharp";

    public override IReadOnlyList<string> Aliases => ["cs", "c#", "c-sharp", "csx"];

    public override object? Tokenize(string line, int start, int end, object? state, SyntaxWriter writer)
    {
        var lexer = new CSharpLexer(line, start, end, writer, state as CSharpLexer.State);
        while (lexer.Pos < end) lexer.Next();
        return lexer.EndLine();
    }
}

/// <summary>
/// Scans C# one token per <see cref="Next"/> call. Multi-line constructs (block comments,
/// verbatim and raw strings, interpolation holes that span lines) live on a small stack that
/// becomes the line's end <see cref="State"/>. Type and function names come from their shape:
/// declarations (<c>Order order</c>), generic arguments, <c>new</c> / <c>is</c> / <c>as</c> /
/// <c>typeof(...)</c>, base lists and constraints, attribute sections, <c>IName</c> interfaces,
/// and a name followed by <c>(</c>.
/// </summary>
internal sealed class CSharpLexer
{
    /// <summary>The constructs open at the end of a line, innermost first.</summary>
    internal sealed record State(Mode Mode, int Quotes, int Dollars, bool Interpolated, int Depth, int Parens, State? Outer);

    internal enum Mode : byte
    {
        BlockComment,
        String,
        Verbatim,
        Raw,
        Hole,
    }

    private struct Frame
    {
        public Mode Mode;
        public int Quotes;
        public int Dollars;
        public bool Interpolated;
        public int Depth;
        public int Parens;
    }

    private enum Prev : byte
    {
        None,
        New,
        TypeDeclaration,
        IsAs,
        TypeOperator,
        Where,
    }

    private static readonly WordSet Keywords = new(false,
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
        "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
        "explicit", "extern", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
        "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "object",
        "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref",
        "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct",
        "switch", "this", "throw", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while");

    private static readonly WordSet Constants = new(false, "true", "false", "null");

    // Keywords only in context: identifiers elsewhere (var, async, get, ...).
    private static readonly WordSet Contextual = new(false,
        "add", "allows", "and", "async", "await", "dynamic", "extension", "file", "get", "global",
        "init", "managed", "nameof", "nint", "not", "notnull", "nuint", "or", "partial", "record",
        "remove", "required", "scoped", "set", "unmanaged", "var", "when", "where", "with", "yield");

    private static readonly WordSet TypeDeclarationModifiers = new(false,
        "class", "struct", "record", "interface", "enum", "delegate", "sealed", "static", "abstract",
        "partial", "readonly", "unsafe", "ref");

    // Words after which an upper-case name is not a variable's declared type (`Value is null`).
    private static readonly WordSet NotDeclarators = new(false,
        "is", "as", "and", "or", "not", "in", "when", "with", "switch", "on", "equals", "into", "by");

    private static readonly WordSet AttributeTargets = new(false,
        "assembly", "module", "field", "event", "method", "param", "property", "return", "type", "typevar");

    private readonly string _s;
    private readonly int _end;
    private readonly int _lineStart;
    private readonly SyntaxWriter _w;
    private readonly List<Frame> _stack = [];

    private char _lastSig;      // the last significant character: punctuation, 'a' after a word, '0' after a number
    private bool _sawSig;       // whether anything significant came on this line yet
    private Prev _prev;         // what the last word was, when it steers the next one
    private bool _declHeader;   // in a type declaration's header or a where clause: ':' and ',' lead to types
    private int _typeArgsEnd = -1;
    private int _parens;
    private int _brackets;
    private int _attrBrackets = -1;
    private int _attrParens;
    private bool _attrName;

    public CSharpLexer(string s, int pos, int end, SyntaxWriter w, State? state)
    {
        _s = s;
        Pos = pos;
        _lineStart = pos;
        _end = end;
        _w = w;
        for (var f = state; f is not null; f = f.Outer)
            _stack.Insert(0, new Frame
            {
                Mode = f.Mode, Quotes = f.Quotes, Dollars = f.Dollars, Interpolated = f.Interpolated,
                Depth = f.Depth, Parens = f.Parens,
            });
    }

    public int Pos;

    /// <summary>In plain code: no comment, string or interpolation hole is open.</summary>
    public bool InCode => _stack.Count == 0;

    /// <summary>Where a statement can begin: nothing significant yet on this line, or after <c>{ } ; :</c>.</summary>
    public bool AtStatementStart => !_sawSig || _lastSig is '{' or '}' or ';' or ':';

    /// <summary>Writes the character at <see cref="Pos"/> as <paramref name="kind"/> and moves past it, as punctuation.</summary>
    public void Punctuation(SyntaxKind kind = SyntaxKind.Text)
    {
        var c = _s[Pos];
        if (c is '{' or ';') _declHeader = false;
        _w.Add(Pos, Pos + 1, kind);
        Pos++;
        Significant(c);
    }

    /// <summary>The state to carry into the next line.</summary>
    public State? EndLine()
    {
        // An unterminated regular string ends with its line; verbatim and raw strings, comments
        // and holes (C# 11 allows line breaks inside a hole) continue.
        while (_stack.Count > 0 && _stack[^1].Mode == Mode.String) _stack.RemoveAt(_stack.Count - 1);
        State? state = null;
        foreach (var f in _stack) state = new State(f.Mode, f.Quotes, f.Dollars, f.Interpolated, f.Depth, f.Parens, state);
        return state;
    }

    /// <summary>Scans one token (or the rest of an open comment or string) and advances past it.</summary>
    public void Next()
    {
        if (_stack.Count > 0)
        {
            switch (_stack[^1].Mode)
            {
                case Mode.BlockComment:
                    BlockComment();
                    return;
                case Mode.String or Mode.Verbatim or Mode.Raw:
                    StringBody();
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
            _stack.Add(new Frame { Mode = Mode.BlockComment });
            BlockComment();
            return;
        }
        if (c == '#' && _stack.Count == 0 && Scan.OnlySpacesBefore(s, _lineStart, i))
        {
            _w.Add(i, _end, SyntaxKind.Meta);
            Pos = _end;
            return;
        }
        if (c is '"' or '$' or '@' && TryString(i)) return;
        if (c == '\'' && TryChar(i)) return;
        if (Scan.IsNumberStart(s, i, _end))
        {
            Pos = Scan.Number(s, i, _end);
            _w.Add(i, Pos, SyntaxKind.Number);
            Significant('0');
            return;
        }
        if (Scan.IsIdentStart(c) || (c == '@' && Scan.IsIdentStart(next)))
        {
            Word(i);
            return;
        }

        var inHole = _stack.Count > 0 && _stack[^1].Mode == Mode.Hole;
        switch (c)
        {
            case '{':
                if (inHole) Top().Depth++;
                _declHeader = false;
                break;
            case '}' when inHole && Top().Depth == 0:
                CloseHole(i);
                return;
            case '}':
                if (inHole) Top().Depth--;
                break;
            case '(':
                _parens++;
                if (inHole) Top().Parens++;
                _attrName = false;
                break;
            case ')':
                if (_parens > 0) _parens--;
                if (inHole && Top().Parens > 0) Top().Parens--;
                break;
            case ':' when inHole && Top().Depth == 0 && Top().Parens == 0 && next != ':' && Scan.At(s, i - 1, _end) != ':':
                // The format part of a hole ({value:N2}): literal text up to the closing brace.
                var close = Scan.IndexOf(s, i + 1, _end, '}');
                Pos = close < 0 ? _end : close;
                _w.Add(i, Pos, SyntaxKind.String);
                return;
            case '[':
                _brackets++;
                if (_attrBrackets < 0 && (!_sawSig || _lastSig is '{' or '}' or ';' or ']' or '(' or ','))
                {
                    var k = Scan.Spaces(s, i + 1, _end);
                    if (k < _end && (char.IsUpper(s[k]) || AttributeTargets.Contains(s, k, Scan.Identifier(s, k, _end))))
                    {
                        _attrBrackets = _brackets;
                        _attrParens = _parens;
                        _attrName = true;
                    }
                }
                break;
            case ']':
                if (_brackets == _attrBrackets)
                {
                    _attrBrackets = -1;
                    _attrName = false;
                }
                if (_brackets > 0) _brackets--;
                break;
            case ',' when _brackets == _attrBrackets && _parens == _attrParens:
                _attrName = true;
                break;
            case ';':
                _declHeader = false;
                break;
        }
        Pos = i + (char.IsHighSurrogate(c) && i + 1 < _end ? 2 : 1);
        _w.Add(i, Pos, SyntaxKind.Text);
        Significant(c);
    }

    private void Word(int start)
    {
        var s = _s;
        var verbatim = s[start] == '@';
        var i = verbatim ? start + 1 : start;
        var end = Scan.Identifier(s, i, _end);
        var word = s.AsSpan(i, end - i);
        var member = _sawSig && _lastSig == '.';
        var plain = !verbatim && !member;

        SyntaxKind kind;
        var prev = Prev.None;
        if (_attrName && plain && AttributeTargets.Contains(word) && Scan.At(s, end, _end) == ':')
            kind = SyntaxKind.Keyword;
        else if (plain && Constants.Contains(word))
            kind = SyntaxKind.Constant;
        else if (plain && Keywords.Contains(word))
        {
            kind = SyntaxKind.Keyword;
            prev = word switch
            {
                "new" => Prev.New,
                "class" or "struct" or "interface" or "enum" => Prev.TypeDeclaration,
                "is" or "as" => Prev.IsAs,
                "typeof" or "sizeof" or "default" or "catch" => Prev.TypeOperator,
                _ => Prev.None,
            };
            if (prev == Prev.TypeDeclaration) _declHeader = true;
        }
        else if (plain && Contextual.Contains(word) && IsContextualKeyword(word, end))
        {
            kind = SyntaxKind.Keyword;
            if (word is "record")
            {
                prev = Prev.TypeDeclaration;
                _declHeader = true;
            }
            else if (word is "where")
            {
                prev = Prev.Where;
                _declHeader = true;
            }
        }
        else
        {
            kind = Classify(start, end, word);
            // `new System.Text.StringBuilder()`: every segment of a qualified name after new.
            if (_prev == Prev.New && Scan.At(s, end, _end) == '.') prev = Prev.New;
        }

        _w.Add(start, end, kind);
        Pos = end;
        _prev = prev;
        _lastSig = 'a';
        _sawSig = true;
    }

    private SyntaxKind Classify(int start, int end, ReadOnlySpan<char> word)
    {
        var s = _s;
        // In type arguments every name is a type, except a tuple element's name: List<(int Id, string Name)>.
        if (start < _typeArgsEnd) return _lastSig == 'a' ? SyntaxKind.Text : SyntaxKind.Type;
        if (_attrName) return SyntaxKind.Type;

        var declaresType = _prev is Prev.New or Prev.TypeDeclaration or Prev.IsAs
            || (_prev == Prev.Where && char.IsUpper(word[0]))
            || (_prev == Prev.TypeOperator && _lastSig == '(');
        var after = Scan.At(s, end, _end);
        if (after == '<' && char.IsUpper(word[0]) && GenericEnd(end) is var close and > 0)
        {
            _typeArgsEnd = close;
            return !declaresType && Scan.At(s, close + 1, _end) == '(' ? SyntaxKind.Function : SyntaxKind.Type;
        }
        if (declaresType) return SyntaxKind.Type;
        if (after == '(') return SyntaxKind.Function;
        if (_declHeader && _lastSig is ':' or ',') return SyntaxKind.Type;
        if (word.Length >= 3 && word[0] == 'I' && char.IsUpper(word[1]) && char.IsLower(word[2])) return SyntaxKind.Type;
        if (char.IsUpper(word[0]) && IsDeclarator(end)) return SyntaxKind.Type;
        return SyntaxKind.Text;
    }

    // `Order order`, `Order? order`, `Order[] orders`: a name followed by another name.
    private bool IsDeclarator(int end)
    {
        var s = _s;
        var k = end;
        if (Scan.At(s, k, _end) == '?') k++;
        while (Scan.At(s, k, _end) == '[')
        {
            var j = k + 1;
            while (Scan.At(s, j, _end) == ',') j++;
            if (Scan.At(s, j, _end) != ']') return false;
            k = j + 1;
        }
        var w = Scan.Spaces(s, k, _end);
        if (w == k || w >= _end || !Scan.IsIdentStart(s[w])) return false;
        return !NotDeclarators.Contains(s, w, Scan.Identifier(s, w, _end));
    }

    // The index of the '>' closing a plausible generic argument list opening at lt, or -1.
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
            else if (!(Scan.IsIdentPart(c) || c is ' ' or ',' or '.' or '?' or '[' or ']' or '(' or ')' or '*' or ':'))
                return -1;
        }
        return -1;
    }

    private bool IsContextualKeyword(ReadOnlySpan<char> word, int end)
    {
        var s = _s;
        var k = Scan.Spaces(s, end, _end);
        var nc = Scan.At(s, k, _end);
        var nextWord = Scan.IsIdentStart(nc) ? s.AsSpan(k, Scan.Identifier(s, k, _end) - k) : default;
        switch (word)
        {
            case "file":
                return !nextWord.IsEmpty && TypeDeclarationModifiers.Contains(nextWord);
            case "record":
                return !nextWord.IsEmpty && (char.IsUpper(nextWord[0]) || nextWord is "class" or "struct");
            case "get" or "set" or "init" or "add" or "remove":
                return nc is ';' or '{' or '\0' || (nc == '=' && Scan.At(s, k + 1, _end) == '>');
            case "global":
                return Scan.At(s, k, _end, "::") || nextWord is "using";
            case "notnull" or "unmanaged" or "managed" or "nint" or "nuint" or "dynamic":
                return true;
        }
        if (nc is '.' or ',' or ')' or ']' or ';' or ':') return false;
        return !(nc == '=' && Scan.At(s, k + 1, _end) is not ('=' or '>'));
    }

    private bool TryString(int i)
    {
        var s = _s;
        var k = i;
        var verbatim = false;
        var dollars = 0;
        if (s[k] == '@')
        {
            verbatim = true;
            k++;
        }
        while (k < _end && s[k] == '$')
        {
            dollars++;
            k++;
        }
        if (!verbatim && k < _end && s[k] == '@')
        {
            verbatim = true;
            k++;
        }
        if (k >= _end || s[k] != '"') return false;

        var quotes = 0;
        while (k + quotes < _end && s[k + quotes] == '"') quotes++;
        Significant('"');
        if (!verbatim && quotes >= 3)
        {
            Pos = k + quotes;
            _w.Add(i, Pos, SyntaxKind.String);
            _stack.Add(new Frame { Mode = Mode.Raw, Quotes = quotes, Dollars = dollars, Interpolated = dollars > 0 });
            return true;
        }
        if (!verbatim && quotes == 2)
        {
            Pos = k + 2;
            _w.Add(i, Pos, SyntaxKind.String);
            return true;
        }
        Pos = k + 1;
        _w.Add(i, Pos, SyntaxKind.String);
        _stack.Add(new Frame { Mode = verbatim ? Mode.Verbatim : Mode.String, Dollars = dollars, Interpolated = dollars > 0 });
        return true;
    }

    private bool TryChar(int i)
    {
        var s = _s;
        var k = i + 1;
        if (k >= _end) return false;
        var escaped = s[k] == '\\';
        k = escaped ? Scan.EscapeEnd(s, k, _end) : k + (char.IsHighSurrogate(s[k]) ? 2 : 1);
        if (Scan.At(s, k, _end) != '\'') return false;
        _w.Add(i, i + 1, SyntaxKind.String);
        if (escaped) _w.Add(i + 1, k, SyntaxKind.Escape);
        _w.Add(i + 1, k + 1, SyntaxKind.String);
        Pos = k + 1;
        Significant('\'');
        return true;
    }

    private void StringBody()
    {
        var s = _s;
        ref var f = ref Top();
        var i = Pos;
        var run = i;
        while (i < _end)
        {
            var c = s[i];
            if (f.Mode == Mode.Raw)
            {
                if (c == '"')
                {
                    var q = 0;
                    while (i + q < _end && s[i + q] == '"') q++;
                    if (q >= f.Quotes)
                    {
                        CloseString(run, i + q);
                        return;
                    }
                    i += q;
                    continue;
                }
                if (f.Dollars > 0 && c == '{')
                {
                    var b = 0;
                    while (i + b < _end && s[i + b] == '{') b++;
                    if (b >= f.Dollars)
                    {
                        var open = i + b - f.Dollars;
                        _w.Add(run, open, SyntaxKind.String);
                        OpenHole(open, i + b);
                        return;
                    }
                    i += b;
                    continue;
                }
                i++;
                continue;
            }

            if (c == '\\' && f.Mode == Mode.String)
            {
                _w.Add(run, i, SyntaxKind.String);
                var e = Scan.EscapeEnd(s, i, _end);
                _w.Add(i, e, SyntaxKind.Escape);
                i = run = e;
                continue;
            }
            if (c == '"')
            {
                if (f.Mode == Mode.Verbatim && Scan.At(s, i + 1, _end) == '"')
                {
                    _w.Add(run, i, SyntaxKind.String);
                    _w.Add(i, i + 2, SyntaxKind.Escape);
                    i = run = i + 2;
                    continue;
                }
                CloseString(run, i + 1);
                return;
            }
            if (f.Interpolated && c is '{' or '}')
            {
                if (Scan.At(s, i + 1, _end) == c)
                {
                    _w.Add(run, i, SyntaxKind.String);
                    _w.Add(i, i + 2, SyntaxKind.Escape);
                    i = run = i + 2;
                    continue;
                }
                if (c == '{')
                {
                    _w.Add(run, i, SyntaxKind.String);
                    OpenHole(i, i + 1);
                    return;
                }
            }
            i++;
        }
        _w.Add(run, _end, SyntaxKind.String);
        Pos = _end;
    }

    private void CloseString(int run, int end)
    {
        var s = _s;
        if (Scan.At(s, end, _end, "u8") || Scan.At(s, end, _end, "U8")) end += 2;
        _w.Add(run, end, SyntaxKind.String);
        _stack.RemoveAt(_stack.Count - 1);
        Pos = end;
        Significant('"');
    }

    private void OpenHole(int start, int end)
    {
        _w.Add(start, end, SyntaxKind.Escape);
        _stack.Add(new Frame { Mode = Mode.Hole });
        Pos = end;
        _lastSig = '{';
        _prev = Prev.None;
    }

    private void CloseHole(int i)
    {
        _stack.RemoveAt(_stack.Count - 1);
        // A raw string with $$ closes its hole with as many braces as it has dollars.
        var braces = _stack.Count > 0 && _stack[^1].Mode == Mode.Raw ? Math.Max(1, _stack[^1].Dollars) : 1;
        var end = i;
        while (end < _end && end - i < braces && _s[end] == '}') end++;
        _w.Add(i, end, SyntaxKind.Escape);
        Pos = end;
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

    private ref Frame Top() => ref CollectionsMarshal.AsSpan(_stack)[^1];

    private void Significant(char c)
    {
        // `catch (Exception e)`, `typeof(Order)`: the paren keeps what the keyword said.
        if (!(c == '(' && _prev == Prev.TypeOperator) && !(c == '.' && _prev == Prev.New)) _prev = Prev.None;
        _lastSig = c;
        _sawSig = true;
    }
}
