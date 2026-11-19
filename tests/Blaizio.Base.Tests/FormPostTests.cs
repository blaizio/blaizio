using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// <c>Name</c> on the controls that keep their value in C#: a hidden input (one per value) carries
/// the value in a plain form post. Without <c>Name</c> the markup holds no such input, and a
/// disabled control posts nothing, as a browser does for a disabled field.
/// </summary>
public class FormPostTests : BunitContext
{
    public FormPostTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static List<(string? Name, string? Value)> Posted<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll("input[type=hidden]").Select(i => (i.GetAttribute("name"), i.GetAttribute("value"))).ToList();

    private static void InCulture(string culture, Action test)
    {
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try { test(); }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Fact]
    public void Without_a_name_no_control_renders_a_hidden_input()
    {
        Assert.Empty(Posted(Render<BaseSwitch>(p => p.Add(x => x.DefaultChecked, true))));
        Assert.Empty(Posted(Render<BaseCheckbox>(p => p.Add(x => x.DefaultChecked, CheckedState.Checked))));
        Assert.Empty(Posted(Render<BaseRadioGroup>(p => p.Add(x => x.DefaultValue, "a").Add(x => x.AriaLabel, "Pick"))));
        Assert.Empty(Posted(Render<BaseSlider>(p => p.Add(x => x.DefaultValue, [10d]))));
        Assert.Empty(Posted(Render<BaseToggleGroup>(p => p.Add(x => x.DefaultValue, "a"))));
        Assert.Empty(Posted(Render<BaseInputTags>(p => p.Add(x => x.DefaultValues, ["a"]))));
        Assert.Empty(Posted(Render<BaseInputNumber<int>>(p => p.Add(x => x.DefaultValue, 3))));
        Assert.Empty(Posted(Render<BaseSelect>(p => p.Add(x => x.DefaultValue, "a"))));
        Assert.Empty(Posted(Render<BaseCombobox>(p => p.Add(x => x.DefaultValue, "a"))));
        Assert.Null(Render<BaseInputOtp>().Find("input").GetAttribute("name"));
    }

    [Fact]
    public void A_switch_posts_true_while_it_is_on()
    {
        var cut = Render<BaseSwitch>(p => p.Add(x => x.Name, "notify"));
        Assert.Empty(Posted(cut));

        cut.Find("[role=switch]").Click();
        Assert.Equal([("notify", "true")], Posted(cut));

        // Beside the button, not inside it.
        Assert.Null(cut.Find("input[type=hidden]").Closest("button"));

        cut.Find("[role=switch]").Click();
        Assert.Empty(Posted(cut));
    }

    [Fact]
    public void A_disabled_control_posts_nothing()
    {
        Assert.Empty(Posted(Render<BaseSwitch>(p => p
            .Add(x => x.Name, "notify").Add(x => x.DefaultChecked, true).Add(x => x.Disabled, true))));
        Assert.Empty(Posted(Render<BaseSelect>(p => p
            .Add(x => x.Name, "plan").Add(x => x.DefaultValue, "pro").Add(x => x.Disabled, true))));
    }

    [Theory]
    [InlineData(CheckedState.Checked, true)]
    [InlineData(CheckedState.Unchecked, false)]
    [InlineData(CheckedState.Indeterminate, false)]
    public void A_checkbox_posts_true_only_when_checked(CheckedState state, bool posted)
    {
        var cut = Render<BaseCheckbox>(p => p.Add(x => x.Name, "terms").Add(x => x.DefaultChecked, state));

        Assert.Equal(posted ? [("terms", "true")] : [], Posted(cut));
    }

    [Fact]
    public void A_radio_group_posts_the_selected_value()
    {
        var none = Render<BaseRadioGroup>(p => p.Add(x => x.Name, "level").Add(x => x.AriaLabel, "Level"));
        Assert.Empty(Posted(none));

        var cut = Render<BaseRadioGroup>(p => p
            .Add(x => x.Name, "level").Add(x => x.DefaultValue, "all").Add(x => x.AriaLabel, "Level"));
        Assert.Equal([("level", "all")], Posted(cut));
        Assert.NotNull(cut.Find("input[type=hidden]").Closest("[role=radiogroup]"));
    }

