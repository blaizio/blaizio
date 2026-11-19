using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The tags input from the keyboard and its chips: what commits a tag, what is dropped, the limit,
/// removal, and the read-only and disabled fields. The field is bound both ways, as
/// <c>@bind-Values</c> and <c>@bind-Search</c> do, except where a test leaves it to itself. The
/// script that keeps Enter from submitting a form is exercised in a browser.
/// </summary>
public class InputTagsRenderTests : BunitContext
{
    public InputTagsRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // What a styled layer does with the cascaded state: one chip per tag, each with its remove button.
    private sealed class Chips : ComponentBase
    {
        [CascadingParameter] public InputTagsContext? Tags { get; set; }
        [Parameter] public RenderFragment<BzRenderProps>? RemoveAs { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (Tags is null) return;
            for (var i = 0; i < Tags.Values.Count; i++)
            {
                var value = Tags.Values[i];
                builder.OpenComponent<BaseInputTagsChip>(0);
                builder.SetKey(i + ":" + value);
                builder.AddComponentParameter(1, nameof(BaseInputTagsChip.Index), i);
                builder.AddComponentParameter(2, nameof(BaseInputTagsChip.Value), value);
                builder.AddComponentParameter(3, nameof(BaseInputTagsChip.ChildContent), (RenderFragment)(chip =>
                {
                    chip.AddContent(0, value);
                    chip.OpenComponent<BaseInputTagsChipRemove>(1);
                    chip.AddComponentParameter(2, nameof(BaseInputTagsChipRemove.RenderAs), RemoveAs);
                    chip.CloseComponent();
                }));
                builder.CloseComponent();
            }
        }
    }

    private static readonly RenderFragment Parts = builder =>
    {
        builder.OpenComponent<Chips>(0);
        builder.CloseComponent();
        builder.OpenComponent<BaseInputTagsInput>(1);
        builder.AddComponentParameter(2, nameof(BaseInputTagsInput.Placeholder), "Add a tag");
        builder.CloseComponent();
    };

    // A form field holding the tags and the text being typed, bound both ways.
    private sealed class Field : ComponentBase
    {
        [Parameter] public IReadOnlyList<string> Initial { get; set; } = [];
        [Parameter] public IReadOnlyDictionary<string, object>? Settings { get; set; }

        public IReadOnlyList<string> Values { get; private set; } = [];
        public string? Search { get; private set; } = "";
        public int ValueChanges { get; private set; }

