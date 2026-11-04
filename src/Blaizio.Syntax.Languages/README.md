# Blaizio.Syntax.Languages

More languages for [Blaizio.Syntax](https://www.nuget.org/packages/Blaizio.Syntax) and the Blaizio
Code Block, each a compact `SyntaxGrammar` table:

- General purpose: Python, Java, Go, Rust, C, C++, Kotlin, Swift, PHP, Ruby, Dart, Lua, Scala
- .NET and functional: F#, VB.NET, Haskell, Elixir, Clojure
- Config and infrastructure: GraphQL, Protocol Buffers, HCL (Terraform), Bicep, Makefile, nginx,
  CMake, batch files
- More: R, Perl, Julia, Objective-C, LaTeX

Add the ones you need where the app configures Blaizio. A trimmed WebAssembly publish keeps only
those:

```csharp
builder.Services.AddBlaizio(options => options.Syntax.Add(SyntaxLanguages.Python, SyntaxLanguages.Rust));
```

or all of them:

```csharp
builder.Services.AddBlaizio(options => options.Syntax.AddAll());
```

Docs: <https://blaiz.io/docs/components/code-block>.
