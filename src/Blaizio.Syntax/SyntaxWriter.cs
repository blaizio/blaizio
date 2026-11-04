namespace Blaizio;

/// <summary>
/// Collects one line's runs for a <see cref="SyntaxLanguage"/>: a scanner reports ranges with a
/// kind, adjacent ranges of the same kind merge into one run, and any gap reads as plain text - so
/// the runs always join back into the line. It also finds the languages a line can host, from the
/// same set the document was created with.
/// </summary>
public sealed class SyntaxWriter
{
    private readonly List<SyntaxRun> _runs = [];
    private readonly SyntaxRegistry _registry;
    private string _line = "";
    private int _start;
    private int _end;
    private int _limit;
    private SyntaxKind _kind;

    internal SyntaxWriter(SyntaxRegistry registry) => _registry = registry;

    internal void Reset(string line)
    {
        _line = line;
        _runs.Clear();
        _start = _end = 0;
        _limit = line.Length;
        _kind = SyntaxKind.Text;
    }

    /// <summary>
    /// Reports <c>line[start..end]</c> as <paramref name="kind"/>. Ranges come in order: the part of
    /// a range before the end of the previous one, or past the end of the range being tokenized, is
    /// dropped, and a gap before a range reads as plain text.
    /// </summary>
    public void Add(int start, int end, SyntaxKind kind)
    {
        if (start < _end) start = _end;
        if (end > _limit) end = _limit;
        if (end <= start) return;
        if (start > _end) Add(_end, start, SyntaxKind.Text);
        if (kind == _kind)
        {
            _end = end;
            return;
        }
        Flush();
        _start = start;
        _end = end;
        _kind = kind;
    }

    /// <summary>
    /// The language <paramref name="language"/> names here - built in, or added by the app, so a
    /// language the app replaced is the replacement - or <see langword="null"/> when there is none.
    /// For one language hosting another: a fence's info string, a script element's body.
    /// </summary>
    public SyntaxLanguage? Find(string? language) => _registry.Find(language);

    /// <summary>
    /// Tokenizes <c>line[start..end]</c> of the current line as <paramref name="language"/>, from
    /// <paramref name="state"/>, and returns its end state: one language hosted inside another.
    /// Its ranges stay inside <c>start..end</c>, and if it throws, that part of the line reads plain
    /// and the returned state is <see langword="null"/> - the host carries on.
    /// </summary>
    public object? Embed(SyntaxLanguage language, int start, int end, object? state)
    {
        ArgumentNullException.ThrowIfNull(language);
        var limit = _limit;
        if (end > limit) end = limit;
        if (start > end) start = end;
        var runs = _runs.Count;
        var (runStart, runEnd, runKind) = (_start, _end, _kind);
        _limit = end;
        try
        {
            return language.Tokenize(_line, start, end, state, this);
        }
        catch (Exception)
        {
            // Back out whatever the language wrote before it failed, then read its range plain.
            _runs.RemoveRange(runs, _runs.Count - runs);
            (_start, _end, _kind) = (runStart, runEnd, runKind);
            Add(start, end, SyntaxKind.Text);
            return null;
        }
        finally
        {
            _limit = limit;
        }
    }

    internal SyntaxRun[] Finish()
    {
        if (_end < _line.Length) Add(_end, _line.Length, SyntaxKind.Text);
        Flush();
        return _runs.Count == 0 ? [] : [.. _runs];
    }

    private void Flush()
    {
        if (_end > _start)
            _runs.Add(new SyntaxRun(_start == 0 && _end == _line.Length ? _line : _line[_start.._end], _kind));
        _start = _end;
    }
}
