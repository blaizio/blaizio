using System.Globalization;
using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// <c>Name</c> on the styled controls: it reaches the Base control that renders the hidden input,
/// and the three controls that live in the styled layer only (date, time, color) post their value
/// the way the matching native input writes it.
/// </summary>
public sealed class FormPostTests : BunitContext
{
    public FormPostTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private static List<(string? Name, string? Value)> Posted<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll("input[type=hidden]").Select(i => (i.GetAttribute("name"), i.GetAttribute("value"))).ToList();

    [Fact]
    public void A_styled_switch_hands_its_name_to_the_control_that_posts()
    {
        var cut = Render<BzSwitch>(p => p.Add(x => x.Name, "notify").Add(x => x.DefaultChecked, true));

        Assert.Equal([("notify", "true")], Posted(cut));
        // The name is the hidden input's, not an attribute left on the button.
        Assert.Null(cut.Find("[role=switch]").GetAttribute("name"));
    }

    [Fact]
    public void Styled_controls_without_a_name_render_no_hidden_input()
    {
        Assert.Empty(Posted(Render<BzSwitch>(p => p.Add(x => x.DefaultChecked, true))));
        Assert.Empty(Posted(Render<BzCheckbox>(p => p.Add(x => x.DefaultChecked, CheckedState.Checked))));
        Assert.Empty(Posted(Render<BzSlider>(p => p.Add(x => x.DefaultValue, 10d))));
        Assert.Empty(Posted(Render<BzInputNumber<int>>(p => p.Add(x => x.DefaultValue, 3))));
        Assert.Empty(Posted(Render<BzInputDate>(p => p.Add(x => x.DefaultValue, new DateOnly(2026, 11, 19)))));
        Assert.Empty(Posted(Render<BzInputTime>(p => p
            .Add(x => x.HourCycle, InputTimeHourCycle.TwentyFourHour)
            .Add(x => x.DefaultValue, new TimeOnly(14, 30)))));
    }

    [Fact]
    public void A_styled_slider_and_number_field_post_through_their_controls()
    {
        Assert.Equal([("volume", "40")], Posted(Render<BzSlider>(p => p.Add(x => x.Name, "volume").Add(x => x.DefaultValue, 40d))));
        Assert.Equal([("qty", "3")], Posted(Render<BzInputNumber<int>>(p => p.Add(x => x.Name, "qty").Add(x => x.DefaultValue, 3))));
    }

    [Fact]
    public void An_input_date_posts_the_date_as_a_native_date_input_writes_it()
    {
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var cut = Render<BzInputDate>(p => p.Add(x => x.Name, "due").Add(x => x.DefaultValue, new DateOnly(2026, 11, 9)));

            Assert.Equal([("due", "2026-11-09")], Posted(cut));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }

        // Nothing picked: the name is absent from the post.
        Assert.Empty(Posted(Render<BzInputDate>(p => p.Add(x => x.Name, "due"))));
    }

    [Fact]
    public void An_input_time_names_the_native_input_on_the_Auto_face()
    {
        var cut = Render<BzInputTime>(p => p.Add(x => x.Name, "start").Add(x => x.DefaultValue, new TimeOnly(14, 30)));

        var native = cut.Find("input[type=time]");
        Assert.Equal("start", native.GetAttribute("name"));
        Assert.Equal("14:30", native.GetAttribute("value"));
        // The native input is the control there: no second input.
        Assert.Empty(Posted(cut));
    }

    [Theory]
    [InlineData(false, "14:30")]
    [InlineData(true, "14:30:05")]
    public void An_input_time_posts_a_hidden_input_on_the_segmented_faces(bool seconds, string expected)
    {
        var cut = Render<BzInputTime>(p => p
            .Add(x => x.Name, "start")
            .Add(x => x.HourCycle, InputTimeHourCycle.TwentyFourHour)
            .Add(x => x.ShowSeconds, seconds)
            .Add(x => x.DefaultValue, new TimeOnly(14, 30, 5)));

        Assert.Equal([("start", expected)], Posted(cut));
        // The segment inputs hold one part each and stay out of the post.
        Assert.All(cut.FindAll("input:not([type=hidden])"), segment => Assert.Null(segment.GetAttribute("name")));
    }

    [Fact]
    public void An_input_color_names_its_own_text_input()
    {
        var cut = Render<BzInputColor>(p => p.Add(x => x.Name, "accent").Add(x => x.DefaultValue, "#ff0000"));

        Assert.Equal("accent", cut.Find("[data-slot=input-color-input]").GetAttribute("name"));
        Assert.Empty(Posted(cut));
    }

    [Fact]
    public void A_styled_select_and_combobox_post_through_their_controls()
    {
        var select = Render<BzSelect>(p => p.Add(x => x.Name, "plan").Add(x => x.DefaultValue, "pro"));
        Assert.Equal([("plan", "pro")], Posted(select));

        var otp = Render<BzInputOtp>(p => p.Add(x => x.Name, "code"));
        Assert.Equal("code", otp.Find("input[data-input-otp]").GetAttribute("name"));
    }
}
