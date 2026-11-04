namespace Blaizio;

/// <summary>
/// The languages of Blaizio.Syntax.Languages, one property each, added where the app configures
/// Blaizio:
/// <code>
/// builder.Services.AddBlaizio(options => options.Syntax.Add(SyntaxLanguages.Python, SyntaxLanguages.Rust));
/// </code>
/// or all of them with <see cref="AddAll"/>. Each is a <see cref="SyntaxGrammar"/> (comments,
/// strings, keywords, numbers, and type and function names by their shape), built the first time
/// its property is read. A trimmed WebAssembly publish keeps only the languages the app adds.
/// </summary>
public static partial class SyntaxLanguages
{
    /// <summary>Every language here: general purpose first, then .NET and functional, config and infrastructure, and the rest.</summary>
    public static IReadOnlyList<SyntaxLanguage> All =>
    [
        Python, Java, Go, Rust, C, Cpp, Kotlin, Swift, Php, Ruby, Dart, Lua, Scala,
        FSharp, VisualBasic, Haskell, Elixir, Clojure,
        GraphQL, Protobuf, Hcl, Bicep, Makefile, Nginx, CMake, Batch,
        R, Perl, Julia, ObjectiveC, LaTeX,
    ];

    /// <summary>Adds every language here to the app's languages: <c>options.Syntax.AddAll()</c>.</summary>
    public static SyntaxOptions AddAll(this SyntaxOptions syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        return syntax.Add(All);
    }
}
