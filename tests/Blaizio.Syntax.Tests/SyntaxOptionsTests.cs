using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Blaizio.Syntax.Tests.SyntaxTextTests;

namespace Blaizio.Syntax.Tests;

/// <summary>
/// The app's languages (<c>BlaizioOptions.Syntax</c>): adding, aliasing and replacing languages,
/// per app; languages hosted inside others finding the app's set; and the contract a custom
/// <see cref="SyntaxLanguage"/> lives by - a scanner that throws or writes out of order costs its
/// own line, never the render.
/// </summary>
public class SyntaxOptionsTests
{
    private static SyntaxOptions NewSyntax() => new BlaizioOptions().Syntax;

    // ---- per app

    [Fact]
    public void Without_AddBlaizio_the_built_in_languages_are_there()
    {
        var syntax = BlaizioOptions.Resolve(new ServiceCollection().BuildServiceProvider()).Syntax;

        Assert.Equal(SyntaxText.Languages, syntax.Languages);
        Assert.Equal("{K:var} x = {N:1};", Render(syntax.Tokenize("var x = 1;", "cs")));
        Assert.False(syntax.IsSupported("python"));
    }

    [Fact]
    public void Each_options_instance_has_its_own_languages()
    {
        var first = new BlaizioOptions();
        var second = new BlaizioOptions();

        first.Syntax.Add(SyntaxLanguages.Python);

        Assert.Same(first.Syntax, first.Syntax);
        Assert.True(first.Syntax.IsSupported("python"));
        Assert.False(second.Syntax.IsSupported("python"));
        Assert.False(SyntaxText.IsSupported("python"));
    }

    [Fact]
    public void AddBlaizio_configures_the_registered_options()
    {
        var services = new ServiceCollection().AddBlaizio(o => o.Syntax.Add(SyntaxLanguages.Go)).BuildServiceProvider();

        Assert.Equal("go", BlaizioOptions.Resolve(services).Syntax.GetLanguageName("golang"));
    }

    // ---- adding and aliasing

    [Fact]
    public void An_added_language_is_listed_after_the_built_in_ones_and_found_by_name_or_alias()
    {
        var syntax = NewSyntax().Add(SyntaxLanguages.Python, SyntaxLanguages.Rust);

        Assert.Equal([.. SyntaxText.Languages, "python", "rust"], syntax.Languages);
        Assert.Equal("python", syntax.GetLanguageName(" PY "));
        Assert.Equal("rust", syntax.GetLanguageName("rs"));
        Assert.Same(SyntaxLanguages.Python, syntax.Find("python3"));
        Assert.Equal("python", new SyntaxDocument("py", syntax).Language);
    }

    [Fact]
    public void Adding_the_same_language_twice_keeps_one_entry()
    {
        var syntax = NewSyntax().Add(SyntaxLanguages.Lua).Add(SyntaxLanguages.Lua);

        Assert.Single(syntax.Languages, "lua");
    }

    [Fact]
    public void Alias_names_a_language_by_its_name_or_one_of_its_aliases()
    {
        var syntax = NewSyntax()
            .Add(SyntaxLanguages.FSharp)
            .Alias("fsharp-script", "fsx")
            .Alias("pipeline", "YAML");

        Assert.Equal("fsharp", syntax.GetLanguageName("fsharp-script"));
        Assert.Equal("yaml", syntax.GetLanguageName("Pipeline"));
    }

    [Fact]
    public void An_alias_set_in_options_overrides_one_a_language_declares()
    {
        var syntax = NewSyntax().Alias("sh", "powershell");

        Assert.Equal("powershell", syntax.GetLanguageName("sh"));
        Assert.Equal("bash", SyntaxText.GetLanguageName("sh"));
    }

    [Fact]
    public void Alias_refuses_an_unknown_language_and_a_language_name()
    {
        var syntax = NewSyntax();

        Assert.Throws<ArgumentException>(() => syntax.Alias("fsi", "fsharp"));
        Assert.Throws<ArgumentException>(() => syntax.Alias("json", "yaml"));
        Assert.Throws<ArgumentException>(() => syntax.Alias(" ", "yaml"));
    }

    [Fact]
    public void Add_refuses_a_language_without_a_usable_name()
    {
        var syntax = NewSyntax();

        Assert.Throws<ArgumentException>(() => syntax.Add(new Named("")));
        Assert.Throws<ArgumentException>(() => syntax.Add(new Named("two words")));
        Assert.Throws<ArgumentException>(() => new SyntaxGrammar("two words"));
    }

