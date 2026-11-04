namespace Blaizio;

/// <summary>
/// SQL across the common dialects (T-SQL, PostgreSQL, SQLite, MySQL): keywords and data types in
/// any case, a name before <c>(</c> as a function, <c>'strings'</c> (and <c>N'...'</c>, dollar-quoted
/// bodies), <c>@variables</c> and <c>$1</c> / <c>:name</c> parameters, <c>--</c> and <c>/* */</c> comments.
/// </summary>
internal sealed class SqlSyntax : SyntaxLanguage
{
    public static readonly SqlSyntax Instance = new();

    internal enum Mode : byte
    {
        BlockComment,
        Quoted,
        Dollar,
    }

    /// <summary>An open comment, an open quoted string (<paramref name="Tag"/> is the quote), or a <c>$tag$</c> body.</summary>
    internal sealed record State(Mode Mode, string Tag);

    private static readonly WordSet Keywords = new(true,
        "add", "all", "alter", "and", "any", "as", "asc", "authorization", "backup", "begin", "between",
        "break", "by", "cascade", "case", "check", "checkpoint", "close", "clustered", "collate", "column",
        "commit", "constraint", "contains", "continue", "create", "cross", "current", "cursor", "database",
        "deallocate", "declare", "default", "delete", "deny", "desc", "distinct", "do", "drop", "else",
        "end", "escape", "except", "exec", "execute", "exists", "explain", "fetch", "first", "for",
        "foreign", "from", "full", "function", "go", "goto", "grant", "group", "having", "identity", "if",
        "ilike", "in", "index", "inner", "insert", "intersect", "into", "is", "join", "key", "language",
        "last", "left", "like", "limit", "lock", "loop", "matched", "merge", "natural", "next", "nocount",
        "nonclustered", "not", "nulls", "of", "off", "offset", "on", "only", "open", "option", "or",
        "order", "outer", "output", "over", "partition", "percent", "pivot", "plan", "pragma", "primary",
        "print", "procedure", "proc", "raiserror", "recursive", "references", "rename", "replace",
        "restrict", "return", "returning", "returns", "revoke", "right", "rollback", "row", "rows",
        "savepoint", "schema", "select", "set", "show", "table", "temp", "temporary", "then", "throw",
        "to", "top", "tran", "transaction", "trigger", "truncate", "try", "catch", "union", "unique",
        "unpivot", "update", "upsert", "use", "using", "values", "vacuum", "view", "when", "where",
        "while", "window", "with", "without", "conflict", "nothing", "lateral", "fetch", "next", "sequence",
        "extension", "owner", "materialized", "concurrently", "elseif", "loop", "raise", "notice", "perform",
        "declare", "exception", "strict", "volatile", "stable", "immutable", "security", "definer", "invoker");

    private static readonly WordSet Types = new(true,
        "bigint", "binary", "bit", "blob", "bool", "boolean", "bytea", "char", "character", "cidr", "clob",
        "date", "datetime", "datetime2", "datetimeoffset", "decimal", "double", "enum", "float", "float4",
        "float8", "geography", "geometry", "hierarchyid", "image", "inet", "int", "int2", "int4", "int8",
        "integer", "interval", "json", "jsonb", "longtext", "macaddr", "mediumint", "mediumtext", "money",
        "nchar", "ntext", "numeric", "nvarchar", "precision", "real", "rowversion", "serial", "bigserial",
        "smalldatetime", "smallint", "smallmoney", "smallserial", "sql_variant", "text", "time", "timestamp",
        "timestamptz", "timetz", "tinyint", "tinytext", "uniqueidentifier", "uuid", "varbinary", "varchar",
        "varying", "xml", "year", "zone", "array");

    private static readonly WordSet Constants = new(true, "null", "true", "false", "unknown");

    public override string Name => "sql";

    public override IReadOnlyList<string> Aliases =>
        ["tsql", "t-sql", "mssql", "postgres", "postgresql", "pgsql", "plpgsql", "psql", "mysql", "mariadb", "sqlite", "plsql"];

