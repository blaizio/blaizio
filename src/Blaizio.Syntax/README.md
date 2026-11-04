# Blaizio.Syntax

Syntax highlighting for Blazor, in C#. `SyntaxText` splits code into runs of one kind (keyword,
string, type, function, comment, ...) per line, and `SyntaxDocument` keeps each line's tokens so
code that grows or changes re-tokenizes only the lines that did. There is no script: highlighting
looks the same in static rendering, on Blazor Server, in WebAssembly and while prerendering.

19 languages are built in: C#, Razor, JSON, bash, console sessions, PowerShell, TypeScript,
JavaScript, HTML, XML, CSS, SQL, YAML, diff, HTTP request files, Dockerfile, INI, TOML and Markdown.

## More languages

Add them where the app configures Blaizio:

```csharp
builder.Services.AddBlaizio(options => options.Syntax
    .Add(SyntaxLanguages.Python, SyntaxLanguages.Go)   // Blaizio.Syntax.Languages
    .Alias("py3", "python"));
```

or describe one with `SyntaxGrammar`:

```csharp
options.Syntax.Add(new SyntaxGrammar("gleam", "gl")
    .LineComment("//")
    .String("\"")
    .Keywords("as assert case const fn if import let opaque panic pub todo type use")
    .Constants("True False Nil")
    .Declarations("fn", SyntaxKind.Function)
    .Numbers()
    .Functions()
    .PascalCaseTypes());
```

For anything a table cannot say, derive from `SyntaxLanguage` and write the scanner yourself.

The Blaizio Code Block (`blaizio add code-block`) renders the runs in your theme's colors. Docs:
<https://blaiz.io/docs/components/code-block>.