    [Fact]
    public void A_language_aliasing_a_built_in_name_keeps_the_built_in()
    {
        var syntax = NewSyntax().Add(new SyntaxGrammar("jsonish", "json", "jsonish5"));

        Assert.Equal("json", syntax.GetLanguageName("json"));
        Assert.Equal("jsonish", syntax.GetLanguageName("jsonish5"));
    }

    // ---- replacing built-in languages

    [Fact]
    public void A_language_with_a_built_in_name_replaces_it_by_name_and_by_alias()
    {
        var json = new SyntaxGrammar("json").Keywords("yes");
        var syntax = NewSyntax().Add(json);

        Assert.Equal(SyntaxText.Languages, syntax.Languages);
        Assert.Same(json, syntax.Find("json"));
        Assert.Same(json, syntax.Find("jsonc"));
        Assert.Equal("{K:yes} no", Render(syntax.Tokenize("yes no", "json5")));
    }

    [Fact]
    public void A_replaced_language_is_the_one_its_hosts_embed()
    {
        var syntax = NewSyntax().Add(
            new SyntaxGrammar("css").Keywords("red"),
            new SyntaxGrammar("javascript").Keywords("go"),
            new SyntaxGrammar("bash").Keywords("hi"),
            new SyntaxGrammar("json").Keywords("ok"));

        Assert.Equal("<{G:style}>a { color: {K:red} }</{G:style}>", Render(syntax.Tokenize("<style>a { color: red }</style>", "html")));
        Assert.Equal("<{G:script}>{K:go}()</{G:script}>", Render(syntax.Tokenize("<script>go()</script>", "html")));
        Assert.Equal("<{G:script}>{K:go}</{G:script}>", Render(syntax.Tokenize("<script>go</script>", "razor")));
        Assert.Equal("{M:$ }echo {K:hi}", Render(syntax.Tokenize("$ echo hi", "console")));
        Assert.Equal("{P:run}: echo {K:hi}", Render(syntax.Tokenize("run: echo hi", "yaml")));
        Assert.Equal("{K:RUN} echo {K:hi}", Render(syntax.Tokenize("RUN echo hi", "dockerfile")));
        Assert.Equal("{K:POST} {U:/x}\n\n{ {K:ok} }", Render(syntax.Tokenize("POST /x\n\n{ ok }", "http")));

        var yaml = NewSyntax().Add(new SyntaxGrammar("yaml").Keywords("front"));
        Assert.Equal("{M:---}\n{K:front}: 1\n{M:---}", Render(yaml.Tokenize("---\nfront: 1\n---", "markdown")));
    }

    [Fact]
    public void A_markdown_fence_finds_the_apps_languages_by_name_and_alias()
    {
        var syntax = NewSyntax().Add(SyntaxLanguages.Python).Alias("snake", "python");

        Assert.Equal(
            "{M:```}{T:py}\n{K:def} {F:f}(): {K:return} {L:None}\n{M:```}\n{M:~~~}{T:snake}\n{K:pass}\n{M:~~~}",
            Render(syntax.Tokenize("```py\ndef f(): return None\n```\n~~~snake\npass\n~~~", "markdown")));
        // Without the app's options the fence is plain text, as for any unknown language.
        Assert.Equal("{M:```}{T:py}\n{S:def f(): pass}\n{M:```}", Render("```py\ndef f(): pass\n```", "markdown"));
    }

    // ---- the contract for custom languages

    [Fact]
    public void A_language_that_throws_gets_its_line_plain_and_the_next_line_starts_fresh()
    {
        var syntax = NewSyntax().Add(new Throwing());

        var lines = syntax.Tokenize("one\nboom\nthree", "throwing");

        Assert.Equal("{S:one}\nboom\n{S:three}", Render(lines));
        Assert.Equal("open", lines[0].EndState);
        Assert.Null(lines[1].EndState);
        Assert.Null(lines[2].StartState);
    }

    [Fact]
    public void A_hosted_language_that_throws_loses_only_its_part_of_the_line()
    {
        var syntax = NewSyntax().Add(new Throwing());

        Assert.Equal(
            "{M:```}{T:throwing}\n{S:one}\nboom\n{S:three}\n{M:```}\n{H:# Done}",
            Render(syntax.Tokenize("```throwing\none\nboom\nthree\n```\n# Done", "markdown")));
    }

