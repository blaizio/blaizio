namespace Blaizio;

/// <summary>
/// One language's tokenizer, and the extension point for languages Blaizio does not ship: derive
/// from it, or describe a simple language with <see cref="SyntaxGrammar"/>, and add it in
/// <c>AddBlaizio(options => options.Syntax.Add(...))</c>. A language with a built-in's name
/// replaces the built-in.
/// </summary>
/// <remarks>
/// <para><see cref="Tokenize"/> reads one line, or a range of one, from the state the previous line
/// ended in, reports its runs to the <see cref="SyntaxWriter"/>, and returns the state this line
/// ends in. The contract:</para>
/// <list type="bullet">
/// <item><description><b>States are immutable and compare by value</b>: records, strings, boxed
/// enums or numbers, with <see langword="null"/> for "nothing open". <see cref="SyntaxDocument"/>
/// reuses a line whose text and starting state are unchanged, so a state that changes after it is
/// returned, or two equal states that do not compare equal, breaks that reuse.</description></item>
/// <item><description><b>The scanner always advances</b>: every step consumes at least one
/// character, so it finishes on any input.</description></item>
/// <item><description><b>Ranges come in order</b>, inside <c>start..end</c>. The writer clips a range
/// that overlaps the one before it or runs past the end, and reads a gap as plain text, so the runs
/// always join back into the line.</description></item>
/// <item><description><b>A scanner that throws</b> gets its line rendered as plain text (or, hosted
/// inside another language, its part of the line), and its state reset to <see langword="null"/>.
/// The built-in scanners never throw; the fallback is for bugs, not for control flow.</description></item>
/// </list>
/// <para>One instance serves every document and thread: keep what belongs to a line in the state,
/// never in fields.</para>
/// </remarks>
public abstract class SyntaxLanguage
{
    /// <summary>The canonical name, matched in any case (<c>"python"</c>).</summary>
    public abstract string Name { get; }

    /// <summary>Other names it answers to (<c>["py", "py3"]</c>). None by default.</summary>
    public virtual IReadOnlyList<string> Aliases => [];

    /// <summary>
    /// Tokenizes <c>line[start..end]</c> from <paramref name="state"/>, reporting runs to
    /// <paramref name="writer"/>, and returns the state at <paramref name="end"/>.
    /// </summary>
    /// <param name="line">The whole line, without its line break.</param>
    /// <param name="start">Where this language's part of the line starts: 0, unless another language hosts it.</param>
    /// <param name="end">Where its part ends: the line's length, unless another language hosts it.</param>
    /// <param name="state">What the previous line ended in; <see langword="null"/> on the first line and whenever nothing is open.</param>
    /// <param name="writer">Collects the runs, and finds the languages a line can host (<see cref="SyntaxWriter.Find"/>).</param>
    public abstract object? Tokenize(string line, int start, int end, object? state, SyntaxWriter writer);

    // Called when the language is added to an app's set: a grammar compiles its tables there, so a
    // mistake in it shows at startup, and it cannot change once in use.
    internal virtual void Seal()
    {
    }
}
