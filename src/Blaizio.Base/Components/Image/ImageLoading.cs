namespace Blaizio;

/// <summary>Native <c>loading</c> attribute of the img element.</summary>
public enum ImageLoading
{
    /// <summary>Load immediately (the browser default; no attribute is emitted).</summary>
    Eager,

    /// <summary>Defer the request until the image nears the viewport (<c>loading="lazy"</c>).</summary>
    Lazy
}
