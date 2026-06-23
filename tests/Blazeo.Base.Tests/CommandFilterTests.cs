using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Pure tests for the default command filter: case-insensitive substring or fuzzy subsequence over the
/// value and keywords, with an empty search matching everything.
/// </summary>
public class CommandFilterTests
{
    [Theory]
    [InlineData("", "Calendar", true)]        // empty search matches everything
    [InlineData("   ", "Calendar", true)]     // whitespace-only is treated as empty
    [InlineData("cal", "Calendar", true)]     // leading substring
    [InlineData("dar", "Calendar", true)]     // substring in the middle
    [InlineData("CAL", "Calendar", true)]     // case-insensitive
    [InlineData("clr", "Calculator", true)]   // fuzzy subsequence (c-l-r)
    [InlineData("xyz", "Calendar", false)]    // no match
    [InlineData("cax", "Calendar", false)]    // subsequence breaks on the last char
    public void Matches_value(string search, string value, bool expected) =>
        Assert.Equal(expected, CommandFilter.Matches(search, value, null));

    [Fact]
    public void Matches_a_keyword_when_the_value_misses()
    {
        Assert.True(CommandFilter.Matches("email", "Send mail", new[] { "email", "compose" }));
        Assert.True(CommandFilter.Matches("compose", "Send mail", new[] { "email", "compose" }));
    }

    [Fact]
    public void No_match_in_value_or_keywords_fails()
    {
        Assert.False(CommandFilter.Matches("zzz", "Send mail", new[] { "email", "compose" }));
        Assert.False(CommandFilter.Matches("zzz", "Send mail", null));
    }
}
