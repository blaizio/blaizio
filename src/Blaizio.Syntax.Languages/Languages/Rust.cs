namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Rust (<c>rust</c>; <c>rs</c>): raw and byte strings (<c>r#"..."#</c>), character literals,
    /// lifetimes, attributes (<c>#[derive(Debug)]</c>), macros (<c>println!</c>), nested block
    /// comments, and the names after <c>fn</c>, <c>struct</c>, <c>enum</c> and <c>trait</c>.
    /// </summary>
    public static SyntaxLanguage Rust => field ??= new SyntaxGrammar("rust", "rs")
        .LineComment("//")
        .BlockComment("/*", "*/", nested: true)
        .String("\"", multiline: true)
        .String("\"", multiline: true, prefixes: "b c")
        .String("\"", escape: '\0', multiline: true, prefixes: "r br cr")
        .String("#\"", "\"#", escape: '\0', multiline: true, prefixes: "r br cr")
        .String("##\"", "\"##", escape: '\0', multiline: true, prefixes: "r br cr")
        .String("#![", "]", escape: '\0', kind: SyntaxKind.Meta)
        .String("#[", "]", escape: '\0', kind: SyntaxKind.Meta)
        .Char()
        .Sigil('\'', SyntaxKind.Variable)
        .Keywords("as async await break const continue crate dyn else enum extern fn for if impl in let loop match mod move mut pub ref return self Self static struct super trait type unsafe use where while yield union macro_rules")
        .Constants("true false None")
        .Words("Some Ok Err", SyntaxKind.Type)
        .Types("i8 i16 i32 i64 i128 isize u8 u16 u32 u64 u128 usize f32 f64 bool char str")
        .Declarations("fn", SyntaxKind.Function)
        .Declarations("struct enum trait type union", SyntaxKind.Type)
        .Numbers()
        .Functions(macro: '!')
        .ConstantCase()
        .PascalCaseTypes();
}
