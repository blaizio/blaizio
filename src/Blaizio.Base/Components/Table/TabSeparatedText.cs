using System.Text;

namespace Blaizio;

/// <summary>
/// The tab-separated text spreadsheets put on the clipboard: one line per row, a tab between cells,
/// and a cell holding a tab, a line break or a quote wrapped in double quotes (a quote inside
/// written twice). Used to paste a block of cells into an editable table and to copy one out.
/// </summary>
public static class TabSeparatedText
{
    /// <summary>
    /// Splits clipboard text into rows of cells. A trailing line break (spreadsheets end the copy
    /// with one) does not add an empty row; text with no tab or line break is one row of one cell.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string? text)
    {
        var rows = new List<IReadOnlyList<string>>();
        if (string.IsNullOrEmpty(text)) return rows;

        var row = new List<string>();
        var cell = new StringBuilder();
        var i = 0;
        var atCellStart = true;

        while (i < text.Length)
        {
            var c = text[i];

            if (atCellStart && c == '"' && TryReadQuoted(text, ref i, cell))
            {
                atCellStart = false;
                continue;
            }
            atCellStart = false;

            switch (c)
            {
                case '\t':
                    row.Add(cell.ToString());
                    cell.Clear();
                    atCellStart = true;
                    i++;
                    break;
                case '\r' or '\n':
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = [];
                    atCellStart = true;
                    i += c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                    break;
                default:
                    cell.Append(c);
                    i++;
                    break;
            }
        }

        // The last line, unless the text ended on a line break (atCellStart with nothing pending).
        if (!atCellStart || row.Count > 0 || cell.Length > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>Writes rows of cells as tab-separated text, quoting cells that hold a tab, a line break or a quote.</summary>
    public static string Format(IEnumerable<IEnumerable<string?>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var builder = new StringBuilder();
        var firstRow = true;
        foreach (var row in rows)
        {
            if (!firstRow) builder.Append("\r\n");
            firstRow = false;
            var firstCell = true;
            foreach (var cell in row)
            {
                if (!firstCell) builder.Append('\t');
                firstCell = false;
                AppendCell(builder, cell ?? string.Empty);
            }
        }
        return builder.ToString();
    }

    private static void AppendCell(StringBuilder builder, string cell)
    {
        if (cell.AsSpan().IndexOfAny("\t\r\n\"") < 0)
        {
            builder.Append(cell);
            return;
        }
        builder.Append('"').Append(cell.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
    }

    // A cell opening with a quote: read to the closing quote (a doubled quote is a literal one). It
    // only counts as quoted when the closing quote ends the cell (tab, line break or end of text);
    // otherwise the quote was literal text and the caller re-reads the cell from the start.
    private static bool TryReadQuoted(string text, ref int index, StringBuilder cell)
    {
        var start = index;
        var j = index + 1;
        var content = new StringBuilder();
        while (j < text.Length)
        {
            if (text[j] == '"')
            {
                if (j + 1 < text.Length && text[j + 1] == '"')
                {
                    content.Append('"');
                    j += 2;
                    continue;
                }
                var after = j + 1;
                if (after == text.Length || text[after] is '\t' or '\r' or '\n')
                {
                    cell.Append(content);
                    index = after;
                    return true;
                }
                break;
            }
            content.Append(text[j]);
            j++;
        }
        index = start;
        return false;
    }
}
