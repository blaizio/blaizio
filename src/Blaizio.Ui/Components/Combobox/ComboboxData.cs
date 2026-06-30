namespace Blaizio.Ui;

/// <summary>
/// Per-item-type data a generic <see cref="BzCombobox{TItem}"/> cascades to its strongly-typed parts
/// (list, item, collection, value, chip). The headless <c>BaseCombobox</c> works in plain strings; this
/// carries the two functions that bridge your <typeparamref name="TItem"/> to those string keys - one to
/// turn an item into its filter/selection value, and one to look an item back up from a value (so a chip
/// or the value display can recover the original item from the engine's string selection).
/// </summary>
/// <typeparam name="TItem">The item type (a string, or any object with an <c>ItemToStringValue</c>).</typeparam>
/// <param name="Items">The full list of items, as given to the root.</param>
/// <param name="ItemToStringValue">Maps an item to the string the engine filters + selects on.</param>
/// <param name="FindItem">Recovers the item for a selected string value, or <see langword="default"/> if none matches.</param>
public sealed record ComboboxData<TItem>(
    IReadOnlyList<TItem> Items,
    Func<TItem, string> ItemToStringValue,
    Func<string, TItem?> FindItem);