    [Fact]
    public void A_slider_posts_one_value_per_thumb_as_a_native_number_input_writes_it()
    {
        // A culture with a decimal comma: the posted text must not follow it.
        InCulture("de-DE", () =>
        {
            var cut = Render<BaseSlider>(p => p.Add(x => x.Name, "range").Add(x => x.DefaultValue, [12.5, 80d]));

            Assert.Equal([("range", "12.5"), ("range", "80")], Posted(cut));
        });
    }

    [Fact]
    public void A_toggle_group_posts_each_value_that_is_on()
    {
        var single = Render<BaseToggleGroup>(p => p.Add(x => x.Name, "align").Add(x => x.DefaultValue, "left"));
        Assert.Equal([("align", "left")], Posted(single));

        var multiple = Render<BaseToggleGroup>(p => p
            .Add(x => x.Name, "style")
            .Add(x => x.SelectionMode, SelectionMode.Multiple)
            .Add(x => x.DefaultValues, ["bold", "italic"]));
        Assert.Equal([("style", "bold"), ("style", "italic")], Posted(multiple));

        Assert.Empty(Posted(Render<BaseToggleGroup>(p => p.Add(x => x.Name, "align"))));
    }

    [Fact]
    public void Input_tags_post_each_tag()
    {
        var cut = Render<BaseInputTags>(p => p.Add(x => x.Name, "tags").Add(x => x.DefaultValues, ["blazor", "ui"]));

        Assert.Equal([("tags", "blazor"), ("tags", "ui")], Posted(cut));
    }

    [Fact]
    public void An_input_number_posts_the_raw_number_and_nothing_when_empty()
    {
        InCulture("de-DE", () =>
        {
            var cut = Render<BaseInputNumber<decimal?>>(p => p.Add(x => x.Name, "price").Add(x => x.DefaultValue, 1234.5m));
            Assert.Equal([("price", "1234.5")], Posted(cut));
        });

        Assert.Empty(Posted(Render<BaseInputNumber<decimal?>>(p => p.Add(x => x.Name, "price"))));
        Assert.Equal([("count", "0")], Posted(Render<BaseInputNumber<int>>(p => p.Add(x => x.Name, "count"))));
    }

    [Fact]
    public void A_select_posts_its_value_or_one_input_per_value()
    {
        var single = Render<BaseSelect>(p => p.Add(x => x.Name, "plan").Add(x => x.DefaultValue, "pro"));
        Assert.Equal([("plan", "pro")], Posted(single));

        var multiple = Render<BaseSelect>(p => p
            .Add(x => x.Name, "plans")
            .Add(x => x.SelectionMode, SelectionMode.Multiple)
            .Add(x => x.DefaultValues, ["free", "pro"]));
        Assert.Equal([("plans", "free"), ("plans", "pro")], Posted(multiple));

        Assert.Empty(Posted(Render<BaseSelect>(p => p.Add(x => x.Name, "plan"))));
    }

    [Fact]
    public void A_combobox_posts_its_value_or_one_input_per_value()
    {
        var single = Render<BaseCombobox>(p => p.Add(x => x.Name, "city").Add(x => x.DefaultValue, "dxb"));
        Assert.Equal([("city", "dxb")], Posted(single));

        var multiple = Render<BaseCombobox>(p => p
            .Add(x => x.Name, "cities")
            .Add(x => x.SelectionMode, SelectionMode.Multiple)
            .Add(x => x.DefaultValues, ["dxb", "auh"]));
        Assert.Equal([("cities", "dxb"), ("cities", "auh")], Posted(multiple));
    }

    [Fact]
    public void An_input_otp_names_its_own_input()
    {
        var cut = Render<BaseInputOtp>(p => p.Add(x => x.Name, "code").Add(x => x.DefaultValue, "1234"));

        // The code is a native input's value already: no second input.
        Assert.Equal("code", cut.Find("input[data-input-otp]").GetAttribute("name"));
        Assert.Empty(cut.FindAll("input[type=hidden]"));
    }

    [Fact]
    public void The_hidden_input_component_renders_nothing_without_a_name_or_a_value()
    {
        Assert.Equal("", Render<BaseHiddenInput>(p => p.Add(x => x.Value, "v")).Markup.Trim());
        Assert.Equal("", Render<BaseHiddenInput>(p => p.Add(x => x.Name, "n")).Markup.Trim());
        Assert.Equal("", Render<BaseHiddenInput>(p => p
            .Add(x => x.Name, "n").Add(x => x.Value, "v").Add(x => x.Disabled, true)).Markup.Trim());
    }
}
