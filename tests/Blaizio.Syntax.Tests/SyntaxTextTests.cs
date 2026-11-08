using System.Diagnostics;
using System.Text;
using Xunit;

namespace Blaizio.Syntax.Tests;

/// <summary>
/// Syntax highlighting: the language registry, the per-language scanners, and the incremental
/// document. Expectations are written in a compact notation - every run that is not plain text
/// renders as <c>{K:text}</c>, with K the kind's letter (see <see cref="Letter"/>).
/// </summary>
public class SyntaxTextTests
{
    // ---- registry and API

    [Fact]
    public void Lists_the_canonical_languages()
    {
        Assert.Equal(
            ["csharp", "razor", "json", "bash", "console", "powershell", "typescript", "javascript", "html", "xml",
             "css", "sql", "yaml", "diff", "http", "dockerfile", "ini", "toml", "markdown"],
            SyntaxText.Languages);
    }

    [Theory]
    [InlineData("cs", "csharp")]
    [InlineData("C#", "csharp")]
    [InlineData("cshtml", "razor")]
    [InlineData("sh", "bash")]
    [InlineData("shell-session", "console")]
    [InlineData("ps1", "powershell")]
    [InlineData("ts", "typescript")]
    [InlineData("jsx", "javascript")]
    [InlineData("csproj", "xml")]
    [InlineData("yml", "yaml")]
    [InlineData("patch", "diff")]
    [InlineData("docker", "dockerfile")]
    [InlineData("editorconfig", "ini")]
    [InlineData("md", "markdown")]
    [InlineData(" JSON ", "json")]
    public void Aliases_resolve_to_their_language(string alias, string name)
    {
        Assert.True(SyntaxText.IsSupported(alias));
        Assert.Equal(name, SyntaxText.GetLanguageName(alias));
        Assert.Equal(name, new SyntaxDocument(alias).Language);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ansi")]
    [InlineData("rust")]
    public void Unsupported_languages_are_reported(string? language)
    {
        Assert.False(SyntaxText.IsSupported(language));
        Assert.Null(SyntaxText.GetLanguageName(language));
    }

    [Fact]
    public void An_unsupported_language_throws_where_tokenizing_is_asked_for()
    {
        Assert.Throws<ArgumentException>(() => new SyntaxDocument("rust"));
        Assert.Throws<ArgumentException>(() => SyntaxText.Tokenize("fn main() {}", "rust"));
    }

    [Fact]
    public void Splits_on_every_line_break_and_keeps_empty_lines()
    {
        var lines = SyntaxText.Tokenize("a\r\nb\rc\n\nd", "csharp");

        Assert.Equal(["a", "b", "c", "", "d"], lines.Select(l => l.Text));
        Assert.Empty(lines[3].Runs);
    }

    [Fact]
    public void A_plain_line_is_one_run_that_reuses_the_line_string()
    {
        var line = Assert.Single(SyntaxText.Tokenize("just words here", "markdown"));
        var run = Assert.Single(line.Runs);

        Assert.Same(line.Text, run.Text);
        Assert.Equal(SyntaxKind.Text, run.Kind);
    }

    // ---- incremental documents

    [Fact]
    public void Appending_tokenizes_only_the_new_lines_and_keeps_the_rest()
    {
        var doc = new SyntaxDocument("csharp");
        var first = doc.Update("var a = 1;\nvar b = 2;");
        var tokenized = doc.TokenizedLineCount;

        var second = doc.Update("var a = 1;\nvar b = 2;\nvar c = 3;");

        Assert.Same(first[0], second[0]);
        Assert.Same(first[1], second[1]);
        Assert.Equal(tokenized + 1, doc.TokenizedLineCount);
    }

    [Fact]
    public void A_growing_last_line_retokenizes_only_that_line()
    {
        var doc = new SyntaxDocument("typescript");
        var first = doc.Update("const a = 1;\nconst b");
        var count = doc.TokenizedLineCount;

        var second = doc.Update("const a = 1;\nconst b = \"x\";");

        Assert.Same(first[0], second[0]);
        Assert.NotSame(first[1], second[1]);
        Assert.Equal(count + 1, doc.TokenizedLineCount);
    }

    [Fact]
    public void Opening_a_comment_retokenizes_the_lines_after_it_and_closing_it_settles_back()
    {
        var doc = new SyntaxDocument("csharp");
        doc.Update("int a;\nint b;\nint c;");

        var open = doc.Update("/* int a;\nint b;\nint c;");
        Assert.All(open, l => Assert.Equal(SyntaxKind.Comment, Assert.Single(l.Runs).Kind));

        var closed = doc.Update("/* int a; */\nint b;\nint c;");
        Assert.Equal("{C:/* int a; */}\n{K:int} b;\n{K:int} c;", Render(closed));
    }

    [Fact]
    public void An_unchanged_line_after_a_settled_edit_is_reused()
    {
        var doc = new SyntaxDocument("csharp");
        var first = doc.Update("int a;\nint b;\nint c;");

        var edited = doc.Update("int x;\nint b;\nint c;");

        Assert.NotSame(first[0], edited[0]);
        Assert.Same(first[1], edited[1]);
        Assert.Same(first[2], edited[2]);
    }

    [Fact]
    public void A_very_long_line_stays_one_plain_run()
    {
        var text = "var x = " + new string('a', SyntaxDocument.MaxLineLength) + ";";
        var line = Assert.Single(SyntaxText.Tokenize(text, "csharp"));

        Assert.Equal(SyntaxKind.Text, Assert.Single(line.Runs).Kind);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Streaming_a_sample_in_chunks_matches_tokenizing_it_whole(string language, string code)
    {
        var whole = Render(SyntaxText.Tokenize(code, language));
        var doc = new SyntaxDocument(language);
        for (var k = 0; k < code.Length; k += 7) doc.Update(code[..k]);

        Assert.Equal(whole, Render(doc.Update(code)));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_run_joins_back_into_its_line(string language, string code)
    {
        foreach (var line in SyntaxText.Tokenize(code, language))
            Assert.Equal(line.Text, string.Concat(line.Runs.Select(r => r.Text)));
    }

    [Fact]
    public void Random_input_never_throws_and_always_joins_back()
    {
        string[] pieces =
        [
            "{", "}", "[", "]", "(", ")", "<", ">", "</", "/>", "@", "@{", "@(", "@*", "*@", "@code", "@if", "<div ", "<text>",
            "\"", "'", "`", "\"\"\"", "$\"", "@\"", "${", "$(", "$", "#", "//", "/*", "*/", "<!--", "-->", "\\", "|", "-", "--",
            "+", ":", ";", ",", ".", "=", "==", "=>", "&", "&&", "*", "**", "_", "~~~", "```", "---", "@@", "\t", " ", "  ",
            "\n", "\n", "\n", "class", "var", "foo", "Bar", "IBaz", "123", "0x1F", "1.5e3", "true", "null", "<<EOF", "EOF",
            "@\"", "\"@", "<#", "#>", "SELECT", "$$", "FROM", "RUN", "@@ -1,2 +1,3 @@", "GET ", "{{x}}", "[x]", "é", "😀", "\r",
        ];
        var random = new Random(20261024);
        foreach (var language in SyntaxText.Languages)
        {
            for (var n = 0; n < 150; n++)
            {
                var sb = new StringBuilder();
                var length = random.Next(1, 120);
                for (var k = 0; k < length; k++) sb.Append(pieces[random.Next(pieces.Length)]);
                var code = sb.ToString();

                var lines = SyntaxText.Tokenize(code, language);
                foreach (var line in lines)
                    Assert.Equal(line.Text, string.Concat(line.Runs.Select(r => r.Text)));

                // Fed in pieces, the document lands on the same runs.
                var doc = new SyntaxDocument(language);
                for (var k = 0; k < code.Length; k += 11) doc.Update(code[..k]);
                Assert.Equal(Render(lines), Render(doc.Update(code)));
            }
        }
    }

    [Theory]
    [MemberData(nameof(LanguageNames))]
    public void A_long_pathological_line_stays_fast(string language)
    {
        var line = string.Concat(Enumerable.Repeat("A<B[(\"'@{$(`*_", 600));
        var watch = Stopwatch.StartNew();
        SyntaxText.Tokenize(line, language);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"{language} took {watch.ElapsedMilliseconds} ms");
    }

    // ---- C#

    [Fact]
    public void CSharp_keywords_constants_types_and_functions()
    {
        Assert.Equal(
            "{K:public} {K:sealed} {K:class} {T:OrderService} : {T:IOrderService}",
            Render("public sealed class OrderService : IOrderService", "csharp"));
        Assert.Equal(
            "{K:private} {K:readonly} {T:Dictionary}<{K:string}, {T:List}<{T:Order}>> _orders = {K:new}();",
            Render("private readonly Dictionary<string, List<Order>> _orders = new();", "csharp"));
        Assert.Equal(
            "{K:public} {K:async} {T:Task}<{T:Order}?> {F:FindAsync}({K:int} id, {T:CancellationToken} token = {K:default})",
            Render("public async Task<Order?> FindAsync(int id, CancellationToken token = default)", "csharp"));
        Assert.Equal(
            "{K:if} (order {K:is} {K:not} {L:null} && order.Total > {N:0.5m}) {K:return} {L:true};",
            Render("if (order is not null && order.Total > 0.5m) return true;", "csharp"));
    }

    [Fact]
    public void CSharp_contextual_keywords_only_where_they_are_keywords()
    {
        Assert.Equal("{K:var} file = {F:Open}();", Render("var file = Open();", "csharp"));
        Assert.Equal("{K:file} {K:sealed} {K:class} {T:Helper} { }", Render("file sealed class Helper { }", "csharp"));
        Assert.Equal("{K:foreach} ({K:var} record {K:in} records) { }", Render("foreach (var record in records) { }", "csharp"));
        Assert.Equal("{K:public} {K:record} {T:Person}({K:string} Name);", Render("public record Person(string Name);", "csharp"));
        Assert.Equal("{K:public} {K:int} Count { {K:get}; {K:private} {K:set}; }", Render("public int Count { get; private set; }", "csharp"));
        Assert.Equal("set.{F:Add}(value);", Render("set.Add(value);", "csharp"));
    }

    [Fact]
    public void CSharp_strings_with_escapes_interpolation_verbatim_and_raw()
    {
        Assert.Equal("{S:\"a}{E:\\n}{S:b\"}", Render("\"a\\nb\"", "csharp"));
        Assert.Equal(
            "{S:$\"Total: }{E:{}order.Total{S::C2}{E:}}{S: }{E:{{}{S:x}{E:}}}{S:\"}",
            Render("$\"Total: {order.Total:C2} {{x}}\"", "csharp"));
        Assert.Equal("{S:@\"C:\\temp\\\"} + {S:\"done\"}", Render("@\"C:\\temp\\\" + \"done\"", "csharp"));
        Assert.Equal("{S:@\"say }{E:\"\"}{S:hi}{E:\"\"}{S:\"}", Render("@\"say \"\"hi\"\"\"", "csharp"));
        Assert.Equal("{S:'x'}, {S:'}{E:\\'}{S:'}", Render("'x', '\\''", "csharp"));
        Assert.Equal("{S:\"bytes\"u8}", Render("\"bytes\"u8", "csharp"));
    }

    [Fact]
    public void CSharp_raw_strings_span_lines_and_keep_comment_markers_literal()
    {
        Assert.Equal(
            "{K:var} json = {S:\"\"\"}\n{S:    { \"a\": 1 } // not a comment}\n{S:    \"\"\"};",
            Render("var json = \"\"\"\n    { \"a\": 1 } // not a comment\n    \"\"\";", "csharp"));
        Assert.Equal(
            "{S:$$\"\"\"{literal} }{E:{{}name{E:}}}{S:\"\"\"}",
            Render("$$\"\"\"{literal} {{name}}\"\"\"", "csharp"));
    }

    [Fact]
    public void CSharp_comments_preprocessor_and_attributes()
    {
        Assert.Equal(
            "{M:#nullable enable}\n[{T:Parameter}, {T:EditorRequired}] {K:public} {K:string} Name { {K:get}; {K:set}; } {C:// the name}",
            Render("#nullable enable\n[Parameter, EditorRequired] public string Name { get; set; } // the name", "csharp"));
        Assert.Equal("[{K:assembly}: {T:InternalsVisibleTo}({S:\"Tests\"})]", Render("[assembly: InternalsVisibleTo(\"Tests\")]", "csharp"));
        Assert.Equal("{C:/* a}\n{C:b */} {K:int} x;", Render("/* a\nb */ int x;", "csharp"));
        Assert.Equal("items[{N:0}] = values[index];", Render("items[0] = values[index];", "csharp"));
    }

    [Fact]
    public void CSharp_type_positions()
    {
        Assert.Contains("{K:catch} ({T:InvalidOperationException} ex)", Render("catch (InvalidOperationException ex)", "csharp"));
        Assert.Contains("{K:typeof}({T:Order})", Render("typeof(Order)", "csharp"));
        Assert.Contains("{K:as} {T:IDisposable}", Render("x as IDisposable", "csharp"));
        Assert.Contains("{K:new} {T:System}.{T:Text}.{T:StringBuilder}()", Render("new System.Text.StringBuilder()", "csharp"));
        Assert.Contains("{K:where} {T:T} : {K:class}, {T:IEntity}", Render("void M<T>() where T : class, IEntity", "csharp"));
        Assert.Contains("{T:List}<({K:int} Id, {K:string} Name)>", Render("List<(int Id, string Name)> rows;", "csharp"));
        Assert.Equal("{K:if} (a < b && c > d) { }", Render("if (a < b && c > d) { }", "csharp"));
    }

    // ---- Razor

    [Fact]
    public void Razor_directives()
    {
        Assert.Equal(
            "{K:@page} {S:\"/orders/{id:int}\"}\n{K:@using} Blaizio.Ui\n{K:@inject} {T:NavigationManager} Navigation\n{K:@inherits} {T:LayoutComponentBase}\n{K:@typeparam} {T:TItem} {K:where} {T:TItem} : {K:notnull}",
            Render("@page \"/orders/{id:int}\"\n@using Blaizio.Ui\n@inject NavigationManager Navigation\n@inherits LayoutComponentBase\n@typeparam TItem where TItem : notnull", "razor"));
    }

    [Fact]
    public void Razor_markup_components_expressions_and_escapes()
    {
        Assert.Equal(
            "<{G:p} {A:class}={S:\"lead\"}>Hi {K:@}user.Name, mail sales@example.com or {E:@@}team</{G:p}>",
            Render("<p class=\"lead\">Hi @user.Name, mail sales@example.com or @@team</p>", "razor"));
        Assert.Equal(
            "<{T:BzButton} {A:Variant}={S:\"ButtonVariant.Outline\"} {A:OnClick}={S:\"}{K:@(}() => {F:Save}({S:\"x\"}){K:)}{S:\"} />",
            Render("<BzButton Variant=\"ButtonVariant.Outline\" OnClick=\"@(() => Save(\"x\"))\" />", "razor"));
        Assert.Equal(
            "<{G:input} {K:@bind-Value}={S:\"}model.Name{S:\"} {K:@onclick}={S:\"}() => count++{S:\"} />",
            Render("<input @bind-Value=\"model.Name\" @onclick=\"() => count++\" />", "razor"));
        Assert.Equal("{K:@}{F:Format}(price) {K:@}items[{N:0}]?.Title", Render("@Format(price) @items[0]?.Title", "razor"));
        Assert.Equal("{K:@await} Component.{F:InvokeAsync}()", Render("@await Component.InvokeAsync()", "razor"));
    }

    [Fact]
    public void Razor_control_structures_with_markup_inside_and_else_chains()
    {
        const string code = """
            @if (orders is null)
            {
                <p>Loading</p>
            }
            else
            {
                @foreach (var order in orders)
                {
                    <li>@order.Id</li>
                }
            }
            <footer></footer>
            """;
        Assert.Equal(
            Lf("""
            {K:@if} (orders {K:is} {L:null})
            {K:{}
                <{G:p}>Loading</{G:p}>
            {K:}}
            {K:else}
            {K:{}
                {K:@foreach} ({K:var} order {K:in} orders)
                {K:{}
                    <{G:li}>{K:@}order.Id</{G:li}>
                {K:}}
            {K:}}
            <{G:footer}></{G:footer}>
            """),
            Render(Lf(code), "razor"));
    }

    [Fact]
    public void Razor_code_blocks_with_templates_text_lines_and_comments()
    {
        const string code = """
            @code {
                [Parameter] public int Count { get; set; }
                private RenderFragment Row => @<tr><td>@Count</td></tr>;
                void Log() { @* a comment *@ var s = $"{Count}"; }
            }
            @{ @:Plain @Count text
            }
            """;
        Assert.Equal(
            Lf("""
            {K:@code} {K:{}
                [{T:Parameter}] {K:public} {K:int} Count { {K:get}; {K:set}; }
                {K:private} {T:RenderFragment} Row => {K:@}<{G:tr}><{G:td}>{K:@}Count</{G:td}></{G:tr}>;
                {K:void} {F:Log}() { {C:@* a comment *@} {K:var} s = {S:$"}{E:{}Count{E:}}{S:"}; }
            {K:}}
            {K:@{} {K:@:}Plain {K:@}Count text
            {K:}}
            """),
            Render(Lf(code), "razor"));
    }

    [Fact]
    public void Razor_multi_line_tags_and_comments()
    {
        Assert.Equal(
            "<{T:BzDialog} {A:Open}={S:\"}{K:@}_open{S:\"}\n           {A:OnClose}={S:\"Close\"}>\n{C:@* one}\n{C:   two *@}\n{C:<!-- html -->}",
            Render("<BzDialog Open=\"@_open\"\n           OnClose=\"Close\">\n@* one\n   two *@\n<!-- html -->", "razor"));
    }

    // ---- JSON

    [Fact]
    public void Json_keys_values_comments_and_escapes()
    {
        Assert.Equal(
            "{\n  {C:// settings}\n  {P:\"name\"}: {S:\"Shop\"}, {P:\"port\"}: {N:-8080}, {P:\"tls\"}: {L:true}, {P:\"proxy\"}: {L:null},\n  {P:\"path\"}: {S:\"C:}{E:\\\\}{S:x}{E:\\u00e9}{S:\"}, {P:bare}: [{N:1.5e3}]\n}",
            Render("{\n  // settings\n  \"name\": \"Shop\", \"port\": -8080, \"tls\": true, \"proxy\": null,\n  \"path\": \"C:\\\\x\\u00e9\", bare: [1.5e3]\n}", "json"));
        Assert.Equal("{C:/* a}\n{C:   b */} {N:1}", Render("/* a\n   b */ 1", "json"));
    }

    // ---- TypeScript / JavaScript

    [Fact]
    public void TypeScript_declarations_types_and_functions()
    {
        Assert.Equal(
            "{K:export} {K:interface} {T:Options}<{T:T} {K:extends} {T:Element}> { {K:readonly} size: {T:number}; }",
            Render("export interface Options<T extends Element> { readonly size: number; }", "typescript"));
        Assert.Equal(
            "{K:const} {L:MAX} = {N:3}; {K:const} map = {K:new} {T:Map}<{T:string}, {T:Item}>();",
            Render("const MAX = 3; const map = new Map<string, Item>();", "typescript"));
        Assert.Equal("{K:type} {T:Id} = {T:string}; event.type = {S:\"click\"};", Render("type Id = string; event.type = \"click\";", "typescript"));
        Assert.Equal("{F:@Component}({ selector: {S:'app'} })", Render("@Component({ selector: 'app' })", "typescript"));
        Assert.Equal("{K:import} { a } {K:from} {S:\"./a\"};", Render("import { a } from \"./a\";", "javascript"));
        Assert.Equal("{K:const} string = {S:\"js\"};", Render("const string = \"js\";", "javascript"));
    }

    [Fact]
    public void Script_templates_regexes_and_division()
    {
        Assert.Equal(
            "{S:`a }{E:${}b + {S:`}{E:${}c{E:}}{S:`}{E:}}{S: d`}",
            Render("`a ${b + `${c}`} d`", "javascript"));
        Assert.Equal("{K:const} r = {E:/[/]+\\d/gi}, half = total / {N:2};", Render("const r = /[/]+\\d/gi, half = total / 2;", "javascript"));
        Assert.Equal("{S:`line one}\n{S:line }{E:${}two{E:}}{S:`};", Render("`line one\nline ${two}`;", "javascript"));
    }

    // ---- HTML / XML

    [Fact]
    public void Html_tags_attributes_entities_and_embedded_languages()
    {
        Assert.Equal(
            "{M:<!DOCTYPE html>}\n<{G:a} {A:href}={S:\"/x\"} {A:data-id}={S:'1'} {A:hidden}>Tom {E:&amp;} Jerry{E:&#169;}</{G:a}>\n{C:<!-- note -->}",
            Render("<!DOCTYPE html>\n<a href=\"/x\" data-id='1' hidden>Tom &amp; Jerry&#169;</a>\n<!-- note -->", "html"));
        Assert.Equal(
            "<{G:style}>{G:p} { {P:color}: {N:#333}; }</{G:style}><{G:script}>{K:let} x = {N:1};</{G:script}>",
            Render("<style>p { color: #333; }</style><script>let x = 1;</script>", "html"));
        Assert.Equal(
            "<{G:script} {A:type}={S:\"application/json\"}>{ {P:\"a\"}: {N:1} }</{G:script}>",
            Render("<script type=\"application/json\">{ \"a\": 1 }</script>", "html"));
        Assert.Equal(
            "<{G:div}\n  {A:class}={S:\"a\"}>",
            Render("<div\n  class=\"a\">", "html"));
    }

    [Fact]
    public void Xml_prolog_cdata_and_no_embedded_languages()
    {
        Assert.Equal(
            "{M:<?xml version=\"1.0\"?>}\n<{G:Project} {A:Sdk}={S:\"Microsoft.NET.Sdk\"}>{S:<![CDATA[ <x> ]]>}<{G:script}>a < b</{G:script}></{G:Project}>",
            Render("<?xml version=\"1.0\"?>\n<Project Sdk=\"Microsoft.NET.Sdk\"><![CDATA[ <x> ]]><script>a < b</script></Project>", "xml"));
    }

    // ---- CSS

    [Fact]
    public void Css_selectors_declarations_and_at_rules()
    {
        Assert.Equal(
            "{G:a}{A:.btn}{K::hover}, [{A:data-state}={S:'open'}] > {A:#main}{K:::before} {\n  {P:margin}: {N:-1px} {F:calc}({N:100%} - {N:2rem}) {K:!important};\n  {V:--brand}: {F:var}({V:--primary});\n  {K:&:focus} { {P:color}: {N:#fff}; }\n}",
            Render("a.btn:hover, [data-state='open'] > #main::before {\n  margin: -1px calc(100% - 2rem) !important;\n  --brand: var(--primary);\n  &:focus { color: #fff; }\n}", "css"));
        Assert.Equal(
            "{K:@media} ({P:min-width}: {N:640px}) {K:and} ({P:prefers-reduced-motion}: no-preference) { }",
            Render("@media (min-width: 640px) and (prefers-reduced-motion: no-preference) { }", "css"));
        Assert.Equal(
            "{A:.x} {\n  {K:@apply} px-4 py-2\n    rounded-md;\n  {P:color}: red;\n}",
            Render(".x {\n  @apply px-4 py-2\n    rounded-md;\n  color: red;\n}", "css"));
    }

    // ---- YAML

    [Fact]
    public void Yaml_keys_scalars_anchors_and_block_scripts()
    {
        const string code = """
            # CI
            on: [push]
            env: &env
              DOTNET_NOLOGO: true
              RETRIES: 3
            steps:
              - name: 'Build: all'
                run: |
                  dotnet build -c Release
                with: { cache: npm, version: 22 }
                token: ${{ secrets.TOKEN }}
            ---
            """;
        Assert.Equal(
            Lf("""
            {C:# CI}
            {P:on}: [{S:push}]
            {P:env}: {V:&env}
              {P:DOTNET_NOLOGO}: {L:true}
              {P:RETRIES}: {N:3}
            {P:steps}:
              - {P:name}: {S:'Build: all'}
                {P:run}: {K:|}
                  {F:dotnet} build {A:-c} Release
                {P:with}: { {P:cache}: {S:npm}, {P:version}: {N:22} }
                {P:token}: {V:${{ secrets.TOKEN }}}
            {M:---}
            """),
            Render(Lf(code), "yaml"));
        Assert.Equal("{P:text}: {K:>}\n  {S:folded # kept}\n{P:next}: {S:x}", Render("text: >\n  folded # kept\nnext: x", "yaml"));
    }

    // ---- bash and console

    [Fact]
    public void Bash_commands_flags_variables_and_strings()
    {
        Assert.Equal(
            "{V:NAME}={S:\"x\"} {F:dotnet} test {A:--no-build} {A:-c} Release {N:2}>&{N:1} | {F:tee} {S:\"}{V:$HOME}{S:/log}{E:\\\"}{S:\"} {C:# run}",
            Render("NAME=\"x\" dotnet test --no-build -c Release 2>&1 | tee \"$HOME/log\\\"\" # run", "bash"));
        Assert.Equal(
            "{K:if} {K:[[} {A:-f} {V:${FILE}} {K:]]}; {K:then} {F:echo} {S:'no $expansion'}; {K:fi}",
            Render("if [[ -f ${FILE} ]]; then echo 'no $expansion'; fi", "bash"));
        Assert.Equal("{F:echo} url#frag", Render("echo url#frag", "bash"));
    }

    [Fact]
    public void Bash_heredocs_and_continuations()
    {
        Assert.Equal(
            "{F:cat} {M:<<'END'} > out\n{S:body $X}\n{M:END}\n{F:a} && {E:\\}\n  {F:b} {A:--x} {E:\\}\n  {A:--y}",
            Render("cat <<'END' > out\nbody $X\nEND\na && \\\n  b --x \\\n  --y", "bash"));
    }

    [Fact]
    public void Console_prompts_commands_and_output()
    {
        Assert.Equal(
            "{C:# Restore}\n{M:$ }{F:dotnet} restore\n  Restored Shop.csproj\n{M:PS C:\\src> }{F:Get-ChildItem} {A:-Recurse}\n{M:user@box:~/shop$ }{F:git} push {E:\\}\n{M:> }{A:--force}",
            Render("# Restore\n$ dotnet restore\n  Restored Shop.csproj\nPS C:\\src> Get-ChildItem -Recurse\nuser@box:~/shop$ git push \\\n> --force", "console"));
    }

    // ---- PowerShell

    [Fact]
    public void PowerShell_cmdlets_parameters_operators_variables_and_types()
    {
        Assert.Equal(
            "{V:$files} = {F:Get-ChildItem} {A:-Path} {S:\"}{V:$env:USERPROFILE}{S:\\src\"} | {F:Where-Object} { {V:$_}.Length {K:-gt} {N:1kb} }",
            Render("$files = Get-ChildItem -Path \"$env:USERPROFILE\\src\" | Where-Object { $_.Length -gt 1kb }", "powershell"));
        Assert.Equal(
            "[{T:Parameter}(Mandatory)][{T:string}[]] {V:$Name} = {L:$null}; [{T:System.IO.Path}]::{F:Combine}({S:'a'}, {S:'it''s'}); {V:$list}[{N:0}]",
            Render("[Parameter(Mandatory)][string[]] $Name = $null; [System.IO.Path]::Combine('a', 'it''s'); $list[0]", "powershell"));
        Assert.Equal("{F:dotnet} pack {A:--no-build} {E:`}\n  {A:-c} Release", Render("dotnet pack --no-build `\n  -c Release", "powershell"));
    }

    [Fact]
    public void PowerShell_comments_and_here_strings()
    {
        Assert.Equal(
            "{C:<# one}\n{C:two #>} {V:$x} = {S:@\"}\n{S:Hello }{V:$name}\n{S:\"@}\n{V:$y} = {N:1} {C:# done}",
            Render("<# one\ntwo #> $x = @\"\nHello $name\n\"@\n$y = 1 # done", "powershell"));
    }

    // ---- SQL

    [Fact]
    public void Sql_keywords_types_functions_variables_and_strings()
    {
        Assert.Equal(
            "{K:SELECT} {K:TOP} ({N:5}) c.Name, {F:COUNT}(*) {K:AS} Orders {K:FROM} dbo.Customers c {K:WHERE} c.Since >= {V:@since} {K:AND} c.Tier <> {S:N'it''s'} {K:AND} c.Note {K:IS} {K:NOT} {L:NULL} {C:-- top}",
            Render("SELECT TOP (5) c.Name, COUNT(*) AS Orders FROM dbo.Customers c WHERE c.Since >= @since AND c.Tier <> N'it''s' AND c.Note IS NOT NULL -- top", "sql"));
        Assert.Equal(
            "{K:create} {K:table} t (id {T:uuid}, total {T:numeric}({N:10}, {N:2}));\n{K:AS} {S:$$}\n{S:BEGIN RETURN 1; END;}\n{S:$$}; {K:WHERE} id = {V:$1}",
            Render("create table t (id uuid, total numeric(10, 2));\nAS $$\nBEGIN RETURN 1; END;\n$$; WHERE id = $1", "sql"));
    }

    // ---- diff

    [Fact]
    public void Diff_headers_hunks_and_lines_that_start_like_headers()
    {
        Assert.Equal(
            "{M:diff --git a/x b/x}\n{M:--- a/x}\n{M:+++ b/x}\n{F:@@ -1,3 +1,3 @@} Main\n keep\n{-:-old}\n{-:--- an old sql comment}\n{+:+new}\n{C:\\ No newline at end of file}",
            Render("diff --git a/x b/x\n--- a/x\n+++ b/x\n@@ -1,3 +1,3 @@ Main\n keep\n-old\n--- an old sql comment\n+new\n\\ No newline at end of file", "diff"));
    }

    // ---- HTTP

    [Fact]
    public void Http_requests_headers_variables_and_json_bodies()
    {
        Assert.Equal(
            "{V:@host} = {S:https://localhost:5001}\n\n{C:### Create}\n{K:POST} {V:{{host}}}{U:/api/orders} {M:HTTP/1.1}\n{P:Content-Type}: application/json\n\n{\n  {P:\"id\"}: {N:1}\n}",
            Render("@host = https://localhost:5001\n\n### Create\nPOST {{host}}/api/orders HTTP/1.1\nContent-Type: application/json\n\n{\n  \"id\": 1\n}", "http"));
    }

    // ---- Dockerfile

    [Fact]
    public void Dockerfile_instructions_stages_shell_and_exec_forms()
    {
        Assert.Equal(
            "{K:FROM} {S:mcr.microsoft.com/dotnet/sdk:10.0} {K:AS} {T:build}\n{K:ENV} {V:PORT}={S:8080}\n{K:RUN} {F:dotnet} restore && {E:\\}\n    {F:dotnet} publish {A:-o} /app\n{K:COPY} {A:--from}={S:build} /app .\n{K:ENTRYPOINT} [{S:\"dotnet\"}, {S:\"Shop.dll\"}]",
            Render("FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build\nENV PORT=8080\nRUN dotnet restore && \\\n    dotnet publish -o /app\nCOPY --from=build /app .\nENTRYPOINT [\"dotnet\", \"Shop.dll\"]", "dockerfile"));
    }

    // ---- INI / TOML

    [Fact]
    public void Ini_sections_keys_and_values()
    {
        Assert.Equal(
            "{C:# top}\n{P:root} = {L:true}\n{T:[*.cs]}\n{P:indent_size} = {N:4}\n{P:style} = {S:space}\n{C:; note}",
            Render("# top\nroot = true\n[*.cs]\nindent_size = 4\nstyle = space\n; note", "ini"));
    }

    [Fact]
    public void Toml_tables_values_and_multi_line_strings()
    {
        Assert.Equal(
            "{T:[[bin]]}\n{P:name} = {S:'app'} {C:# main}\n{P:env} = { {P:debug} = {L:false}, {P:level} = {N:2} }\n{P:notes} = {S:\"\"\"}\n{S:two\"\"\"}",
            Render("[[bin]]\nname = 'app' # main\nenv = { debug = false, level = 2 }\nnotes = \"\"\"\ntwo\"\"\"", "toml"));
    }

    // ---- Markdown

    [Fact]
    public void Markdown_blocks_inlines_and_fenced_code()
    {
        const string code = """
            # Title
            Use **bold**, *em*, `code` and [docs](https://x.y) or <https://x.y>; snake_case stays.
            - [x] done
            1. first
            > quoted
            ```csharp
            var x = 1;
            ```
            """;
        Assert.Equal(
            Lf("""
            {H:# Title}
            Use {B:**bold**}, {I:*em*}, {S:`code`} and {U:[docs]}{M:(https://x.y)} or {U:<https://x.y>}; snake_case stays.
            {K:- [x]} done
            {K:1. }first
            {C:> quoted}
            {M:```}{T:csharp}
            {K:var} x = {N:1};
            {M:```}
            """),
            Render(Lf(code), "markdown"));
        Assert.Equal("{M:---}\n{P:title}: {S:Docs}\n{M:---}\ntext", Render("---\ntitle: Docs\n---\ntext", "markdown"));
    }

    // ---- helpers

    public static TheoryData<string> LanguageNames()
    {
        var data = new TheoryData<string>();
        foreach (var language in SyntaxText.Languages) data.Add(language);
        return data;
    }

    public static TheoryData<string, string> Samples() => new()
    {
        { "csharp", "/// <summary>Doc</summary>\npublic sealed class A<T> : IB where T : notnull\n{\n    private readonly string _s = $@\"x{{y}} {Value,5:N2}\n    tail\";\n    var raw = $$\"\"\"\n      {{ name }} \"\"\";\n    /* open\n       close */ int n = 0x1F_FF;\n}" },
        { "razor", "@page \"/x\"\n<ul>\n@foreach (var i in Items)\n{\n    <li class=\"@i.Css\">@i.Name</li>\n}\n</ul>\n@code {\n    string S => $\"{1}\";\n}" },
        { "json", "{\n  \"a\": [1, 2.5e3, true, null],\n  /* c */ \"b\": \"x\\n\"\n}" },
        { "typescript", "const a = `x${`y${z}`}\nmore`;\nexport function f<T>(x: T): T { return /re/g.test(x) ? x : x / 2; }" },
        { "html", "<div class=\"a\"\n data-x='1'>\n<style>a { color: red; }\n</style><script>\nlet t = `a\nb`;\n</script></div>" },
        { "css", ".a {\n  @apply b\n    c;\n  color: red; /* note\n  more */\n}" },
        { "yaml", "a: |\n  line one\n  line two\nb: \"quoted\n  on two lines\"\nrun: |\n  echo \"hi\" \\\n    --x\nc: 1" },
        { "bash", "cat <<EOF\nbody\nEOF\necho \"multi\nline\" && \\\n  ls -la" },
        { "console", "$ git commit -m \"a\n> b\"\ndone\nPS> Write-Host `\n  hi" },
        { "powershell", "<# a\nb #>\n$h = @'\nliteral\n'@\n\"two\nlines\" | Out-Null" },
        { "sql", "SELECT 'a\nb' FROM t; /* c\nd */ $tag$\nbody\n$tag$" },
        { "diff", "@@ -1,2 +1,2 @@\n-a\n+b\n c\n--- x\n+++ y" },
        { "http", "GET https://x/y\nAccept: */*\n\n{\n  \"a\": \"b\"\n}\n###\nPOST https://x" },
        { "dockerfile", "FROM a AS b\nRUN echo \\\n  hi && \\\n  ls\nCMD [\"a\"]" },
        { "ini", "[a]\nb = 1\n; c" },
        { "toml", "a = \"\"\"\nb\n\"\"\"\n[t]\nc = [1,\n  2]" },
        { "markdown", "---\nt: 1\n---\n# H\n```razor\n@if (x) {\n  <p>@y</p>\n}\n```\ntext *em*" },
    };

    /// <summary>The kind letters of the notation: plain text is written as is.</summary>
    internal static char Letter(SyntaxKind kind) => kind switch
    {
        SyntaxKind.Comment => 'C',
        SyntaxKind.Keyword => 'K',
        SyntaxKind.String => 'S',
        SyntaxKind.Escape => 'E',
        SyntaxKind.Number => 'N',
        SyntaxKind.Constant => 'L',
        SyntaxKind.Type => 'T',
        SyntaxKind.Function => 'F',
        SyntaxKind.Property => 'P',
        SyntaxKind.Attribute => 'A',
        SyntaxKind.Tag => 'G',
        SyntaxKind.Variable => 'V',
        SyntaxKind.Meta => 'M',
        SyntaxKind.Inserted => '+',
        SyntaxKind.Deleted => '-',
        SyntaxKind.Heading => 'H',
        SyntaxKind.Emphasis => 'I',
        SyntaxKind.Strong => 'B',
        SyntaxKind.Link => 'U',
        _ => '?',
    };

    internal static string Render(string code, string language) => Render(SyntaxText.Tokenize(code, language));

    internal static string Render(IReadOnlyList<SyntaxLine> lines) =>
        string.Join("\n", lines.Select(line =>
            string.Concat(line.Runs.Select(r => r.Kind == SyntaxKind.Text ? r.Text : $"{{{Letter(r.Kind)}:{r.Text}}}"))));

    /// <summary>
    /// A raw string literal takes the line endings of its file, which a Windows clone checks out as
    /// CRLF, while <see cref="Render(IReadOnlyList{SyntaxLine})"/> joins lines with <c>\n</c>.
    /// Multi-line samples and expectations pass through this, so they read the same on every
    /// machine. CRLF input itself has its own test.
    /// </summary>
    private static string Lf(string text) => text.ReplaceLineEndings("\n");
}