        protected override void OnInitialized() => Values = Initial;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<BaseInputTags>(0);
            builder.AddComponentParameter(1, nameof(BaseInputTags.Values), Values);
            builder.AddComponentParameter(2, nameof(BaseInputTags.ValuesChanged),
                EventCallback.Factory.Create<IReadOnlyList<string>>(this, values => { Values = values; ValueChanges++; }));
            builder.AddComponentParameter(3, nameof(BaseInputTags.Search), Search);
            builder.AddComponentParameter(4, nameof(BaseInputTags.SearchChanged),
                EventCallback.Factory.Create<string?>(this, text => Search = text));
            if (Settings is not null) builder.AddMultipleAttributes(5, Settings);
            builder.AddComponentParameter(6, nameof(BaseInputTags.ChildContent), Parts);
            builder.CloseComponent();
        }
    }

    private IRenderedComponent<Field> RenderField(string[]? tags = null, params (string Name, object Value)[] settings) =>
        Render<Field>(p => p
            .Add(x => x.Initial, tags ?? [])
            .Add(x => x.Settings, settings.ToDictionary(s => s.Name, s => s.Value)));

    private static IElement Input(IRenderedComponent<Field> cut) => cut.Find("input[data-bz-input-tags-input]");

    private static string[] Tags(IRenderedComponent<Field> cut) =>
        [.. cut.FindAll("[data-bz-input-tags-chip]").Select(chip => chip.GetAttribute("data-value")!)];

    private static void Type(IRenderedComponent<Field> cut, string text, string key)
    {
        Input(cut).Input(text);
        Input(cut).KeyDown(key);
    }

    [Fact]
    public void Enter_turns_the_typed_text_into_a_tag_and_empties_the_box()
    {
        var cut = RenderField();

        Type(cut, "  design ", "Enter");

        Assert.Equal(["design"], Tags(cut)); // trimmed
        Assert.Equal(["design"], cut.Instance.Values);
        Assert.Equal("", cut.Instance.Search);
        Assert.Equal("", Input(cut).GetAttribute("value"));
    }

    [Fact]
    public void A_comma_commits_by_default_and_tags_keep_their_entry_order()
    {
        var cut = RenderField(["alpha"]);

        Type(cut, "beta", ",");
        Type(cut, "gamma", "Enter");

        Assert.Equal(["alpha", "beta", "gamma"], Tags(cut));
    }

    [Fact]
    public void Only_the_configured_delimiters_commit()
    {
        var cut = RenderField(null, (nameof(BaseInputTags.Delimiters), new[] { ";", "Tab" }));

        Type(cut, "one", ","); // an ordinary character now
        Assert.Empty(Tags(cut));

        Type(cut, "one", ";");
        Type(cut, "two", "Tab");
        Assert.Equal(["one", "two"], Tags(cut));
    }

    [Fact]
    public void With_no_delimiters_only_Enter_commits()
    {
        var cut = RenderField(null, (nameof(BaseInputTags.Delimiters), Array.Empty<string>()));

        Type(cut, "one", ",");
        Assert.Empty(Tags(cut));

        Input(cut).KeyDown("Enter");
        Assert.Equal(["one"], Tags(cut));
    }

    [Fact]
    public void Blank_text_commits_nothing()
    {
        var cut = RenderField(["alpha"]);

        Type(cut, "   ", "Enter");

        Assert.Equal(["alpha"], Tags(cut));
        Assert.Equal(0, cut.Instance.ValueChanges);
        Assert.Equal("", cut.Instance.Search);
    }

    [Fact]
    public void A_tag_that_is_already_there_is_dropped_and_the_box_still_empties()
    {
        var cut = RenderField(["alpha"]);

        Type(cut, "alpha", "Enter");

        Assert.Equal(["alpha"], Tags(cut));
        Assert.Equal(0, cut.Instance.ValueChanges);
        Assert.Equal("", cut.Instance.Search);
    }

    [Fact]
    public void The_same_tag_is_accepted_twice_when_duplicates_are_allowed()
    {
        var cut = RenderField(["alpha"], (nameof(BaseInputTags.AllowDuplicates), true));

        Type(cut, "alpha", "Enter");

        Assert.Equal(["alpha", "alpha"], Tags(cut));
    }

    [Fact]
    public void At_the_limit_a_commit_adds_nothing_and_the_text_stays_until_there_is_room()
    {
        var cut = RenderField(["alpha", "beta"], (nameof(BaseInputTags.MaxTags), (int?)2));

        Type(cut, "gamma", "Enter");
        Assert.Equal(["alpha", "beta"], Tags(cut));
        Assert.Equal("gamma", cut.Instance.Search); // left in the box, so it is clear why nothing happened

        cut.FindAll("[data-bz-input-tags-chip-remove]")[0].Click();
        Input(cut).KeyDown("Enter");
        Assert.Equal(["beta", "gamma"], Tags(cut));
        Assert.Equal("", cut.Instance.Search);
    }

    [Fact]
    public void Backspace_in_an_empty_box_removes_the_newest_tag()
    {
        var cut = RenderField(["alpha", "beta"]);

        Input(cut).KeyDown("Backspace");

        Assert.Equal(["alpha"], Tags(cut));
    }

    [Fact]
    public void Backspace_while_there_is_text_only_edits_the_text()
    {
        var cut = RenderField(["alpha", "beta"]);

        Input(cut).Input("ga");
        Input(cut).KeyDown("Backspace");

        Assert.Equal(["alpha", "beta"], Tags(cut));
    }

    [Fact]
    public void Backspace_with_no_tags_does_nothing()
    {
        var cut = RenderField();

        Input(cut).KeyDown("Backspace");

        Assert.Equal(0, cut.Instance.ValueChanges);
    }

    [Fact]
    public void A_chips_remove_button_drops_that_tag_and_keeps_the_rest_in_order()
    {
        var cut = RenderField(["alpha", "beta", "gamma"]);

        cut.FindAll("[data-bz-input-tags-chip-remove]")[1].Click();

        Assert.Equal(["alpha", "gamma"], Tags(cut));
        Assert.Equal(1, cut.Instance.ValueChanges);
    }

    [Fact]
    public void Pasted_text_with_one_delimiter_becomes_a_tag_and_the_rest_stays_in_the_box()
    {
        var cut = RenderField(["alpha"]);

        Input(cut).Input(" beta ,gam");

        Assert.Equal(["alpha", "beta"], Tags(cut));
        Assert.Equal("gam", cut.Instance.Search);
    }

    [Fact(Skip = "bug: in a bound field (@bind-Values) pasted text with two or more delimiters keeps only its last tag, since each one is appended to the list as it was before the paste")]
    public void Pasted_text_with_several_delimiters_becomes_one_tag_each_in_a_bound_field()
    {
        var cut = RenderField();

        Input(cut).Input("alpha, beta ,gam");

        Assert.Equal(["alpha", "beta"], Tags(cut));
        Assert.Equal("gam", cut.Instance.Search);
    }

    [Fact]
    public void Pasted_text_with_several_delimiters_becomes_one_tag_each_in_a_field_left_to_itself()
    {
        var cut = Render<BaseInputTags>(p => p.AddChildContent(Parts));

        cut.Find("input").Input("alpha, beta ,gam");

        Assert.Equal(["alpha", "beta"], cut.FindAll("[data-bz-input-tags-chip]").Select(c => c.GetAttribute("data-value")));
        Assert.Equal("gam", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void A_key_name_among_the_delimiters_does_not_cut_text_that_contains_it()
    {
        var cut = RenderField(null, (nameof(BaseInputTags.Delimiters), new[] { "Tab", ";" }));

        Input(cut).Input("Tablet;Phone");

        Assert.Equal(["Tablet"], Tags(cut));
        Assert.Equal("Phone", cut.Instance.Search);
    }

    [Fact]
    public void The_parts_carry_their_roles_names_and_hooks()
    {
        var cut = RenderField(["alpha"], (nameof(BaseInputTags.Label), "Topics"), ("data-slot", "input-tags"));

        var root = cut.Find("[data-bz-input-tags]");
        Assert.Equal("group", root.GetAttribute("role"));
        Assert.Equal("input-tags", root.GetAttribute("data-slot"));
        Assert.False(root.HasAttribute("data-disabled"));

        var input = Input(cut);
        Assert.Equal("Topics", input.GetAttribute("aria-label"));
        Assert.Equal("text", input.GetAttribute("type"));
        Assert.Equal("Add a tag", input.GetAttribute("placeholder"));
        Assert.Equal("off", input.GetAttribute("autocomplete"));
        Assert.False(string.IsNullOrEmpty(input.Id));

        var remove = cut.Find("button[data-bz-input-tags-chip-remove]");
        Assert.Equal("Remove", remove.GetAttribute("aria-label"));
        Assert.Equal("button", remove.GetAttribute("type"));
        // Out of the tab order: Backspace is the keyboard way to remove.
        Assert.Equal("-1", remove.GetAttribute("tabindex"));
        Assert.False(remove.HasAttribute("disabled"));
    }

    [Fact]
    public void A_read_only_field_shows_its_tags_and_accepts_no_change()
    {
        var cut = RenderField(["alpha", "beta"], (nameof(BaseInputTags.ReadOnly), true));

        Assert.True(Input(cut).HasAttribute("readonly"));
        Assert.False(Input(cut).HasAttribute("disabled"));
        Assert.All(cut.FindAll("[data-bz-input-tags-chip-remove]"), remove => Assert.True(remove.HasAttribute("disabled")));

        Type(cut, "gamma", "Enter");
        Input(cut).Input("delta,");
        Input(cut).KeyDown("Backspace");
        // A script or an assistive tool can still raise the click on a disabled button's handler.
        cut.FindAll("[data-bz-input-tags-chip-remove]")[0].TriggerEvent("onclick", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Equal(["alpha", "beta"], Tags(cut));
        Assert.Equal(0, cut.Instance.ValueChanges);
    }

    [Fact]
    public void A_disabled_field_is_marked_on_every_part_and_accepts_no_change()
    {
        var cut = RenderField(["alpha"], (nameof(BaseInputTags.Disabled), true));

        Assert.True(cut.Find("[data-bz-input-tags]").HasAttribute("data-disabled"));
        Assert.True(Input(cut).HasAttribute("disabled"));
        Assert.True(cut.Find("[data-bz-input-tags-chip]").HasAttribute("data-disabled"));
        Assert.True(cut.Find("[data-bz-input-tags-chip-remove]").HasAttribute("disabled"));

        Input(cut).TriggerEvent("oninput", new ChangeEventArgs { Value = "beta," });
        Input(cut).TriggerEvent("onkeydown", new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });
        Input(cut).TriggerEvent("onkeydown", new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Backspace" });

        Assert.Equal(["alpha"], Tags(cut));
        Assert.Equal(0, cut.Instance.ValueChanges);
    }

    // The chips are text, not form controls: nothing tells assistive technology (or a contrast
    // check, which exempts inactive parts) that the dimmed tags of a disabled field are inactive.
    // The docs axe gate leaves the disabled example out until the root says so.
    [Fact(Skip = "bug: a disabled tags field marks its root with data-disabled only, not aria-disabled")]
    public void A_disabled_field_says_so_to_assistive_technology()
    {
        var cut = RenderField(["alpha"], (nameof(BaseInputTags.Disabled), true));

        Assert.Equal("true", cut.Find("[data-bz-input-tags]").GetAttribute("aria-disabled"));
    }

    [Fact]
    public void A_remove_button_rendered_as_another_element_is_disabled_for_assistive_tools()
    {
        RenderFragment<BzRenderProps> removeAs = props => builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.AddContent(2, "x");
            builder.CloseElement();
        };
        var cut = Render<BaseInputTags>(p => p
            .Add(x => x.DefaultValues, ["alpha"])
            .Add(x => x.ReadOnly, true)
            .AddChildContent<Chips>(chips => chips.Add(x => x.RemoveAs, removeAs)));

        var remove = cut.Find("span[data-bz-input-tags-chip-remove]");
        // A span takes no native disabled, and no type.
        Assert.False(remove.HasAttribute("disabled"));
        Assert.False(remove.HasAttribute("type"));
        Assert.Equal("true", remove.GetAttribute("aria-disabled"));
        Assert.True(remove.HasAttribute("data-disabled"));
    }

    [Fact]
    public void Left_to_itself_the_field_keeps_its_own_tags_and_text()
    {
        var cut = Render<BaseInputTags>(p => p
            .Add(x => x.DefaultValues, ["alpha"])
            .Add(x => x.DefaultSearch, "beta")
            .AddChildContent(Parts));
        Assert.Equal("beta", cut.Find("input").GetAttribute("value"));

        cut.Find("input").KeyDown("Enter");

        Assert.Equal(["alpha", "beta"], cut.FindAll("[data-bz-input-tags-chip]").Select(c => c.GetAttribute("data-value")));
        Assert.Equal("", cut.Find("input").GetAttribute("value"));

        cut.Find("input").KeyDown("Backspace");
        Assert.Equal(["alpha"], cut.FindAll("[data-bz-input-tags-chip]").Select(c => c.GetAttribute("data-value")));
    }

    [Fact]
    public void Outside_a_tags_input_the_parts_render_inert()
    {
        var input = Render<BaseInputTagsInput>();
        var remove = Render<BaseInputTagsChipRemove>();

        input.Find("input").KeyDown("Enter"); // no field to commit to: must not throw
        remove.Find("button").Click();

        Assert.Equal("", input.Find("input").GetAttribute("value"));
        Assert.False(input.Find("input").HasAttribute("id"));
    }
}
