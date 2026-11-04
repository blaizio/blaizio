namespace Blaizio;

/// <summary>
/// Razor (Blazor components and Razor views): markup with C# wherever an <c>@</c> transitions into
/// it. Covers directives (<c>@page</c>, <c>@inject</c>, <c>@code</c>, ...), implicit
/// (<c>@item.Name</c>) and explicit (<c>@(...)</c>) expressions, code blocks and control structures
/// (<c>@if</c> with its <c>else</c>, <c>@foreach</c>, <c>@switch</c>, <c>@try</c>, ...), markup inside
/// code (a tag where a statement can start, <c>&lt;text&gt;</c>, <c>@:</c> lines, <c>@&lt;p&gt;</c>
/// templates), directive attributes whose value is C# (<c>@onclick</c>, <c>@bind-Value</c>), Razor
/// and HTML comments, and <c>@@</c>. Components (an upper-case or dotted tag name) read as types,
/// HTML elements as tags. An <c>@</c> after a letter or digit is text, as in an email address.
/// </summary>
internal sealed class RazorSyntax : SyntaxLanguage
{
    public static readonly RazorSyntax Instance = new();

    public override string Name => "razor";

    public override IReadOnlyList<string> Aliases => ["cshtml", "blazor"];

    internal enum Mode : byte
    {
        Island,
        TextLine,
        Code,
        Header,
        Expression,
        Implicit,
        RazorComment,
        HtmlComment,
        Tag,
        Value,
        Embedded,
        Continuation,
    }

    /// <summary>One open construct; the chain is innermost first.</summary>
    internal sealed record Frame(Mode Mode, int Depth, int Flags, char Quote, string? Tag, object? Inner, Frame? Outer);

    // Frame flags.
    private const int Control = 1;    // Header / Code: a control structure, which an else, catch or finally can continue
    private const int Template = 2;   // Island: an inline template, @<p>...</p>
    private const int CodeValue = 4;  // Value: a directive attribute's value, which is C#
    private const int DoWhile = 8;    // Header: the `while (...);` that ends a do block

    private static readonly WordSet ControlKeywords = new(false, "if", "for", "foreach", "while", "switch", "lock", "try", "do");

    private static readonly WordSet BlockDirectives = new(false, "code", "functions");

    private static readonly WordSet LineDirectives = new(false,
        "page", "inject", "inherits", "implements", "layout", "namespace", "attribute", "typeparam",
        "rendermode", "preservewhitespace", "model", "addTagHelper", "removeTagHelper", "tagHelperPrefix",
        "section", "using");

    private static readonly WordSet TypeDirectives = new(false, "inherits", "implements", "layout", "model");

    private static readonly WordSet Continuations = new(false, "else", "catch", "finally", "while");

