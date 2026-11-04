namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Haskell (<c>haskell</c>; <c>hs</c>): nested <c>{- -}</c> comments, pragmas
    /// (<c>{-# LANGUAGE ... #-}</c>), character literals, primed names (<c>foldl'</c>), types and
    /// constructors by their capital letter, and the name a type signature declares (<c>main ::</c>).
    /// </summary>
    public static SyntaxLanguage Haskell => field ??= new SyntaxGrammar("haskell", "hs")
        .LineComment("--")
        .String("{-#", "#-}", escape: '\0', multiline: true, kind: SyntaxKind.Meta)
        .BlockComment("{-", "-}", nested: true)
        .String("\"")
        .Char()
        .IdentifierChars("'")
        .Keys("::", SyntaxKind.Function, lineStart: true)
        .Keywords("as case class data default deriving do else family forall foreign hiding if import in infix infixl infixr instance let mdo module newtype of pattern proc qualified rec then type where")
        .Constants("True False otherwise")
        .Numbers()
        .PascalCaseTypes();
}
