using Xunit;
using static Blaizio.Syntax.Tests.SyntaxTextTests;

namespace Blaizio.Syntax.Tests;

/// <summary>
/// <see cref="SyntaxGrammar"/>, rule by rule: comments, strings and their holes, character
/// literals, heredocs, tokens, word lists, declarations, keys, sigils, names, numbers and patterns,
/// what carries across lines, and the guarantees every grammar inherits (it never throws on
/// content, its runs join back into the line, streamed and whole tokenizing agree).
/// </summary>
public class SyntaxGrammarTests
{
    private static string Tokens(SyntaxGrammar grammar, string code)
    {
        var syntax = new BlaizioOptions().Syntax.Add(grammar);
        return Render(syntax.Tokenize(code, grammar.Name));
    }

    private static SyntaxGrammar G() => new("test");

    // ---- comments and lines

    [Fact]
    public void A_line_comment_runs_to_the_end_of_the_line()
    {
        Assert.Equal("x {C:// note}\ny", Tokens(G().LineComment("//"), "x // note\ny"));
    }

    [Fact]
    public void A_line_start_comment_only_opens_a_line()
    {
        var grammar = G().LineComment("#", lineStart: true);

        Assert.Equal("  {C:# yes}\nx # no", Tokens(grammar, "  # yes\nx # no"));
    }

    [Fact]
    public void A_word_marker_matches_whole_words_and_with_IgnoreCase_any_case()
    {
        var grammar = G().IgnoreCase().LineComment("REM");

        Assert.Equal("{C:rem one}\n{C:REM two}\nremove three\nxREM", Tokens(grammar, "rem one\nREM two\nremove three\nxREM"));
        Assert.Equal("remark", Tokens(grammar, "remark"));
    }

    [Fact]
    public void A_line_rule_gives_the_rest_of_the_line_a_kind()
    {
        var grammar = G().Line("#", SyntaxKind.Meta, lineStart: true).Numbers();

        Assert.Equal("{M:#include <stdio.h>}\nx = {N:1} # y", Tokens(grammar, "#include <stdio.h>\nx = 1 # y"));
    }

    [Fact]
    public void A_block_comment_spans_lines()
    {
        Assert.Equal("a {C:/* one}\n{C:two */} b", Tokens(G().BlockComment("/*", "*/"), "a /* one\ntwo */ b"));
    }

    [Fact]
    public void A_nested_block_comment_needs_a_close_for_each_open()
    {
        const string code = "(* a (* b *) c *) d";

        Assert.Equal("{C:(* a (* b *) c *)} d", Tokens(G().BlockComment("(*", "*)", nested: true), code));
        Assert.Equal("{C:(* a (* b *)} c *) d", Tokens(G().BlockComment("(*", "*)"), code));
        Assert.Equal("{C:(* (* a *)}\n{C:b *)} c", Tokens(G().BlockComment("(*", "*)", nested: true), "(* (* a *)\nb *) c"));
    }

    [Fact]
    public void A_token_beats_a_shorter_marker_it_starts_with()
    {
        var grammar = G().Token("(*)", SyntaxKind.Text).BlockComment("(*", "*)");

        Assert.Equal("fold (*) 1 {C:(* c *)}", Tokens(grammar, "fold (*) 1 (* c *)"));
    }

    // ---- strings

    [Fact]
    public void A_string_reads_backslash_escapes()
    {
        Assert.Equal("{S:\"a}{E:\\n}{S:b}{E:\\u00e9}{S:\"} x", Tokens(G().String("\""), "\"a\\nb\\u00e9\" x"));
    }

    [Fact]
    public void A_doubled_quote_is_an_escape_when_the_quote_is_the_escape_character()
    {
        Assert.Equal("{S:\"say }{E:\"\"}{S:hi}{E:\"\"}{S:\"} x", Tokens(G().String("\"", escape: '"'), "\"say \"\"hi\"\"\" x"));
    }

    [Fact]
    public void A_raw_string_has_no_escapes()
    {
        Assert.Equal("{S:`C:\\temp\\`} x", Tokens(G().String("`", escape: '\0'), "`C:\\temp\\` x"));
    }

