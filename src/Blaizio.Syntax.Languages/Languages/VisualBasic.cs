namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Visual Basic .NET (<c>vbnet</c>; <c>vb</c>, <c>visualbasic</c>), in any case: <c>'</c> and
    /// <c>REM</c> comments, strings with doubled quotes, interpolated strings (<c>$"{x}"</c>),
    /// <c>#Region</c> and other directives, and the names after <c>Sub</c>, <c>Function</c>,
    /// <c>Class</c> and <c>As</c>.
    /// </summary>
    public static SyntaxLanguage VisualBasic => field ??= new SyntaxGrammar("vbnet", "vb", "visualbasic")
        .IgnoreCase()
        .LineComment("'")
        .LineComment("REM")
        .Line("#", SyntaxKind.Meta, lineStart: true)
        .String("$\"", "\"", escape: '"', interpolation: SyntaxInterpolation.Braces)
        .String("\"", escape: '"')
        .Keywords("AddHandler AddressOf Alias And AndAlso As ByRef ByVal Call Case Catch Class Const Continue Declare Default Delegate Dim DirectCast Do Each Else ElseIf End Enum Erase Error Event Exit Finally For Friend Function Get GetType Global GoTo Handles If Implements Imports In Inherits Interface Is IsNot Let Lib Like Loop Me Mod Module MustInherit MustOverride MyBase MyClass NameOf Namespace Narrowing New Next Not NotInheritable NotOverridable Of On Operator Option Optional Or OrElse Overloads Overridable Overrides ParamArray Partial Private Property Protected Public RaiseEvent ReadOnly ReDim RemoveHandler Resume Return Select Set Shadows Shared Static Step Stop Structure Sub SyncLock Then Throw To Try TryCast TypeOf Using When While Widening With WithEvents WriteOnly Xor Async Await Iterator Yield")
        .Constants("True False Nothing")
        .Types("Boolean Byte Char Date Decimal Double Integer Long Object SByte Short Single String UInteger ULong UShort")
        .Declarations("Sub Function", SyntaxKind.Function)
        .Declarations("Class Structure Module Interface Enum Inherits Implements As New", SyntaxKind.Type)
        .Numbers()
        .Functions()
        .PascalCaseTypes();
}
