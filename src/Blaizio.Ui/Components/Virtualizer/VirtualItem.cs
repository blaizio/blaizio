namespace Blaizio.Ui;

/// <summary>
/// The context a <see cref="BzVirtualizer{TItem}"/> hands its item template for one mounted item: the
/// data <see cref="Item"/>, its <see cref="Index"/> in the full list, and the <see cref="Attributes"/>
/// to splat on the item's root element. Those attributes carry the <c>data-bz-virtual-index</c> marker
/// the dynamic measurement reads and, in fixed mode, the item height that keeps the spacer math exact -
/// so always spread them: <c>@attributes="context.Attributes"</c>.
/// </summary>
/// <typeparam name="TItem">The item data type.</typeparam>
/// <param name="Item">The data item for this row.</param>
/// <param name="Index">The item's zero-based index within the full list.</param>
/// <param name="Attributes">Attributes to splat on the item's root element.</param>
public readonly record struct VirtualItem<TItem>(
    TItem Item,
    int Index,
    IReadOnlyDictionary<string, object> Attributes);