    [Fact]
    public void A_one_line_string_closes_at_the_line_end_and_a_multiline_one_carries_on()
    {
        Assert.Equal("{S:\"open}\nx", Tokens(G().String("\""), "\"open\nx"));
        Assert.Equal("{S:\"\"\"one}\n{S:two\"\"\"} x", Tokens(G().String("\"\"\"", multiline: true), "\"\"\"one\ntwo\"\"\" x"));
    }

    [Fact]
    public void A_longer_opener_wins()
    {
        var grammar = G().String("\"").String("\"\"\"", multiline: true);

        Assert.Equal("{S:\"\"\"a\"b\"\"\"} {S:\"c\"}", Tokens(grammar, "\"\"\"a\"b\"\"\" \"c\""));
    }

    [Fact]
    public void A_prefixed_string_needs_its_prefix_in_any_case()
    {
        var grammar = G().String("'").String("'", escape: '\0', prefixes: "r br");

        // x is not a prefix: x'\' is a name, then a plain string whose escaped quote leaves it open.
        Assert.Equal("{S:'}{E:\\d}{S:'} {S:r'\\d'} {S:BR'\\d'} x{S:'}{E:\\'}", Tokens(grammar, "'\\d' r'\\d' BR'\\d' x'\\'"));
    }

    [Fact]
    public void Braces_open_holes_of_code_and_double_braces_are_escapes()
    {
        var grammar = G().String("\"", interpolation: SyntaxInterpolation.Braces, prefixes: "f").Keywords("if").Numbers();

        Assert.Equal(
            "{S:f\"}{E:{}a {K:if} b{E:}}{S: }{E:{{}{S:x}{E:}}}{S: }{E:{}{N:1}{E:}}{S:\"}",
            Tokens(grammar, "f\"{a if b} {{x}} {1}\""));
    }

    [Fact]
    public void A_hole_counts_its_brackets_and_can_hold_strings()
    {
        var grammar = G().String("\"", interpolation: SyntaxInterpolation.DollarBraces);

        Assert.Equal("{S:\"}{E:${}f({ a: {S:\"}\"} }) {E:}}{S:!\"}", Tokens(grammar, "\"${f({ a: \"}\" }) }!\""));
    }

    [Fact]
    public void Each_interpolation_style_opens_its_own_holes()
    {
        Assert.Equal("{S:\"}{E:#{}x{E:}}{S: ${y}\"}", Tokens(G().String("\"", interpolation: SyntaxInterpolation.HashBraces), "\"#{x} ${y}\""));
        Assert.Equal("{S:\"}{E:\\(}a(b){E:)}{S:\"}", Tokens(G().String("\"", interpolation: SyntaxInterpolation.BackslashParens), "\"\\(a(b))\""));
        Assert.Equal("{S:\"}{V:$name}{S: $ 1\"}", Tokens(G().String("\"", interpolation: SyntaxInterpolation.DollarName), "\"$name $ 1\""));
    }

    [Fact]
    public void A_hole_in_a_multiline_string_carries_across_lines()
    {
        var grammar = G().String("\"\"\"", multiline: true, interpolation: SyntaxInterpolation.DollarBraces).Keywords("x");

        Assert.Equal("{S:\"\"\"a }{E:${}\n  {K:x}\n{E:}}{S: b\"\"\"} {K:x}", Tokens(grammar, "\"\"\"a ${\n  x\n} b\"\"\" x"));
    }

    [Fact]
    public void A_hole_in_a_one_line_string_closes_with_the_line()
    {
        var grammar = G().String("\"", interpolation: SyntaxInterpolation.DollarBraces).Keywords("x");

        Assert.Equal("{S:\"}{E:${}{K:x}\n{K:x}", Tokens(grammar, "\"${x\nx"));
    }

    [Fact]
    public void A_span_can_be_any_kind()
    {
        var grammar = G().String("#[", "]", escape: '\0', kind: SyntaxKind.Meta).String("$(", ")", escape: '\0', kind: SyntaxKind.Variable);

        Assert.Equal("{M:#[derive(Debug)]} {V:$(CC)}", Tokens(grammar, "#[derive(Debug)] $(CC)"));
    }