    [Fact]
    public void Ranges_out_of_order_or_out_of_bounds_are_clipped_and_the_runs_still_join_back()
    {
        var syntax = NewSyntax().Add(new Sloppy());

        var line = Assert.Single(syntax.Tokenize("abcdef", "sloppy"));

        Assert.Equal("ab{K:cdef}", Render([line]));
        Assert.Equal("abcdef", string.Concat(line.Runs.Select(r => r.Text)));
    }

    [Fact]
    public void A_hosted_language_cannot_write_past_its_range()
    {
        var syntax = NewSyntax().Add(new Greedy("javascript"));

        Assert.Equal("<{G:script}>{K:x = 1}</{G:script}> after", Render(syntax.Tokenize("<script>x = 1</script> after", "html")));
    }

    [Fact]
    public void A_language_can_host_another_through_the_writer()
    {
        var syntax = NewSyntax().Add(new Host());

        Assert.Equal("{M:sql:}{K:SELECT} {N:1}", Render(syntax.Tokenize("sql:SELECT 1", "host")));
    }

    [Fact]
    public void Adding_a_grammar_seals_it()
    {
        var grammar = new SyntaxGrammar("tiny").Keywords("a");
        NewSyntax().Add(grammar);

        Assert.Throws<InvalidOperationException>(() => grammar.Keywords("b"));
    }

    // ---- the Code Block

    [Fact]
    public void The_code_block_highlights_a_language_added_in_AddBlaizio()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlaizio(o => o.Syntax.Add(SyntaxLanguages.Python));

        var cut = ctx.Render<BzCodeBlockContent>(p => p
            .Add(x => x.Code, "def f():\n    return None")
            .Add(x => x.Language, "py"));

        Assert.Equal(["def", "return"], cut.FindAll("[data-token=keyword]").Select(e => e.TextContent));
        Assert.Equal(["None"], cut.FindAll("[data-token=constant]").Select(e => e.TextContent));
        Assert.Contains("bz-code-block-syntax", cut.Find("pre").ClassName);
    }

    [Fact]
    public void Without_AddBlaizio_the_code_block_highlights_the_built_in_languages_only()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var python = ctx.Render<BzCodeBlockContent>(p => p.Add(x => x.Code, "def f(): pass").Add(x => x.Language, "python"));
        var csharp = ctx.Render<BzCodeBlockContent>(p => p.Add(x => x.Code, "var x = 1;").Add(x => x.Language, "csharp"));

        Assert.Empty(python.FindAll("[data-token]"));
        Assert.Equal(["var"], csharp.FindAll("[data-token=keyword]").Select(e => e.TextContent));
    }

    // ---- custom languages used above

    private sealed class Named(string name) : SyntaxLanguage
    {
        public override string Name => name;

        public override object? Tokenize(string line, int start, int end, object? state, SyntaxWriter writer) => null;
    }

    // String lines ending in an "open" state; a line with "boom" in it writes, then throws.
    private sealed class Throwing : SyntaxLanguage
    {
        public override string Name => "throwing";

        public override object? Tokenize(string line, int start, int end, object? state, SyntaxWriter writer)
        {
            if (line.AsSpan(start, end - start).Contains("boom", StringComparison.Ordinal))
            {
                writer.Add(start, end, SyntaxKind.Keyword);
                throw new InvalidOperationException("boom");
            }
            writer.Add(start, end, SyntaxKind.String);
            return "open";
        }
    }

    // Writes past the end, then out of order, then nowhere at all.
    private sealed class Sloppy : SyntaxLanguage
    {
        public override string Name => "sloppy";

        public override object? Tokenize(string line, int start, int end, object? state, SyntaxWriter writer)
        {
            writer.Add(start + 2, end + 10, SyntaxKind.Keyword);
            writer.Add(start, start + 4, SyntaxKind.String);
            writer.Add(-5, -1, SyntaxKind.Number);
            return null;
        }
    }

    // Claims the whole line, whatever range it was given.
    private sealed class Greedy(string name) : SyntaxLanguage
    {
        public override string Name => name;

        public override object? Tokenize(string line, int start, int end, object? state, SyntaxWriter writer)
        {
            writer.Add(0, line.Length, SyntaxKind.Keyword);
            return null;
        }
    }

    // "sql:" then SQL, through the writer's lookup and Embed.
    private sealed class Host : SyntaxLanguage
    {
        public override string Name => "host";

        public override object? Tokenize(string line, int start, int end, object? state, SyntaxWriter writer)
        {
            writer.Add(start, start + 4, SyntaxKind.Meta);
            return writer.Embed(writer.Find("sql")!, start + 4, end, state);
        }
    }
}