    private static readonly WordSet VoidElements = new(true,
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr");

    public override object? Tokenize(string line, int start, int end, object? state, SyntaxWriter writer) =>
        new Scanner(line, start, end, writer, state as Frame).Run();

    private struct Open
    {
        public Mode Mode;
        public int Depth;
        public int Flags;
        public char Quote;
        public string? Tag;
        public object? Inner;
    }

    private sealed class Scanner
    {
        private readonly string _s;
        private readonly int _start;
        private readonly int _end;
        private readonly SyntaxWriter _w;
        private readonly List<Open> _stack = [];
        private int _i;

        public Scanner(string s, int start, int end, SyntaxWriter w, Frame? state)
        {
            _s = s;
            _start = start;
            _i = start;
            _end = end;
            _w = w;
            for (var f = state; f is not null; f = f.Outer)
                _stack.Insert(0, new Open { Mode = f.Mode, Depth = f.Depth, Flags = f.Flags, Quote = f.Quote, Tag = f.Tag, Inner = f.Inner });
        }

        private ref Open Top => ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_stack)[^1];

        public Frame? Run()
        {
            while (_i < _end)
            {
                if (_stack.Count == 0)
                {
                    Markup(island: false);
                    continue;
                }
                switch (Top.Mode)
                {
                    case Mode.Island:
                        Markup(island: true);
                        break;
                    case Mode.TextLine:
                        Markup(island: false);
                        break;
                    case Mode.Code:
                        Code();
                        break;
                    case Mode.Header:
                        Header();
                        break;
                    case Mode.Expression or Mode.Implicit:
                        Expression();
                        break;
                    case Mode.RazorComment:
                        RazorComment(_i);
                        break;
                    case Mode.HtmlComment:
                        HtmlComment(_i);
                        break;
                    case Mode.Tag:
                        Tag();
                        break;
                    case Mode.Value:
                        Value();
                        break;
                    case Mode.Embedded:
                        Embedded();
                        break;
                    case Mode.Continuation:
                        Continuation();
                        break;
                }
            }

            // A @: line ends with its line.
            while (_stack.Count > 0 && Top.Mode == Mode.TextLine) _stack.RemoveAt(_stack.Count - 1);
            Frame? state = null;
            foreach (var f in _stack) state = new Frame(f.Mode, f.Depth, f.Flags, f.Quote, f.Tag, f.Inner, state);
            return state;
        }

        private void Push(Mode mode, int depth = 0, int flags = 0, char quote = '\0', string? tag = null) =>
            _stack.Add(new Open { Mode = mode, Depth = depth, Flags = flags, Quote = quote, Tag = tag });

        private void Pop() => _stack.RemoveAt(_stack.Count - 1);

        private void Add(int start, int end, SyntaxKind kind) => _w.Add(start, end, kind);

        // ---- markup (the document, an island of markup inside code, or a @: line)

        private void Markup(bool island)
        {
            var s = _s;
            var depth = _stack.Count;
            while (_i < _end && _stack.Count == depth)
            {
                var c = s[_i];
                if (c == '@')
                {
                    Transition();
                    continue;
                }
                if (c == '<')
                {
                    MarkupTag(island);
                    continue;
                }
                if (c == '&')
                {
                    var e = MarkupSyntax.EntityEnd(s, _i, _end);
                    if (e > 0)
                    {
                        Add(_i, e, SyntaxKind.Escape);
                        _i = e;
                        continue;
                    }
                }
                var k = s.AsSpan(_i + 1, _end - _i - 1).IndexOfAny('@', '<', '&');
                var stop = k < 0 ? _end : _i + 1 + k;
                Add(_i, stop, SyntaxKind.Text);
                _i = stop;
            }
        }

        // At '@' in markup or in an attribute value.
        private void Transition()
        {
            var s = _s;
            var i = _i;
            var next = Scan.At(s, i + 1, _end);
            if (next == '@')
            {
                Add(i, i + 2, SyntaxKind.Escape);
                _i = i + 2;
                return;
            }
            if (next == '*')
            {
                RazorComment(i);
                return;
            }
            // name@example.com: an @ after a letter or digit is text.
            if (i > _start && char.IsLetterOrDigit(s[i - 1]))
            {
                Add(i, i + 1, SyntaxKind.Text);
                _i = i + 1;
                return;
            }
            if (next == '(')
            {
                Add(i, i + 2, SyntaxKind.Keyword);
                _i = i + 2;
                Push(Mode.Expression, depth: 1);
                return;
            }
            if (next == '{')
            {
                Add(i, i + 2, SyntaxKind.Keyword);
                _i = i + 2;
                Push(Mode.Code, depth: 1);
                return;
            }
            if (!Scan.IsIdentStart(next))
            {
                Add(i, i + 1, SyntaxKind.Text);
                _i = i + 1;
                return;
            }

            var wordEnd = Scan.Identifier(s, i + 1, _end);
            var word = s.AsSpan(i + 1, wordEnd - i - 1);
            var after = Scan.At(s, Scan.Spaces(s, wordEnd, _end), _end);
            if (BlockDirectives.Contains(word))
            {
                Add(i, wordEnd, SyntaxKind.Keyword);
                _i = wordEnd;
                Push(Mode.Header);
                return;
            }
            if (ControlKeywords.Contains(word) || (word is "using" && after == '('))
            {
                Add(i, wordEnd, SyntaxKind.Keyword);
                _i = wordEnd;
                Push(Mode.Header, flags: Control);
                return;
            }
            if (LineDirectives.Contains(word) && Scan.OnlySpacesBefore(s, _start, i))
            {
                Add(i, wordEnd, SyntaxKind.Keyword);
                _i = wordEnd;
                Directive(word);
                return;
            }
            Add(i, i + 1, SyntaxKind.Keyword);
            _i = i + 1;
            if (word is "await")
            {
                Add(_i, wordEnd, SyntaxKind.Keyword);
                _i = Scan.Spaces(s, wordEnd, _end);
                Add(wordEnd, _i, SyntaxKind.Text);
                if (_i >= _end || !Scan.IsIdentStart(s[_i])) return;
            }
            ImplicitExpression();
        }

        // @item.Name, @Format(value), @items[0]?.Title: names joined by . and ?., with balanced () and [].
        private void ImplicitExpression()
        {
            var s = _s;
            var k = _i;
            while (k < _end && Scan.IsIdentStart(s[k]))
            {
                k = Scan.Identifier(s, k, _end);
                while (k < _end && s[k] is '(' or '[')
                {
                    var close = BalancedEnd(k);
                    if (close < 0)
                    {
                        // The call runs onto the next line: C# until its brackets balance.
                        var open = OpenBrackets(k);
                        CSharp(_i, _end);
                        _i = _end;
                        Push(Mode.Implicit, depth: open);
                        return;
                    }
                    k = close + 1;
                }
                if (Scan.At(s, k, _end) == '.' && Scan.IsIdentStart(Scan.At(s, k + 1, _end))) k++;
                else if (Scan.At(s, k, _end, "?.") && Scan.IsIdentStart(Scan.At(s, k + 2, _end))) k += 2;
                else break;
            }
            CSharp(_i, k);
            _i = k;
        }

        // The index of the bracket closing the one at open, skipping strings; -1 if not on this line.
        private int BalancedEnd(int open)
        {
            var s = _s;
            var depth = 0;
            for (var k = open; k < _end; k++)
            {
                switch (s[k])
                {
                    case '(' or '[':
                        depth++;
                        break;
                    case ')' or ']':
                        if (--depth == 0) return k;
                        break;
                    case '"' or '\'':
                        k = JsonSyntax.QuotedEnd(s, k, _end) - 1;
                        break;
                }
            }
            return -1;
        }

        private int OpenBrackets(int from)
        {
            var depth = 0;
            for (var k = from; k < _end; k++)
            {
                if (_s[k] is '(' or '[') depth++;
                else if (_s[k] is ')' or ']') depth--;
            }
            return Math.Max(1, depth);
        }

        // C# for s[from..to] from a fresh state.
        private void CSharp(int from, int to)
        {
            var lexer = new CSharpLexer(_s, from, to, _w, null);
            while (lexer.Pos < to) lexer.Next();
        }

        // The rest of a directive's line: @inherits / @implements / @layout name types; the others are C#.
        private void Directive(ReadOnlySpan<char> word)
        {
            var s = _s;
            if (TypeDirectives.Contains(word) || word is "typeparam")
            {
                var k = Scan.Spaces(s, _i, _end);
                Add(_i, k, SyntaxKind.Text);
                var e = Scan.Word(s, k, _end, ".");
                Add(k, e, SyntaxKind.Type);
                _i = e;
                if (word is not "typeparam")
                {
                    // Generic arguments and further interfaces: every name is a type.
                    while (_i < _end)
                    {
                        if (Scan.IsIdentStart(s[_i]))
                        {
                            var we = Scan.Word(s, _i, _end, ".");
                            Add(_i, we, SyntaxKind.Type);
                            _i = we;
                        }
                        else
                        {
                            Add(_i, _i + 1, SyntaxKind.Text);
                            _i++;
                        }
                    }
                    return;
                }
            }
            CSharp(_i, _end);
            _i = _end;
        }

        // At '<' in markup.
        private void MarkupTag(bool island)
        {
            var s = _s;
            var i = _i;
            if (Scan.At(s, i, _end, "<!--"))
            {
                Add(i, i + 4, SyntaxKind.Comment);
                _i = i + 4;
                Push(Mode.HtmlComment);
                return;
            }
            if (Scan.At(s, i + 1, _end) == '!')
            {
                var close = Scan.IndexOf(s, i, _end, '>');
                _i = close < 0 ? _end : close + 1;
                Add(i, _i, SyntaxKind.Meta);
                return;
            }
            var closing = Scan.At(s, i + 1, _end) == '/';
            var nameStart = closing ? i + 2 : i + 1;
            if (nameStart >= _end || !MarkupSyntax.IsNameStart(s[nameStart]))
            {
                Add(i, i + 1, SyntaxKind.Text);
                _i = i + 1;
                return;
            }
            var nameEnd = MarkupSyntax.NameEnd(s, nameStart, _end);
            var name = s[nameStart..nameEnd];
            Add(i, nameStart, SyntaxKind.Text);
            Add(nameStart, nameEnd, TagKind(name, island));
            _i = nameEnd;
            if (!closing)
            {
                Push(Mode.Tag, tag: name);
                return;
            }
            var end = Scan.IndexOf(s, _i, _end, '>');
            var e = end < 0 ? _end : end + 1;
            Add(_i, e, SyntaxKind.Text);
            _i = e;
            if (island && --Top.Depth <= 0) Pop();
        }

        private SyntaxKind TagKind(string name, bool island)
        {
            if (name == "text" && (island || _stack.Count > 0)) return SyntaxKind.Keyword;
            return char.IsUpper(name[0]) || name.Contains('.') ? SyntaxKind.Type : SyntaxKind.Tag;
        }

        // Inside a start tag: attributes until > or />.
        private void Tag()
        {
            var s = _s;
            while (_i < _end)
            {
                var c = s[_i];
                if (Scan.IsSpace(c))
                {
                    var e = Scan.Spaces(s, _i, _end);
                    Add(_i, e, SyntaxKind.Text);
                    _i = e;
                    continue;
                }
                if (c == '/' && Scan.At(s, _i + 1, _end) == '>')
                {
                    Add(_i, _i + 2, SyntaxKind.Text);
                    _i += 2;
                    EndStartTag(selfClosing: true);
                    return;
                }
                if (c == '>')
                {
                    Add(_i, _i + 1, SyntaxKind.Text);
                    _i++;
                    EndStartTag(selfClosing: false);
                    return;
                }
                if (c == '@' && Scan.At(s, _i + 1, _end) == '*')
                {
                    RazorComment(_i);
                    return;
                }

                var nameStart = _i;
                var nameEnd = _i;
                while (nameEnd < _end && !Scan.IsSpace(s[nameEnd]) && s[nameEnd] is not ('=' or '>' or '"' or '\'')
                    && !(s[nameEnd] == '/' && Scan.At(s, nameEnd + 1, _end) == '>'))
                    nameEnd++;
                if (nameEnd == nameStart) nameEnd++;
                var directive = s[nameStart] == '@';
                Add(nameStart, nameEnd, directive ? SyntaxKind.Keyword : SyntaxKind.Attribute);
                _i = nameEnd;

                var eq = Scan.Spaces(s, _i, _end);
                if (Scan.At(s, eq, _end) != '=') continue;
                Add(_i, eq + 1, SyntaxKind.Text);
                var v = Scan.Spaces(s, eq + 1, _end);
                Add(eq + 1, v, SyntaxKind.Text);
                _i = v;
                if (v >= _end) return;
                if (s[v] is '"' or '\'')
                {
                    Add(v, v + 1, SyntaxKind.String);
                    _i = v + 1;
                    Push(Mode.Value, flags: directive ? CodeValue : 0, quote: s[v]);
                    return;
                }
                // An unquoted value: an expression, or a bare word.
                if (s[v] == '@')
                {
                    Transition();
                    if (_stack.Count == 0 || Top.Mode != Mode.Tag) return;
                    continue;
                }
                var u = v;
                while (u < _end && !Scan.IsSpace(s[u]) && s[u] != '>' && !(s[u] == '/' && Scan.At(s, u + 1, _end) == '>')) u++;
                if (directive) CSharp(v, u);
                else Add(v, u, SyntaxKind.String);
                _i = u;
            }
        }

        private void EndStartTag(bool selfClosing)
        {
            var name = Top.Tag!;
            Pop();
            if (!selfClosing && (name.Equals("script", StringComparison.OrdinalIgnoreCase) || name.Equals("style", StringComparison.OrdinalIgnoreCase)))
            {
                Push(Mode.Embedded, tag: name);
                return;
            }
            if (_stack.Count == 0 || Top.Mode != Mode.Island) return;
            if (!selfClosing && !VoidElements.Contains(name)) Top.Depth++;
            else if (Top.Depth == 0) Pop();
        }

        // A quoted attribute value: text with @ transitions, or C# for a directive attribute.
        private void Value()
        {
            var s = _s;
            var quote = Top.Quote;
            if ((Top.Flags & CodeValue) != 0)
            {
                var lexer = new CSharpLexer(s, _i, _end, _w, Top.Inner as CSharpLexer.State);
                while (lexer.Pos < _end)
                {
                    if (lexer.InCode && s[lexer.Pos] == quote)
                    {
                        Add(lexer.Pos, lexer.Pos + 1, SyntaxKind.String);
                        _i = lexer.Pos + 1;
                        Pop();
                        return;
                    }
                    lexer.Next();
                }
                _i = _end;
                Top.Inner = lexer.EndLine();
                return;
            }

            var depth = _stack.Count;
            while (_i < _end && _stack.Count == depth)
            {
                var c = s[_i];
                if (c == quote)
                {
                    Add(_i, _i + 1, SyntaxKind.String);
                    _i++;
                    Pop();
                    return;
                }
                if (c == '@')
                {
                    Transition();
                    continue;
                }
                var k = s.AsSpan(_i + 1, _end - _i - 1).IndexOfAny(quote, '@');
                var stop = k < 0 ? _end : _i + 1 + k;
                Add(_i, stop, SyntaxKind.String);
                _i = stop;
            }
        }

        // ---- code

        // A code block (@{ }, @code { }, a control structure's body): C# up to its closing brace,
        // with markup wherever a statement can start with a tag.
        private void Code()
        {
            var s = _s;
            var lexer = new CSharpLexer(s, _i, _end, _w, Top.Inner as CSharpLexer.State);
            while (lexer.Pos < _end)
            {
                if (lexer.InCode)
                {
                    var p = lexer.Pos;
                    var c = s[p];
                    var n = Scan.At(s, p + 1, _end);
                    if (c == '{')
                    {
                        Top.Depth++;
                        lexer.Punctuation();
                        continue;
                    }
                    if (c == '}')
                    {
                        if (Top.Depth <= 1)
                        {
                            Add(p, p + 1, SyntaxKind.Keyword);
                            _i = p + 1;
                            var control = (Top.Flags & Control) != 0;
                            Pop();
                            if (control) Push(Mode.Continuation);
                            return;
                        }
                        Top.Depth--;
                        lexer.Punctuation();
                        continue;
                    }
                    if (c == '@' && n is '<' or ':' or '*')
                    {
                        _i = p;
                        Top.Inner = null;
                        CodeTransition(p, n);
                        return;
                    }
                    if (c == '@' && Scan.IsIdentStart(n) && ControlKeywords.Contains(s, p + 1, Scan.Identifier(s, p + 1, _end)))
                    {
                        // @foreach inside a code block: Razor allows the transition there too.
                        var e = Scan.Identifier(s, p + 1, _end);
                        Add(p, e, SyntaxKind.Keyword);
                        _i = e;
                        Top.Inner = null;
                        Push(Mode.Header, flags: Control);
                        return;
                    }
                    if (c == '<' && lexer.AtStatementStart && MarkupSyntax.IsNameStart(n))
                    {
                        _i = p;
                        Top.Inner = null;
                        Push(Mode.Island);
                        return;
                    }
                }
                lexer.Next();
            }
            _i = _end;
            Top.Inner = lexer.EndLine();
        }

        // @<p>...</p>, @: text, @* comment *@ inside code.
        private void CodeTransition(int p, char next)
        {
            switch (next)
            {
                case '<':
                    Add(p, p + 1, SyntaxKind.Keyword);
                    _i = p + 1;
                    Push(Mode.Island, flags: Template);
                    break;
                case ':':
                    Add(p, p + 2, SyntaxKind.Keyword);
                    _i = p + 2;
                    Push(Mode.TextLine);
                    break;
                default:
                    RazorComment(p);
                    break;
            }
        }

        // Between @if / @foreach / @code / ... and the brace that opens its block (perhaps on a
        // later line): C#, counting parentheses so a brace inside the condition does not count.
        private void Header()
        {
            var s = _s;
            var lexer = new CSharpLexer(s, _i, _end, _w, Top.Inner as CSharpLexer.State);
            while (lexer.Pos < _end)
            {
                if (lexer.InCode)
                {
                    var p = lexer.Pos;
                    var c = s[p];
                    if (c == '(') Top.Depth++;
                    else if (c == ')' && Top.Depth > 0) Top.Depth--;
                    else if (c == '{' && Top.Depth == 0)
                    {
                        Add(p, p + 1, SyntaxKind.Keyword);
                        _i = p + 1;
                        var flags = Top.Flags & Control;
                        Pop();
                        Push(Mode.Code, depth: 1, flags: flags);
                        return;
                    }
                    else if (c == ';' && Top.Depth == 0 && (Top.Flags & DoWhile) != 0)
                    {
                        lexer.Punctuation();
                        _i = lexer.Pos;
                        Pop();
                        return;
                    }
                }
                lexer.Next();
            }
            _i = _end;
            Top.Inner = lexer.EndLine();
        }

        // @( ... ), or an implicit expression's call running across lines: C# until the brackets balance.
        private void Expression()
        {
            var s = _s;
            var explicitExpression = Top.Mode == Mode.Expression;
            var lexer = new CSharpLexer(s, _i, _end, _w, Top.Inner as CSharpLexer.State);
            while (lexer.Pos < _end)
            {
                if (lexer.InCode)
                {
                    var p = lexer.Pos;
                    var c = s[p];
                    if (c is '(' or '[') Top.Depth++;
                    else if (c is ')' or ']' && --Top.Depth == 0)
                    {
                        Add(p, p + 1, explicitExpression ? SyntaxKind.Keyword : SyntaxKind.Text);
                        _i = p + 1;
                        Pop();
                        return;
                    }
                    else if (c == '@' && Scan.At(s, p + 1, _end) == '<')
                    {
                        _i = p;
                        Top.Inner = null;
                        CodeTransition(p, '<');
                        return;
                    }
                }
                lexer.Next();
            }
            _i = _end;
            Top.Inner = lexer.EndLine();
        }

        // After a control block's closing brace: an else, catch or finally continues it.
        private void Continuation()
        {
            var s = _s;
            var k = Scan.Spaces(s, _i, _end);
            Add(_i, k, SyntaxKind.Text);
            _i = k;
            if (k >= _end) return;
            var e = Scan.Identifier(s, k, _end);
            Pop();
            if (!Continuations.Contains(s, k, e)) return;
            Add(k, e, SyntaxKind.Keyword);
            _i = e;
            Push(Mode.Header, flags: s.AsSpan(k, e - k) is "while" ? DoWhile : Control);
        }

        // ---- comments and embedded languages

        private void RazorComment(int i)
        {
            var from = Top_IsRazorComment() ? i : i + 2;
            var close = Scan.IndexOf(_s, from, _end, "*@");
            if (!Top_IsRazorComment()) Push(Mode.RazorComment);
            if (close < 0)
            {
                Add(i, _end, SyntaxKind.Comment);
                _i = _end;
                return;
            }
            Add(i, close + 2, SyntaxKind.Comment);
            _i = close + 2;
            Pop();
        }

        private bool Top_IsRazorComment() => _stack.Count > 0 && Top.Mode == Mode.RazorComment;

        private void HtmlComment(int i)
        {
            var close = Scan.IndexOf(_s, i, _end, "-->");
            if (close < 0)
            {
                Add(i, _end, SyntaxKind.Comment);
                _i = _end;
                return;
            }
            Add(i, close + 3, SyntaxKind.Comment);
            _i = close + 3;
            Pop();
        }

        private void Embedded()
        {
            var tag = Top.Tag!;
            var close = MarkupSyntax.IndexOfClosingTag(_s, _i, _end, tag);
            var stop = close < 0 ? _end : close;
            // By name, so the body gets the language the app replaced css or javascript with.
            var language = tag.Equals("style", StringComparison.OrdinalIgnoreCase)
                ? _w.Find("css") ?? CssSyntax.Instance
                : _w.Find("javascript") ?? ScriptSyntax.JavaScript;
            var inner = _w.Embed(language, _i, stop, Top.Inner);
            _i = stop;
            if (close < 0)
            {
                Top.Inner = inner;
                return;
            }
            Pop();
        }
    }
}