    [Fact]
    public void A_character_literal_is_one_character_or_one_escape()
    {
        var grammar = G().Char().Sigil('\'', SyntaxKind.Variable);

        Assert.Equal("{S:'a'} {S:'}{E:\\n}{S:'} <{V:'a}>", Tokens(grammar, "'a' '\\n' <'a>"));
        Assert.Equal("{V:'ab}'", Tokens(grammar, "'ab'"));
        Assert.Equal("''", Tokens(grammar, "''"));
    }

    [Fact]
    public void A_heredoc_runs_to_its_terminator()
    {
        var grammar = G().Heredoc("<<~").Heredoc("<<").Keywords("x");

        Assert.Equal(
            "{K:x} = {S:<<~SQL}.strip\n{S:  select 1}\n{S:  SQL}\n{K:x}",
            Tokens(grammar, "x = <<~SQL.strip\n  select 1\n  SQL\nx"));
        Assert.Equal("{S:<<'eot'}\n{S:x}\n{S:eot};", Tokens(grammar, "<<'eot'\nx\neot;"));
        // A bare terminator must be upper-case: a << b and a <<b are operators.
        Assert.Equal("a << b\na <<b", Tokens(grammar, "a << b\na <<b"));
    }

    // ---- words

    [Fact]
    public void Listed_words_take_their_kind_and_the_first_list_wins()
    {
        var grammar = G()
            .Keywords("if else")
            .Constants("true null")
            .Types("int")
            .Words("print if", SyntaxKind.Function);

        Assert.Equal("{K:if} {L:true} {T:int} {F:print} {K:else} {L:null} other", Tokens(grammar, "if true int print else null other"));
    }

    [Fact]
    public void IgnoreCase_matches_listed_words_in_any_case()
    {
        Assert.Equal("{K:Dim} {K:DIM} {K:dim}", Tokens(G().IgnoreCase().Keywords("Dim"), "Dim DIM dim"));
        Assert.Equal("{K:Dim} DIM dim", Tokens(G().Keywords("Dim"), "Dim DIM dim"));
    }

    [Fact]
    public void A_name_after_a_dot_is_a_member_not_a_listed_word()
    {
        var grammar = G().Keywords("match class").Functions();

        Assert.Equal("{K:match} re.{F:match}(x) obj.class", Tokens(grammar, "match re.match(x) obj.class"));
    }

    [Fact]
    public void A_declaration_names_what_follows_it()
    {
        var grammar = G()
            .Declarations("def", SyntaxKind.Function)
            .Declarations("class", SyntaxKind.Type);

        Assert.Equal("{K:def} {F:area} {K:class} {T:shape}: x", Tokens(grammar, "def area class shape: x"));
        // Anything else in between ends the declaration.
        Assert.Equal("{K:class} <T> name", Tokens(grammar, "class <T> name"));
    }

    [Fact]
    public void Keys_are_names_followed_by_a_separator()
    {
        var grammar = G().Keys(":").Keys("=", SyntaxKind.Variable, lineStart: true);

        Assert.Equal("{P:id}: 1, a : b, a::b", Tokens(grammar, "id: 1, a : b, a::b"));
        Assert.Equal("  {V:name}    = 1\nx == y", Tokens(grammar, "  name    = 1\nx == y"));
    }

    [Fact]
    public void A_separator_that_continues_into_another_operator_is_not_one()
    {
        var grammar = G().Keys("= :", lineStart: true);

        Assert.Equal("a == b\na := b\na => b\n{P:a} = b", Tokens(grammar, "a == b\na := b\na => b\na = b"));
    }

    [Fact]
    public void The_first_word_of_a_line_takes_the_FirstWord_kind()
    {
        var grammar = G().Keywords("server").FirstWord(SyntaxKind.Property).String("\"");

        Assert.Equal("{K:server} {\n    {P:listen} 80 ssl;\n}\n{S:\"x\"} y", Tokens(grammar, "server {\n    listen 80 ssl;\n}\n\"x\" y"));
    }

