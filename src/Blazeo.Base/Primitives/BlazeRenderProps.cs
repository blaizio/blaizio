namespace Blazeo;

/// <summary>
/// The bag of merged attributes a primitive hands to an <c>AsChild</c> render fragment so the
/// consumer can splat them onto their <i>own</i> element — Blazeo's faithful equivalent of Radix's
/// <c>asChild</c> / Base UI's <c>render</c> prop.
/// </summary>
/// <example>
/// <code>
/// &lt;BlazeToggle&gt;
///     &lt;AsChild Context="props"&gt;
///         &lt;button @attributes="props.Attributes"&gt;Bold&lt;/button&gt;
///     &lt;/AsChild&gt;
/// &lt;/BlazeToggle&gt;
/// </code>
/// </example>
public sealed class BlazeRenderProps
{
    internal static readonly IReadOnlyDictionary<string, object> EmptyAttributes =
        new Dictionary<string, object>(0);

    public BlazeRenderProps(IReadOnlyDictionary<string, object> attributes) => Attributes = attributes;

    /// <summary>The behaviour/ARIA/<c>data-*</c> attributes (and event handlers) to splat onto your element.</summary>
    public IReadOnlyDictionary<string, object> Attributes { get; }
}
