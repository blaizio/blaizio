using System.Reflection;
using Xunit;
using static Blaizio.Syntax.Tests.SyntaxTextTests;

namespace Blaizio.Syntax.Tests;

/// <summary>
/// Blaizio.Syntax.Languages: the set itself (every name and alias reaches its language, and no
/// static constructor ties the languages together, so trimming can drop the ones an app does not
/// add), one sample per language with its expected tokens, and the grammar guarantees run against
/// every language.
/// </summary>
public class SyntaxLanguagesTests
{
    private static readonly SyntaxOptions Syntax = new BlaizioOptions().Syntax.AddAll();

    [Fact]
    public void All_holds_the_thirty_one_languages_once_each()
    {
        var names = SyntaxLanguages.All.Select(l => l.Name).ToList();

        Assert.Equal(31, names.Count);
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal([.. SyntaxText.Languages, .. names], Syntax.Languages);
    }

    [Fact]
    public void Every_name_and_alias_finds_its_own_language()
    {
        foreach (var language in SyntaxLanguages.All)
        {
            Assert.Same(language, Syntax.Find(language.Name));
            foreach (var alias in language.Aliases) Assert.Same(language, Syntax.Find(alias));
        }
    }

    [Fact]
    public void Each_language_is_built_once()
    {
        Assert.Same(SyntaxLanguages.Python, SyntaxLanguages.Python);
        Assert.Same(SyntaxLanguages.All[0], SyntaxLanguages.Python);
    }

