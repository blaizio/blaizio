namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// C (<c>c</c>; <c>h</c>): preprocessor lines, character literals, wide and UTF strings, the
    /// standard types, ALL_CAPS macros as constants, and the names after <c>struct</c>,
    /// <c>union</c> and <c>enum</c>.
    /// </summary>
    public static SyntaxLanguage C => field ??= new SyntaxGrammar("c", "h")
        .LineComment("//")
        .BlockComment("/*", "*/")
        .Line("#", SyntaxKind.Meta, lineStart: true)
        .String("\"")
        .String("\"", prefixes: "L u U u8")
        .Char()
        .Keywords("auto break case const continue default do else enum extern for goto if inline register restrict return sizeof static struct switch typedef union volatile while _Alignas _Alignof _Atomic _Generic _Noreturn _Static_assert _Thread_local alignas alignof constexpr static_assert thread_local typeof typeof_unqual")
        .Constants("true false NULL nullptr EOF stdin stdout stderr")
        .Types("char short int long float double void signed unsigned bool _Bool _Complex size_t ssize_t ptrdiff_t intptr_t uintptr_t int8_t int16_t int32_t int64_t uint8_t uint16_t uint32_t uint64_t wchar_t FILE")
        .Declarations("struct union enum", SyntaxKind.Type)
        .Numbers()
        .Functions()
        .ConstantCase();
}
