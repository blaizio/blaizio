using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// Base class for every Blaizio headless primitive.
/// </summary>
/// <remarks>
/// Deliberately carries <b>no</b> styling concern - there is no Tailwind, no
/// <c>TailwindMerge</c>, no default CSS class. A primitive's job is to render the correct
/// element with the correct ARIA roles/states and a <c>data-state</c> contract; the styled
/// layer (copied into the consumer via the Blaizio CLI) is what dresses those data-attributes.
///
/// <para>
/// <see cref="Class"/> / <see cref="Style"/> and any extra splatted attributes
/// (<see cref="Attributes"/>) are passed straight through to the rendered element, merged via
/// <see cref="PropBuilder"/> so consumer values compose with - rather than clobber - the
/// primitive's own attributes.
/// </para>
/// </remarks>
public abstract class BzComponentBase : ComponentBase
{
    [Inject] private IServiceProvider Services { get; set; } = default!;

    private BlaizioOptions? _options;

    /// <summary>
    /// The app-wide defaults configured in <c>AddBlaizio(options => ...)</c> (built-in defaults when
    /// nothing is registered). Component parameters, when set, always override these.
    /// </summary>
    protected BlaizioOptions Options => _options ??= BlaizioOptions.Resolve(Services);

    /// <summary>
    /// The ambient reading direction supplied by an ancestor <see cref="BaseDirectionProvider"/>.
    /// Prefer <see cref="ResolvedDirection"/> when reading.
    /// </summary>
    [CascadingParameter(Name = BaseDirectionProvider.CascadeName)]
    protected Direction? CascadedDirection { get; set; }

    /// <summary>CSS classes to apply to the rendered element. Concatenated with the primitive's own (if any).</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline styles to apply to the rendered element. Concatenated with the primitive's own (if any).</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>
    /// Explicit reading direction for this subtree, overriding the cascaded value. Usually left unset.
    /// Equivalent to putting a <c>dir</c> attribute on the element - it reaches the DOM (see
    /// <see cref="DirAttribute"/>) so CSS logical properties and direction-aware behaviour follow it.
    /// </summary>
    [Parameter] public Direction? Dir { get; set; }

    /// <summary>
    /// Any attribute the consumer puts on the component that the primitive doesn't otherwise handle
    /// (e.g. <c>id</c>, <c>data-*</c>, extra event handlers). Splatted onto the rendered element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? Attributes { get; set; }

    /// <summary>
    /// The effective direction: explicit <see cref="Dir"/> ⇒ cascaded ⇒ the app-wide
    /// <see cref="BlaizioOptions.Direction"/> default (<see cref="Direction.Ltr"/> out of the box).
    /// </summary>
    protected Direction ResolvedDirection => Dir ?? CascadedDirection ?? Options.Direction;

    /// <summary>
    /// The <c>dir</c> attribute value to splat onto the rendered element when <see cref="Dir"/> is set
    /// explicitly (else <see langword="null"/>, so the element inherits the ambient direction from an
    /// ancestor <see cref="BaseDirectionProvider"/> / <c>&lt;html dir&gt;</c> rather than pinning one).
    /// Emitting it is what makes the per-component override actually reach CSS + DOM-reading behaviour.
    /// </summary>
    protected string? DirAttribute => Dir is { } d ? d.ToAttribute() : null;

    /// <summary>
    /// The <c>dir</c> attribute for a floating surface that portals to <c>&lt;body&gt;</c>: a
    /// body-level node escapes a subtree-scoped <see cref="BaseDirectionProvider"/>'s DOM ancestry,
    /// so the CASCADED direction must be stamped onto the element itself (explicit <see cref="Dir"/>
    /// still wins). <see langword="null"/> when neither is set - the element then inherits from
    /// <c>&lt;html dir&gt;</c>, which is also where an app-wide direction lives.
    /// </summary>
    protected string? PortalDirAttribute => (Dir ?? CascadedDirection) is { } d ? d.ToAttribute() : null;
}