    [Fact]
    public void No_static_constructor_ties_the_languages_together()
    {
        // A static constructor would build every grammar the moment one is read, and keep every
        // one of them through trimming. Each property builds its own table instead.
        Assert.Null(typeof(SyntaxLanguages).TypeInitializer);
        Assert.All(
            typeof(SyntaxLanguages).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
            f => Assert.False(f.IsInitOnly && !f.Name.Contains("k__BackingField", StringComparison.Ordinal), f.Name));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void A_sample_tokenizes_as_expected(string language, string code, string expected)
    {
        Assert.Equal(expected, Render(Syntax.Tokenize(code, language)));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void A_language_never_throws_and_its_runs_join_back(string name)
    {
        var language = Syntax.Find(name)!;
        var random = new Random(name.GetHashCode(StringComparison.Ordinal) ^ 31);
        const string alphabet = "ab_ 09.\"'`$#@%{}()[]<>/*\\!?:=-~;|\t\n\u00e9";
        for (var round = 0; round < 150; round++)
        {
            var chars = new char[random.Next(0, 60)];
            for (var i = 0; i < chars.Length; i++) chars[i] = alphabet[random.Next(alphabet.Length)];
            object? state = null;
            foreach (var line in new string(chars).Split('\n'))
            {
                var writer = new SyntaxWriter(Syntax.Registry);
                writer.Reset(line);
                state = language.Tokenize(line, 0, line.Length, state, writer);
                Assert.Equal(line, string.Concat(writer.Finish().Select(r => r.Text)));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Streamed_tokenizing_matches_whole_tokenizing(string language, string code, string expected)
    {
        var doc = new SyntaxDocument(language, Syntax);
        var lines = code.Split('\n');
        for (var i = 1; i <= lines.Length; i++) doc.Update(string.Join('\n', lines, 0, i));

        Assert.Equal(expected, Render(doc.Lines));
    }

    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (var language in SyntaxLanguages.All) data.Add(language.Name);
        return data;
    }

    public static TheoryData<string, string, string> Samples() => new()
    {
        {
            "python",
            "#!/usr/bin/env python3\n@app.route(\"/\")\ndef total(xs) -> int:\n    return sum(x for x in xs if x is not None)  # ok\ns = rb'\\d' + f\"{x!r:>8} {{y}}\" + '''doc\nmore'''",
            "{M:#!/usr/bin/env python3}\n{M:@app.route}({S:\"/\"})\n{K:def} {F:total}(xs) -> {T:int}:\n    {K:return} {F:sum}(x {K:for} x {K:in} xs {K:if} x {K:is} {K:not} {L:None})  {C:# ok}\ns = {S:rb'\\d'} + {S:f\"}{E:{}x!r:>{N:8}{E:}}{S: }{E:{{}{S:y}{E:}}}{S:\"} + {S:'''doc}\n{S:more'''}"
        },
        {
            "java",
            "@Override\npublic List<String> names(int max) throws IOException {\n    var s = \"\"\"\n        text\"\"\"; char c = '\\n'; return new ArrayList<>(MAX_SIZE);\n}",
            "{M:@Override}\n{K:public} {T:List}<{T:String}> {F:names}({T:int} max) {K:throws} {T:IOException} {\n    {K:var} s = {S:\"\"\"}\n{S:        text\"\"\"}; {T:char} c = {S:'}{E:\\n}{S:'}; {K:return} {K:new} {T:ArrayList}<>({L:MAX_SIZE});\n}"
        },
        {
            "go",
            "func (r *Repo) Find(id int) (*Order, error) {\n    s := `raw\nline`; return nil, fmt.Errorf(\"no %d\", id)\n}",
            "{K:func} (r *{T:Repo}) {F:Find}(id {T:int}) (*{T:Order}, {T:error}) {\n    s := {S:`raw}\n{S:line`}; {K:return} {L:nil}, fmt.{F:Errorf}({S:\"no %d\"}, id)\n}"
        },
        {
            "rust",
            "#[derive(Debug)]\nfn longest<'a>(x: &'a str) -> Option<u32> { let r = r#\"raw \"q\"\"#; println!(\"{}\", 1u8); Some(0xFF_u32) }",
            "{M:#[derive(Debug)]}\n{K:fn} {F:longest}<{V:'a}>(x: &{V:'a} {T:str}) -> {T:Option}<{T:u32}> { {K:let} r = {S:r#\"raw \"q\"\"#}; {F:println!}({S:\"{}\"}, {N:1u8}); {T:Some}({N:0xFF_u32}) }"
        },
        {
            "c",
            "#include <stdio.h>\ntypedef struct point { int x; } point_t;\nint main(void) { char c = 'a'; printf(\"%d\\n\", MAX); return 0; }",
            "{M:#include <stdio.h>}\n{K:typedef} {K:struct} {T:point} { {T:int} x; } point_t;\n{T:int} {F:main}({T:void}) { {T:char} c = {S:'a'}; {F:printf}({S:\"%d}{E:\\n}{S:\"}, {L:MAX}); {K:return} {N:0}; }"
        },
        {
            "cpp",
            "template <typename T> class Box {};\n[[nodiscard]] auto f() noexcept { return R\"(raw \\n)\"; }\nstd::vector<int> v{1, 2}; nullptr;",
            "{K:template} <{K:typename} {T:T}> {K:class} {T:Box} {};\n{M:[[nodiscard]]} {K:auto} {F:f}() {K:noexcept} { {K:return} {S:R\"(raw \\n)\"}; }\nstd::{T:vector}<{T:int}> v{{N:1}, {N:2}}; {L:nullptr};"
        },
        {
            "kotlin",
            "@JvmStatic\nfun greet(name: String?): String = \"Hi $name, ${name?.length ?: 0}\"\nval raw = \"\"\"a $b\nc\"\"\"",
            "{M:@JvmStatic}\n{K:fun} {F:greet}(name: {T:String}?): {T:String} = {S:\"Hi }{V:$name}{S:, }{E:${}name?.length ?: {N:0}{E:}}{S:\"}\n{K:val} raw = {S:\"\"\"a }{V:$b}\n{S:c\"\"\"}"
        },
        {
            "swift",
            "@MainActor\nfunc greet(_ name: String) -> String { \"Hi \\(name.uppercased())\" }\n#if DEBUG\nlet x: Int? = nil\n#endif",
            "{M:@MainActor}\n{K:func} {F:greet}(_ name: {T:String}) -> {T:String} { {S:\"Hi }{E:\\(}name.{F:uppercased}(){E:)}{S:\"} }\n{K:#if} {L:DEBUG}\n{K:let} x: {T:Int}? = {L:nil}\n{K:#endif}"
        },
        {
            "php",
            "<?php\n#[Attr]\nfunction total(array $items): float { return array_sum($items); } // sum\n$s = \"Hi $name\"; $t = <<<EOT\nbody\nEOT;",
            "{M:<?php}\n{M:#[Attr]}\n{K:function} {F:total}({K:array} {V:$items}): {T:float} { {K:return} {F:array_sum}({V:$items}); } {C:// sum}\n{V:$s} = {S:\"Hi }{V:$name}{S:\"}; {V:$t} = {S:<<<EOT}\n{S:body}\n{S:EOT};"
        },
        {
            "ruby",
            "class Order < Base\n  attr_reader :id\n  def total? = items.sum { |i| i[:price] }\nend\nputs \"#{@id}: #{$count}\", key: :value",
            "{K:class} {T:Order} < {T:Base}\n  {K:attr_reader} {L::id}\n  {K:def} {F:total?} = items.sum { |i| i[{L::price}] }\n{K:end}\n{F:puts} {S:\"}{E:#{}{V:@id}{E:}}{S:: }{E:#{}{V:$count}{E:}}{S:\"}, {L:key}: {L::value}"
        },
        {
            "dart",
            "@override\nWidget build(BuildContext context) => Text('$count ${items.length}', key: const Key(r'\\n'));",
            "{M:@override}\n{T:Widget} {F:build}({T:BuildContext} context) => {F:Text}({S:'}{V:$count}{S: }{E:${}items.length{E:}}{S:'}, key: {K:const} {F:Key}({S:r'\\n'}));"
        },
        {
            "lua",
            "--[[ block\ncomment ]]\nlocal function f(t) return #t end -- len\nlocal s = [[long\nstring]]",
            "{C:--[[ block}\n{C:comment ]]}\n{K:local} {K:function} {F:f}(t) {K:return} #t {K:end} {C:-- len}\n{K:local} s = {S:[[long}\n{S:string]]}"
        },
        {
            "scala",
            "case class Order(id: Long)\ndef total(xs: Seq[Int]): Int = xs.sum\nval s = s\"total ${total(xs)} for $id\"",
            "{K:case} {K:class} {T:Order}(id: {T:Long})\n{K:def} {F:total}(xs: {T:Seq}[{T:Int}]): {T:Int} = xs.sum\n{K:val} s = {S:s\"total }{E:${}{F:total}(xs){E:}}{S: for }{V:$id}{S:\"}"
        },
        {
            "fsharp",
            "[<EntryPoint>]\nlet main argv =\n    (* comment (* nested *) *) let f (x: 'T) = x\n    printfn $\"{argv.Length} args\"; List.fold (*) 1 [1; 2]",
            "{M:[<EntryPoint>]}\n{K:let} main argv =\n    {C:(* comment (* nested *) *)} {K:let} f (x: {T:'T}) = x\n    {F:printfn} {S:$\"}{E:{}argv.{T:Length}{E:}}{S: args\"}; {T:List}.fold (*) {N:1} [{N:1}; {N:2}]"
        },
        {
            "vbnet",
            "Public Function Total(items As List(Of Integer)) As Integer\n    Dim s = \"say \"\"hi\"\"\" ' comment\n    REM another\nEnd Function",
            "{K:Public} {K:Function} {F:Total}(items {K:As} {T:List}({K:Of} {T:Integer})) {K:As} {T:Integer}\n    {K:Dim} s = {S:\"say }{E:\"\"}{S:hi}{E:\"\"}{S:\"} {C:' comment}\n    {C:REM another}\n{K:End} {K:Function}"
        },
        {
            "haskell",
            "{-# LANGUAGE GADTs #-}\nmain :: IO ()\nmain = putStrLn \"hi\" -- greet\n{- block {- nested -} -} x' = foldl' (+) 0 [1, 2]",
            "{M:{-# LANGUAGE GADTs #-}}\n{F:main} :: {T:IO} ()\nmain = putStrLn {S:\"hi\"} {C:-- greet}\n{C:{- block {- nested -} -}} x' = foldl' (+) {N:0} [{N:1}, {N:2}]"
        },
        {
            "elixir",
            "defmodule Shop do\n  @doc \"Hi\"\n  def greet(name), do: \"Hi #{name}\"\n  defp ok?(x), do: x == :ok\nend",
            "{K:defmodule} {T:Shop} {K:do}\n  {M:@doc} {S:\"Hi\"}\n  {K:def} {F:greet}(name), {L:do}: {S:\"Hi }{E:#{}name{E:}}{S:\"}\n  {K:defp} {F:ok?}(x), {L:do}: x == {L::ok}\n{K:end}"
        },
        {
            "clojure",
            "(defn greet [name]\n  ; say hi\n  (str \"Hi \" name)) (:keys m) #\"\\d+\"",
            "({K:defn} {F:greet} [name]\n  {C:; say hi}\n  ({F:str} {S:\"Hi \"} name)) ({L::keys} m) {E:#\"\\d+\"}"
        },
        {
            "graphql",
            "query Get($id: ID!) {\n  user(id: $id) @skip(if: false) { name }\n}\n\"\"\"Block\ndoc\"\"\"",
            "{K:query} {F:Get}({V:$id}: {T:ID}!) {\n  user({P:id}: {V:$id}) {M:@skip}({P:if}: {L:false}) { name }\n}\n{S:\"\"\"Block}\n{S:doc\"\"\"}"
        },
        {
            "protobuf",
            "syntax = \"proto3\";\nmessage Order { int64 id = 1; repeated Line lines = 2; }\nservice Shop { rpc Get(Req) returns (Order); }",
            "{K:syntax} = {S:\"proto3\"};\n{K:message} {T:Order} { {T:int64} id = {N:1}; {K:repeated} {T:Line} lines = {N:2}; }\n{K:service} {T:Shop} { {K:rpc} {F:Get}({T:Req}) {K:returns} ({T:Order}); }"
        },
        {
            "hcl",
            "resource \"aws_s3_bucket\" \"b\" {\n  bucket = \"logs-${var.env}\"\n  count  = length(var.names) # n\n}",
            "{K:resource} {S:\"aws_s3_bucket\"} {S:\"b\"} {\n  {P:bucket} = {S:\"logs-}{E:${}var.env{E:}}{S:\"}\n  {P:count}  = {F:length}(var.names) {C:# n}\n}"
        },
        {
            "bicep",
            "@minLength(3)\nparam name string = 'st${uniqueString(resourceGroup().id)}'\nresource sa 'Microsoft.Storage/storageAccounts@2023-05-01' = {\n  name: name\n}",
            "{M:@minLength}({N:3})\n{K:param} {V:name} {T:string} = {S:'st}{E:${}{F:uniqueString}({F:resourceGroup}().id){E:}}{S:'}\n{K:resource} {V:sa} {S:'Microsoft.Storage/storageAccounts@2023-05-01'} = {\n  {P:name}: name\n}"
        },
        {
            "makefile",
            "CC ?= gcc\nall: main.o\n\t$(CC) -o $@ $^ # link\n.PHONY: all",
            "{V:CC} ?= gcc\n{F:all}: main.o\n\t{V:$(CC)} -o {V:$@} {V:$^} {C:# link}\n{K:.PHONY}: all"
        },
        {
            "nginx",
            "server {\n    listen 80;\n    set $root /var/www; # docroot\n    return 301 https://$host$request_uri;\n}",
            "{K:server} {\n    {P:listen} {N:80};\n    {K:set} {V:$root} /var/www; {C:# docroot}\n    {K:return} {N:301} https://{V:$host$request_uri};\n}"
        },
        {
            "cmake",
            "#[[ block ]]\nset(SOURCES main.c util.c)\nif(WIN32)\n  target_link_libraries(app PRIVATE ${LIBS}) # libs\nendif()",
            "{C:#[[ block ]]}\n{F:set}({L:SOURCES} main.c util.c)\n{K:if}({L:WIN32})\n  {F:target_link_libraries}(app {L:PRIVATE} {V:${LIBS}}) {C:# libs}\n{K:endif}()"
        },
        {
            "batch",
            "@echo off\nREM build\nset /a n=%n%+1\n:loop\nif !n! gtr 3 goto :eof",
            "@{K:echo} {K:off}\n{C:REM build}\n{K:set} /a n={V:%n%}+{N:1}\n{F::loop}\n{K:if} {V:!n!} {K:gtr} {N:3} {K:goto} :eof"
        },
        {
            "r",
            "library(ggplot2)\nmean_pos <- function(x) mean(x[x > 0 & !is.na(x)])  # mean\nprint(TRUE); NA_real_",
            "{K:library}(ggplot2)\nmean_pos <- {K:function}(x) {F:mean}(x[x > {N:0} & !{F:is.na}(x)])  {C:# mean}\n{F:print}({L:TRUE}); {L:NA_real_}"
        },
        {
            "perl",
            "my @items = qw(a b);\nmy %seen;\nsub count { my ($x) = @_; return scalar @items; } # n\nprint \"Got $x items\\n\";",
            "{K:my} {V:@items} = {K:qw}(a b);\n{K:my} {V:%seen};\n{K:sub} {F:count} { {K:my} ({V:$x}) = {V:@_}; {K:return} scalar {V:@items}; } {C:# n}\n{K:print} {S:\"Got }{V:$x}{S: items}{E:\\n}{S:\"};"
        },
        {
            "julia",
            "#= block =# function area(r::Float64)\n    return pi * r^2  # sq\nend\n@time area(2.0); push!(xs, :sym)",
            "{C:#= block =#} {K:function} {F:area}(r::{T:Float64})\n    {K:return} {L:pi} * r^{N:2}  {C:# sq}\n{K:end}\n{M:@time} {F:area}({N:2.0}); {F:push!}(xs, {L::sym})"
        },
        {
            "objectivec",
            "#import <Foundation/Foundation.h>\n@interface Shop : NSObject\n@property (nonatomic) NSInteger count;\n@end\nNSString *s = @\"hi\"; BOOL ok = YES;",
            "{M:#import <Foundation/Foundation.h>}\n{K:@interface} {T:Shop} : {T:NSObject}\n{K:@property} ({K:nonatomic}) {T:NSInteger} count;\n{K:@end}\n{T:NSString} *s = {S:@\"hi\"}; {T:BOOL} ok = {L:YES};"
        },
        {
            "latex",
            "\\section{Intro} % note\nCost: 50\\% of \\$10, $x^2$ and\n\\[ y = 1 \\]",
            "{K:\\section}{Intro} {C:% note}\nCost: 50{E:\\%} of {E:\\$}10, {S:$x^2$} and\n{S:\\[ y = 1 \\]}"
        },
    };
}
