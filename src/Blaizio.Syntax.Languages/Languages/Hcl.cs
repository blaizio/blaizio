namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// HCL and Terraform (<c>hcl</c>; <c>terraform</c>, <c>tf</c>, <c>tfvars</c>): block types,
    /// <c>name =</c> attributes, interpolation (<c>${var.name}</c>), heredocs (<c>&lt;&lt;EOF</c>),
    /// type constraints, and function calls.
    /// </summary>
    public static SyntaxLanguage Hcl => field ??= new SyntaxGrammar("hcl", "terraform", "tf", "tfvars")
        .LineComment("#")
        .LineComment("//")
        .BlockComment("/*", "*/")
        .String("\"", interpolation: SyntaxInterpolation.DollarBraces)
        .Heredoc("<<-")
        .Heredoc("<<")
        .Keys("=", lineStart: true)
        .Keywords("resource data variable output locals module provider terraform backend required_providers provisioner connection lifecycle dynamic content moved import check removed for in if else endif endfor")
        .Constants("true false null")
        .Types("string number bool list map set object tuple any")
        .Numbers()
        .Functions();
}