    [Fact]
    public void A_sigil_marks_the_name_after_it()
    {
        var grammar = G()
            .Sigil('$', SyntaxKind.Variable)
            .Sigil('@', SyntaxKind.Meta, ".")
            .Sigil('%', SyntaxKind.Variable, "~", closed: true)
            .Sigil(':', SyntaxKind.Function, lineStart: true);

        Assert.Equal(
            "{V:$a} {V:$$b} {M:@app.route} user@host {V:%PATH%} {V:%%i} {V:%~dp0} 5 % 2\n{F::label} goto :label",
            Tokens(grammar, "$a $$b @app.route user@host %PATH% %%i %~dp0 5 % 2\n:label goto :label"));
    }

    [Fact]
    public void Identifier_chars_extend_names_but_not_into_an_operator()
    {
        var grammar = G().IdentifierChars("?!").Keywords("defined?").Functions();

        Assert.Equal("{K:defined?} {F:empty?}(x) a!=b {F:save!}()", Tokens(grammar, "defined? empty?(x) a!=b save!()"));
    }

    [Fact]
    public void A_name_before_a_paren_is_a_function_and_a_macro_marker_makes_one_too()
    {
        var grammar = G().Functions(macro: '!');

        Assert.Equal("{F:run}(1) run (1) {F:println!}(x) {F:vec!}[1] a != b", Tokens(grammar, "run(1) run (1) println!(x) vec![1] a != b"));
    }

    [Fact]
    public void Constant_case_and_pascal_case_names_follow_their_switches()
    {
        Assert.Equal("{L:MAX_SIZE} {T:Order} plain {T:A}", Tokens(G().ConstantCase().PascalCaseTypes(), "MAX_SIZE Order plain A"));
        Assert.Equal("MAX_SIZE Order plain", Tokens(G(), "MAX_SIZE Order plain"));
    }

    [Fact]
    public void Numbers_are_off_until_declared_and_never_start_inside_a_name()
    {
        Assert.Equal("x1 = 2", Tokens(G(), "x1 = 2"));
        Assert.Equal("x1 = {N:0x1F} + {N:1_000.5e3} + {N:10u8} - {N:.5}", Tokens(G().Numbers(), "x1 = 0x1F + 1_000.5e3 + 10u8 - .5"));
    }

    // ---- patterns

    [Fact]
    public void A_pattern_colors_its_match_before_any_other_rule()
    {
        var grammar = G()
            .Pattern(@"\d{4}-\d{2}-\d{2}", SyntaxKind.Meta)
            .Keywords("INFO")
            .Pattern(@"INFO\b", SyntaxKind.Inserted)
            .Numbers();

        Assert.Equal("{M:2026-10-26} {+:INFO} {N:42}", Tokens(grammar, "2026-10-26 INFO 42"));
    }

    [Fact]
    public void A_pattern_with_several_kinds_colors_its_groups()
    {
        var grammar = G().Pattern(@"(let)\s+(\w+)\s*=", SyntaxKind.Keyword, SyntaxKind.Variable);

        Assert.Equal("{K:let} {V:total} = 1", Tokens(grammar, "let total = 1"));
    }

    [Fact]
    public void An_empty_pattern_match_does_not_count()
    {
        var grammar = G().Pattern(@"x*", SyntaxKind.Keyword).Numbers();

        Assert.Equal("{N:1} {K:xx} y", Tokens(grammar, "1 xx y"));
    }

    [Fact]
    public void A_pattern_is_checked_when_it_is_declared()
    {
        Assert.ThrowsAny<ArgumentException>(() => G().Pattern("(unclosed", SyntaxKind.Keyword));
        Assert.Throws<ArgumentException>(() => G().Pattern("a"));
    }

    // ---- the grammar as a language

    [Fact]
    public void A_grammar_is_fixed_once_it_tokenizes()
    {
        var grammar = G().Keywords("a");
        Tokens(grammar, "a");

        Assert.Throws<InvalidOperationException>(() => grammar.Numbers());
        Assert.Throws<InvalidOperationException>(() => grammar.String("\""));
    }

