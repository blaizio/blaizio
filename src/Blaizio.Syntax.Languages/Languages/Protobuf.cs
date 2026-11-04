namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Protocol Buffers (<c>protobuf</c>; <c>proto</c>): scalar types, field options, and the names
    /// after <c>message</c>, <c>enum</c>, <c>service</c> and <c>rpc</c>.
    /// </summary>
    public static SyntaxLanguage Protobuf => field ??= new SyntaxGrammar("protobuf", "proto")
        .LineComment("//")
        .BlockComment("/*", "*/")
        .String("\"")
        .String("'")
        .Keywords("syntax edition package import option message enum service rpc returns stream oneof map reserved repeated optional required extend extensions to max public weak group")
        .Constants("true false")
        .Types("double float int32 int64 uint32 uint64 sint32 sint64 fixed32 fixed64 sfixed32 sfixed64 bool string bytes")
        .Declarations("message enum service extend", SyntaxKind.Type)
        .Declarations("rpc", SyntaxKind.Function)
        .Numbers()
        .PascalCaseTypes();
}
