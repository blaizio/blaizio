namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// C++ (<c>cpp</c>; <c>c++</c>, <c>cc</c>, <c>cxx</c>, <c>hpp</c>, <c>hxx</c>, <c>hh</c>, <c>ino</c>):
    /// C plus raw strings (<c>R"(...)"</c>), attributes (<c>[[nodiscard]]</c>), templates, common
    /// standard library types, and the names after <c>class</c>, <c>struct</c>, <c>namespace</c> and
    /// <c>typename</c>.
    /// </summary>
    public static SyntaxLanguage Cpp => field ??= new SyntaxGrammar("cpp", "c++", "cc", "cxx", "hpp", "hxx", "hh", "h++", "ino")
        .LineComment("//")
        .BlockComment("/*", "*/")
        .Line("#", SyntaxKind.Meta, lineStart: true)
        .String("\"")
        .String("\"", prefixes: "L u U u8")
        .String("\"(", ")\"", escape: '\0', multiline: true, prefixes: "R LR uR UR u8R")
        .String("[[", "]]", escape: '\0', kind: SyntaxKind.Meta)
        .Char()
        .Keywords("alignas alignof and and_eq asm auto bitand bitor break case catch class compl concept const consteval constexpr constinit const_cast continue co_await co_return co_yield decltype default delete do dynamic_cast else enum explicit export extern final for friend goto if import inline module mutable namespace new noexcept not not_eq operator or or_eq override private protected public register reinterpret_cast requires return sizeof static static_assert static_cast struct switch template this thread_local throw try typedef typeid typename union using virtual volatile while xor xor_eq")
        .Constants("true false nullptr NULL")
        .Types("bool char char8_t char16_t char32_t wchar_t short int long float double void signed unsigned size_t int8_t int16_t int32_t int64_t uint8_t uint16_t uint32_t uint64_t string string_view vector map unordered_map set array optional unique_ptr shared_ptr")
        .Declarations("class struct union enum namespace typename concept", SyntaxKind.Type)
        .Numbers()
        .Functions()
        .ConstantCase()
        .PascalCaseTypes();
}
