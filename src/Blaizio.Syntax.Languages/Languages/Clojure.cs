namespace Blaizio;

public static partial class SyntaxLanguages
{
    /// <summary>
    /// Clojure (<c>clojure</c>; <c>clj</c>, <c>cljs</c>, <c>cljc</c>, <c>edn</c>): <c>:keywords</c>,
    /// names with <c>-</c>, <c>?</c> and <c>!</c> in them, regex literals (<c>#"..."</c>), type hints
    /// (<c>^String</c>), special forms and common core functions, and the names after
    /// <c>defn</c>, <c>def</c> and the like.
    /// </summary>
    public static SyntaxLanguage Clojure => field ??= new SyntaxGrammar("clojure", "clj", "cljs", "cljc", "edn")
        .LineComment(";")
        .String("#\"", "\"", kind: SyntaxKind.Escape)
        .String("\"", multiline: true)
        .IdentifierChars("-?!*+<>=/.'")
        .Sigil(':', SyntaxKind.Constant, "-?!*+<>=/.")
        .Sigil('^', SyntaxKind.Type, "-.")
        .Keywords("def defn defn- defmacro defmulti defmethod defprotocol defrecord deftype defonce fn fn* let letfn loop recur if if-not if-let if-some when when-not when-let when-some cond condp case do doto quote var throw try catch finally ns require import use and or not new set! binding -> ->> as-> some-> some->> for doseq dotimes while")
        .Constants("true false nil")
        .Words("println print str map filter reduce first rest cons conj assoc dissoc get get-in update update-in merge keys vals count apply partial comp range into vec atom swap! reset! deref", SyntaxKind.Function)
        .Declarations("def defn defn- defmacro defmulti defmethod defprotocol defrecord deftype defonce", SyntaxKind.Function)
        .Numbers();
}
