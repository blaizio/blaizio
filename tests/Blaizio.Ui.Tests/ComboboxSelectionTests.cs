using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// BzCombobox maps the engine's string selection back to items. With server-side search Items
/// holds only the current matches, so an earlier pick is not in it any more.
/// </summary>
public class ComboboxSelectionTests : BunitContext
{
    public ComboboxSelectionTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private sealed record Person(int Id, string Name);

    [Fact]
    public async Task A_pick_survives_Items_being_replaced_by_the_next_search()
    {
        var alice = new Person(1, "Alice");
        var bob = new Person(2, "Bob");
        IReadOnlyList<Person> picked = [];
        var onChange = EventCallback.Factory.Create<IReadOnlyList<Person>>(this, v => picked = v);

        var cut = Render<BzCombobox<Person>>(p => p
            .Add(x => x.Items, [alice])
            .Add(x => x.ItemToStringValue, person => person.Name)
            .Add(x => x.ShouldFilter, false)
            .Add(x => x.Values, picked)
            .Add(x => x.ValuesChanged, onChange));
        var engine = cut.FindComponent<BaseCombobox>();

        await cut.InvokeAsync(() => engine.Instance.ValuesChanged.InvokeAsync(["Alice"]));
        Assert.Equal([alice], picked);

        // The next query returns Bob only; Alice is selected but no longer among Items.
        cut.Render(p => p.Add(x => x.Items, [bob]).Add(x => x.Values, picked));
        await cut.InvokeAsync(() => engine.Instance.ValuesChanged.InvokeAsync(["Alice", "Bob"]));

        Assert.Equal([alice, bob], picked);
    }

    [Fact]
    public void An_item_without_a_string_key_does_not_break_rendering()
    {
        var nameless = new Person(3, null!);

        var cut = Render<BzCombobox<Person>>(p => p
            .Add(x => x.Items, [nameless])
            .Add(x => x.ItemToStringValue, person => person.Name)
            .Add(x => x.DefaultValue, nameless));

        Assert.NotNull(cut.FindComponent<BaseCombobox>());
    }

    [Fact]
    public async Task A_value_that_matches_nothing_is_dropped_not_sent_as_a_default()
    {
        IReadOnlyList<int> picked = [];
        var cut = Render<BzCombobox<int>>(p => p
            .Add(x => x.Items, [1, 2])
            .Add(x => x.Values, picked)
            .Add(x => x.ValuesChanged, EventCallback.Factory.Create<IReadOnlyList<int>>(this, v => picked = v)));

        await cut.InvokeAsync(() => cut.FindComponent<BaseCombobox>().Instance.ValuesChanged.InvokeAsync(["2", "99"]));

        Assert.Equal([2], picked); // 99 is not an item: no stray 0
    }
}
