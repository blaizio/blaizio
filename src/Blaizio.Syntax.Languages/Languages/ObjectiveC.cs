namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Objective-C (<c>objectivec</c>; <c>objc</c>, <c>objective-c</c>, <c>obj-c</c>): C plus
    /// <c>@"..."</c> strings, <c>@interface</c> and the other <c>@</c> keywords, property attributes,
    /// <c>YES</c> and <c>NO</c>, and class names by their capital letter.
    /// </summary>
    public static SyntaxLanguage ObjectiveC => field ??= new SyntaxGrammar("objectivec", "objc", "objective-c", "obj-c")
        .LineComment("//")
        .BlockComment("/*", "*/")
        .Line("#", SyntaxKind.Meta, lineStart: true)
        .String("@\"", "\"")
        .String("\"")
        .Char()
        .Sigil('@', SyntaxKind.Keyword)
        .Keywords("auto break case const continue default do else enum extern for goto if inline register restrict return sizeof static struct switch typedef union volatile while self super nonatomic atomic strong weak copy assign retain readonly readwrite nullable nonnull __block __weak __strong in out inout bycopy byref oneway")
        .Constants("YES NO nil Nil NULL true false")
        .Types("char short int long float double void signed unsigned id instancetype BOOL SEL IMP Class NSInteger NSUInteger CGFloat")
        .Numbers()
        .Functions()
        .PascalCaseTypes();
}