    [Fact]
    public void A_grammars_states_compare_by_value_so_unchanged_lines_are_reused()
    {
        var syntax = new BlaizioOptions().Syntax.Add(Featured());
        var doc = new SyntaxDocument("featured", syntax);
        var first = doc.Update("/* open\nstill \"\"\"x\nmore");
        var count = doc.TokenizedLineCount;

        var second = doc.Update("/* open\nstill \"\"\"x\nmore\n\"\"\" done */ y");

        Assert.Same(first[0], second[0]);
        Assert.Same(first[2], second[2]);
        Assert.Equal(count + 1, doc.TokenizedLineCount);
    }

    [Fact]
    public void Streamed_tokenizing_matches_whole_tokenizing()
    {
        var syntax = new BlaizioOptions().Syntax.Add(Featured());
        var whole = Render(syntax.Tokenize(FeaturedSample, "featured"));

        var doc = new SyntaxDocument("featured", syntax);
        var lines = FeaturedSample.Split('\n');
        for (var i = 1; i <= lines.Length; i++) doc.Update(string.Join('\n', lines, 0, i));

        Assert.Equal(whole, Render(doc.Lines));
    }

    [Fact]
    public void A_grammar_never_throws_and_its_runs_join_back_into_every_line()
    {
        var grammar = Featured();
        Tokens(grammar, "x");
        var random = new Random(20261026);
        const string alphabet = "ab_ 019.\"'`$#@%{}()[]<>/*\\!?:=-~\t\n\u00e9\u4e2d";
        for (var round = 0; round < 400; round++)
        {
            var chars = new char[random.Next(0, 80)];
            for (var i = 0; i < chars.Length; i++) chars[i] = alphabet[random.Next(alphabet.Length)];
            object? state = null;
            foreach (var line in new string(chars).Split('\n'))
            {
                var writer = new SyntaxWriter(SyntaxRegistry.BuiltIn);
                writer.Reset(line);
                state = grammar.Tokenize(line, 0, line.Length, state, writer);
                Assert.Equal(line, string.Concat(writer.Finish().Select(r => r.Text)));
            }
        }
    }

    // Every rule at once.
    private static SyntaxGrammar Featured() => new SyntaxGrammar("featured")
        .LineComment("//")
        .BlockComment("/*", "*/", nested: true)
        .Line("#!", SyntaxKind.Meta, lineStart: true)
        .String("\"\"\"", multiline: true, interpolation: SyntaxInterpolation.DollarBraces | SyntaxInterpolation.DollarName)
        .String("\"", interpolation: SyntaxInterpolation.Braces | SyntaxInterpolation.HashBraces | SyntaxInterpolation.BackslashParens)
        .String("'", escape: '\'')
        .String("'", escape: '\0', prefixes: "r")
        .String("#[", "]", escape: '\0', kind: SyntaxKind.Meta)
        .Char('`')
        .Heredoc("<<")
        .Token("(*)", SyntaxKind.Text)
        .Pattern(@"\d+ms\b", SyntaxKind.Number)
        .Keywords("if else for def")
        .Constants("true false")
        .Types("int")
        .Declarations("def", SyntaxKind.Function)
        .Keys(":")
        .Keys("=", SyntaxKind.Variable, lineStart: true)
        .Sigil('$', SyntaxKind.Variable)
        .Sigil('%', SyntaxKind.Variable, "~", closed: true)
        .Sigil('@', SyntaxKind.Meta, ".")
        .IdentifierChars("?!")
        .FirstWord(SyntaxKind.Property)
        .Functions(macro: '!')
        .ConstantCase()
        .PascalCaseTypes()
        .Numbers();

    private const string FeaturedSample = """"
        #!/usr/bin/env featured
        /* a (* nested /* deeper */ still */
        def area(r: int) = r * r // squared
        name = "x {r} #{y} \(z)" + '''' + r'\n' + `a`
        doc = <<END
        body ${not} a hole
        END
        msg = """first ${
          if true { 1 } else { 2 }
        } $who"""
        #[inline] @app.route %PATH% $HOME println!(MAX_SIZE, Order, 15ms, 0x1F)
        """";
}
