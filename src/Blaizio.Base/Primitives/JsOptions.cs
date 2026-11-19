using System.Diagnostics.CodeAnalysis;

namespace Blaizio;

/// <summary>
/// Base of every options object handed to a script module. Derive as
/// <c>sealed class XOptions : JsOptions&lt;XOptions&gt;</c> with public properties; they reach the
/// script camel-cased (<c>SideOffset</c> arrives as <c>sideOffset</c>).
/// </summary>
/// <remarks>
/// Interop arguments are serialized by reflection over the object's runtime type, which the trimmer
/// cannot see: in a trimmed app a property nothing reads from C# loses its getter, and the script
/// receives an object without it, silently. The annotation on <typeparamref name="TSelf"/> states
/// the requirement where the trimmer does see it: every public property of the derived type is
/// kept wherever the type itself is. An anonymous object (<c>new { ... }</c>) has no such
/// guarantee, so none is passed to script from this assembly (a test enforces it).
/// </remarks>
/// <typeparam name="TSelf">The deriving options type itself.</typeparam>
internal abstract class JsOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TSelf>
    where TSelf : JsOptions<TSelf>;

/// <summary>Options of <c>createPositioning</c> and <c>createItemAlignedPositioning</c> (positioning.ts).</summary>
internal sealed class PositioningOptions : JsOptions<PositioningOptions>
{
    public required string Side { get; init; }
    public required string Align { get; init; }
    public double SideOffset { get; init; }
    public double AlignOffset { get; init; }
    public double CollisionPadding { get; init; } = 8;

    /// <summary>Render in place instead of moving the surface to the body.</summary>
    public bool Inline { get; init; }
}

/// <summary>Options of <c>createDismissableLayer</c> (dismissableLayer.ts).</summary>
internal sealed class DismissableLayerOptions : JsOptions<DismissableLayerOptions>
{
    /// <summary>The trigger or anchor: a press on it is not "outside", it toggles itself.</summary>
    public string? AnchorSelector { get; init; }

    /// <summary>Also dismiss on Escape. Menus and lists close on Escape and Tab in their own key handler and pass false.</summary>
    public bool DismissOnEscape { get; init; }

    /// <summary>Dismiss on a press outside. A dialog passes false: its overlay judges that press.</summary>
    public bool DismissOnOutsidePress { get; init; } = true;

    /// <summary>Also dismiss when anything outside the surface scrolls.</summary>
    public bool DismissOnScroll { get; init; }
}

/// <summary>Options of <c>createMenu</c> (menu.ts).</summary>
internal sealed class MenuOptions : JsOptions<MenuOptions>
{
    /// <summary><c>none</c>, <c>first</c>, <c>last</c> or <c>selected</c>.</summary>
    public required string InitialFocus { get; init; }
    public int TypeaheadTimeout { get; init; } = 1000;
    public bool Loop { get; init; }
    public bool IsSubmenu { get; init; }
    public required string TriggerSelector { get; init; }
}

/// <summary>Options of <c>createFocusScope</c> (focusScope.ts).</summary>
internal sealed class FocusScopeOptions : JsOptions<FocusScopeOptions>
{
    public bool Loop { get; init; }
    public bool Trapped { get; init; }
    public bool HasMountAutoFocus { get; init; }
    public bool HasUnmountAutoFocus { get; init; }

    /// <summary>Claim the scope stack and nothing else: no focus on first render, none restored at the end.</summary>
    public bool Passive { get; init; }
}

/// <summary>Options of <c>createRovingFocus</c> (rovingFocus.ts).</summary>
internal sealed class RovingFocusOptions : JsOptions<RovingFocusOptions>
{
    public required string Orientation { get; init; }
    public required string Dir { get; init; }
    public bool Loop { get; init; }
    public bool SelectOnFocus { get; init; }
}

/// <summary>Options of <c>createCarousel</c> (carousel.ts).</summary>
internal sealed class CarouselOptions : JsOptions<CarouselOptions>
{
    public bool Horizontal { get; init; }
    public bool Loop { get; init; }
    public int AutoplayMs { get; init; }
}

/// <summary>Options of <c>createCombobox</c> (combobox.ts).</summary>
internal sealed class ComboboxListOptions : JsOptions<ComboboxListOptions>
{
    public required string InputId { get; init; }
    public bool Loop { get; init; }
    public bool AutoHighlight { get; init; }
}

/// <summary>Options of <c>createCommand</c> (command.ts).</summary>
internal sealed class CommandListOptions : JsOptions<CommandListOptions>
{
    public required string InputId { get; init; }
    public bool Loop { get; init; }
}

/// <summary>Options of <c>guardNumeric</c> (inputNumber.ts): which characters the field lets through.</summary>
internal sealed class NumericGuardOptions : JsOptions<NumericGuardOptions>
{
    public bool Decimal { get; init; }
    public bool Negative { get; init; }
    public required string Separator { get; init; }
}

/// <summary>Options of <c>createInputTags</c> (inputTags.ts).</summary>
internal sealed class InputTagsOptions : JsOptions<InputTagsOptions>
{
    public required IReadOnlyList<string> Delimiters { get; init; }
}

/// <summary>Options of <c>createMessageScroller</c> (messageScroller.ts).</summary>
internal sealed class MessageScrollerOptions : JsOptions<MessageScrollerOptions>
{
    public bool AutoScroll { get; init; }

    /// <summary><c>start</c> or <c>end</c>.</summary>
    public required string DefaultPosition { get; init; }
    public int AnchorPeek { get; init; }
    public int FollowMargin { get; init; }
}

/// <summary>Options of <c>createSortable</c> (sortable.ts).</summary>
internal sealed class SortableOptions : JsOptions<SortableOptions>
{
    public required string Id { get; init; }
    public required string Strategy { get; init; }
    public required string Orientation { get; init; }
    public string? Group { get; init; }
    public double AnimationMs { get; init; }
    public bool Disabled { get; init; }
    public bool Sort { get; init; }
    public bool TransferOut { get; init; }
    public bool TransferIn { get; init; }
    public double DelayMs { get; init; }
    public bool LockAxis { get; init; }
    public required string SwapMode { get; init; }
    public bool Placeholder { get; init; }
    public string? PlaceholderClass { get; init; }
    public required string Animation { get; init; }
}

/// <summary>Options of <c>createTree</c> (tree.ts).</summary>
internal sealed class TreeOptions : JsOptions<TreeOptions>
{
    public required string Id { get; init; }
    public string? Group { get; init; }
    public bool Disabled { get; init; }
    public bool DragEnabled { get; init; }
    public double DelayMs { get; init; }
    public double HoverExpandMs { get; init; }
    public bool TransferIn { get; init; }
    public bool TransferOut { get; init; }
    public string? IndicatorClass { get; init; }
}

/// <summary>What the tree instance's <c>update</c> takes (tree.ts): the two options that follow their parameters.</summary>
internal sealed class TreeUpdateOptions : JsOptions<TreeUpdateOptions>
{
    public bool Disabled { get; init; }
    public bool DragEnabled { get; init; }
}
