namespace Blaizio;

/// <summary>
/// What a carousel's <c>SlideAriaLabel</c> receives: a slide's place among the slides, for a name
/// worded in the reader's language.
/// </summary>
public sealed class CarouselSlidePosition
{
    internal CarouselSlidePosition(int position, int count)
    {
        Position = position;
        Count = count;
    }

    /// <summary>The slide's place, counting from 1.</summary>
    public int Position { get; }

    /// <summary>How many slides the carousel has.</summary>
    public int Count { get; }
}