    public override object? Tokenize(string s, int start, int end, object? state, SyntaxWriter w)
    {
        var i = start;
        if (state is State st)
        {
            int close;
            switch (st.Mode)
            {
                case Mode.BlockComment:
                    close = Scan.IndexOf(s, i, end, "*/");
                    if (close < 0)
                    {
                        w.Add(i, end, SyntaxKind.Comment);
                        return st;
                    }
                    w.Add(i, close + 2, SyntaxKind.Comment);
                    i = close + 2;
                    break;
                case Mode.Dollar:
                    close = Scan.IndexOf(s, i, end, st.Tag);
                    if (close < 0)
                    {
                        w.Add(i, end, SyntaxKind.String);
                        return st;
                    }
                    w.Add(i, close + st.Tag.Length, SyntaxKind.String);
                    i = close + st.Tag.Length;
                    break;
                default:
                    close = QuoteEnd(s, i, end, st.Tag[0]);
                    if (close < 0)
                    {
                        w.Add(i, end, SyntaxKind.String);
                        return st;
                    }
                    w.Add(i, close, SyntaxKind.String);
                    i = close;
                    break;
            }
        }

        while (i < end)
        {
            var c = s[i];
            var next = Scan.At(s, i + 1, end);
            if (Scan.IsSpace(c))
            {
                var e = Scan.Spaces(s, i, end);
                w.Add(i, e, SyntaxKind.Text);
                i = e;
                continue;
            }
            if (c == '-' && next == '-' || c == '#' && i == start)
            {
                w.Add(i, end, SyntaxKind.Comment);
                return null;
            }
            if (c == '/' && next == '*')
            {
                var close = Scan.IndexOf(s, i + 2, end, "*/");
                if (close < 0)
                {
                    w.Add(i, end, SyntaxKind.Comment);
                    return new State(Mode.BlockComment, "*/");
                }
                w.Add(i, close + 2, SyntaxKind.Comment);
                i = close + 2;
                continue;
            }
            if (c is '\'' or '"' || (c is 'N' or 'n' or 'E' or 'e' or 'B' or 'b' or 'X' or 'x') && next == '\'' && (i == start || !Scan.IsIdentPart(s[i - 1])))
            {
                var quoteAt = c is '\'' or '"' ? i : i + 1;
                var close = QuoteEnd(s, quoteAt + 1, end, s[quoteAt]);
                if (close < 0)
                {
                    w.Add(i, end, SyntaxKind.String);
                    return new State(Mode.Quoted, s[quoteAt].ToString());
                }
                w.Add(i, close, SyntaxKind.String);
                i = close;
                continue;
            }
            if (c == '$' && (next == '$' || Scan.IsIdentStart(next)) && DollarTag(s, i, end) is { } tag)
            {
                var close = Scan.IndexOf(s, i + tag.Length, end, tag);
                if (close < 0)
                {
                    w.Add(i, end, SyntaxKind.String);
                    return new State(Mode.Dollar, tag);
                }
                w.Add(i, close + tag.Length, SyntaxKind.String);
                i = close + tag.Length;
                continue;
            }
            if (c is '@' or ':' && (Scan.IsIdentStart(next) || (c == '@' && next == '@')) && !(c == ':' && Scan.At(s, i - 1, end) == ':'))
            {
                var e = Scan.Word(s, i + 1, end, "@$#");
                w.Add(i, e, SyntaxKind.Variable);
                i = e;
                continue;
            }
            if (c == '$' && char.IsAsciiDigit(next))
            {
                var e = i + 1;
                while (e < end && char.IsAsciiDigit(s[e])) e++;
                w.Add(i, e, SyntaxKind.Variable);
                i = e;
                continue;
            }
            if (c is '[' or '`')
            {
                // [Quoted Name], `quoted name`: identifiers.
                var close = Scan.IndexOf(s, i + 1, end, c == '[' ? ']' : '`');
                var e = close < 0 ? i + 1 : close + 1;
                w.Add(i, e, SyntaxKind.Text);
                i = e;
                continue;
            }
            if (Scan.IsNumberStart(s, i, end) && (i == start || !Scan.IsIdentPart(s[i - 1])))
            {
                var e = Scan.Number(s, i, end);
                w.Add(i, e, SyntaxKind.Number);
                i = e;
                continue;
            }
            if (Scan.IsIdentStart(c))
            {
                var e = Scan.Word(s, i, end, "$#");
                var member = i > start && s[i - 1] == '.';
                SyntaxKind kind;
                if (Scan.At(s, e, end) == '(' && !Keywords.Contains(s, i, e) && !Types.Contains(s, i, e))
                    kind = SyntaxKind.Function;
                else if (member)
                    kind = SyntaxKind.Text;
                else if (Constants.Contains(s, i, e))
                    kind = SyntaxKind.Constant;
                else if (Keywords.Contains(s, i, e))
                    kind = SyntaxKind.Keyword;
                else if (Types.Contains(s, i, e))
                    kind = SyntaxKind.Type;
                else
                    kind = SyntaxKind.Text;
                w.Add(i, e, kind);
                i = e;
                continue;
            }
            w.Add(i, i + 1, SyntaxKind.Text);
            i++;
        }
        return null;
    }

    // Past the closing quote ('' and "" escape a quote), or -1 when the string continues.
    private static int QuoteEnd(string s, int i, int end, char quote)
    {
        for (var k = i; k < end; k++)
        {
            if (s[k] != quote) continue;
            if (Scan.At(s, k + 1, end) == quote)
            {
                k++;
                continue;
            }
            return k + 1;
        }
        return -1;
    }

    // $$ or $tag$ opening a dollar-quoted body.
    private static string? DollarTag(string s, int i, int end)
    {
        var k = i + 1;
        while (k < end && Scan.IsIdentPart(s[k])) k++;
        return Scan.At(s, k, end) == '$' ? s[i..(k + 1)] : null;
    }
}
