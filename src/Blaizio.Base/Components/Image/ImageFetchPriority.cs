namespace Blaizio;

/// <summary>Native <c>fetchpriority</c> attribute of the img element.</summary>
public enum ImageFetchPriority
{
    /// <summary>Let the browser decide (no attribute is emitted).</summary>
    Auto,

    /// <summary>Prioritise the request - for LCP/hero images (<c>fetchpriority="high"</c>).</summary>
    High,

    /// <summary>Deprioritise the request (<c>fetchpriority="low"</c>).</summary>
    Low
}
