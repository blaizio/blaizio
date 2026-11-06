using Blaizio;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>The clipboard text spreadsheets copy: tabs between cells, lines between rows, quotes around the awkward cells.</summary>
public class TabSeparatedTextTests
{
    private static string[][] Parse(string text) =>
        [.. TabSeparatedText.Parse(text).Select(row => row.ToArray())];

    [Fact]
    public void A_single_value_is_one_cell()
    {
        Assert.Equal([["42"]], Parse("42"));
    }

    [Fact]
    public void The_trailing_line_break_a_spreadsheet_adds_is_not_a_row()
    {
        Assert.Equal([["42"]], Parse("42\r\n"));
        Assert.Equal([["a", "b"], ["c", "d"]], Parse("a\tb\r\nc\td\r\n"));
        Assert.Equal([["a"], ["b"]], Parse("a\nb\n"));
    }

    [Fact]
    public void Empty_cells_and_inner_empty_rows_survive()
    {
        Assert.Equal([["a", "", "c"]], Parse("a\t\tc"));
        Assert.Equal([["a", ""]], Parse("a\t"));
        Assert.Equal([["a"], [""], ["b"]], Parse("a\r\n\r\nb"));
    }

    [Fact]
    public void Quoted_cells_hold_tabs_line_breaks_and_doubled_quotes()
    {
        Assert.Equal([["line one\nline two", "x"]], Parse("\"line one\nline two\"\tx"));
        Assert.Equal([["say \"hi\"", "tab\there"]], Parse("\"say \"\"hi\"\"\"\t\"tab\there\""));
    }

    [Fact]
    public void A_quote_that_does_not_close_the_cell_is_literal_text()
    {
        Assert.Equal([["5\" screen"]], Parse("5\" screen"));
        Assert.Equal([["\"open", "b"]], Parse("\"open\tb"));
    }

    [Fact]
    public void Nothing_parses_to_no_rows()
    {
        Assert.Empty(TabSeparatedText.Parse(""));
        Assert.Empty(TabSeparatedText.Parse(null));
    }

    [Fact]
    public void Format_quotes_only_the_cells_that_need_it_and_round_trips()
    {
        string?[][] rows = [["plain", "tab\tin", null], ["quote \"q\"", "two\nlines", "x"]];
        var text = TabSeparatedText.Format(rows);

        Assert.Equal("plain\t\"tab\tin\"\t\r\n\"quote \"\"q\"\"\"\t\"two\nlines\"\tx", text);
        Assert.Equal([["plain", "tab\tin", ""], ["quote \"q\"", "two\nlines", "x"]], Parse(text));
    }
}
