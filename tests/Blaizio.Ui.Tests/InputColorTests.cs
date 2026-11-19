using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>BzInputColor: the text field, what it accepts and writes, the invalid state, and the picker popup.</summary>
public class InputColorTests : BunitContext
{
    public InputColorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private const string Field = "input[data-slot=input-color-input]";
    private const string OpenPicker = "button[aria-label='Open color picker']";

    private static string Shown(IRenderedComponent<IComponent> cut) => cut.Find(Field).GetAttribute("value") ?? "";

    [Fact]
    public void An_empty_field_shows_its_placeholder_and_a_blank_swatch()
    {
        var cut = Render<BzInputColor>();

        var field = cut.Find(Field);
        Assert.Equal("", Shown(cut));
        Assert.Equal("#000000", field.GetAttribute("placeholder"));
        Assert.Equal("Color", field.GetAttribute("aria-label"));
        Assert.False(field.HasAttribute("aria-invalid"));
        var swatch = cut.Find("[data-slot=input-color-swatch]");
        Assert.Equal("true", swatch.GetAttribute("aria-hidden"));
        Assert.Contains("linear-gradient(transparent, transparent)", swatch.GetAttribute("style"));
    }

    [Theory]
    [InlineData(ColorFormat.Hex, "#ff8800")]
    [InlineData(ColorFormat.Rgb, "rgb(255 136 0)")]
    [InlineData(ColorFormat.Hsl, "hsl(32 100% 50%)")]
    [InlineData(ColorFormat.Cmyk, "device-cmyk(0% 46.67% 100% 0%)")]
    public void A_value_in_any_format_is_shown_in_the_fields_format(ColorFormat format, string shown)
    {
        var cut = Render<BzInputColor>(p => p.Add(x => x.DefaultValue, "rgb(255, 136, 0)").Add(x => x.Format, format));

        Assert.Equal(shown, Shown(cut));
        // The swatch is always something a browser paints.
        Assert.Contains("linear-gradient(rgb(255 136 0), rgb(255 136 0))", cut.Find("[data-slot=input-color-swatch]").GetAttribute("style"));
    }

    [Theory]
    [InlineData("00ff00")]
    [InlineData(" #0f0 ")]
    [InlineData("rgb(0 255 0)")]
    [InlineData("hsl(120 100% 50%)")]
    [InlineData("0 255 0")]
    public void A_color_typed_in_any_format_is_written_in_the_fields_format(string typed)
    {
        var values = new List<string?>();
        var cut = Render<BzInputColor>(p => p
            .Add(x => x.Value, "#ff0000")
            .Add(x => x.ValueChanged, (string? value) => values.Add(value)));

        cut.Find(Field).Change(typed);

        Assert.Equal(["#00ff00"], values);
    }

    [Fact]
    public void Clearing_the_field_clears_the_value()
    {
        var values = new List<string?>();
        var cut = Render<BzInputColor>(p => p
            .Add(x => x.Value, "#ff0000")
            .Add(x => x.ValueChanged, (string? value) => values.Add(value)));

        cut.Find(Field).Change("   ");

        Assert.Equal([null], values);
    }

    [Fact]
    public void Text_that_is_not_a_color_stays_in_the_field_marked_invalid_until_a_color_replaces_it()
    {
        var values = new List<string?>();
        var cut = Render<BzInputColor>(p => p
            .Add(x => x.Value, "#ff0000")
            .Add(x => x.ValueChanged, (string? value) => values.Add(value)));

        cut.Find(Field).Change("not a color");

        Assert.Empty(values);
        Assert.Equal("not a color", Shown(cut));
        Assert.Equal("true", cut.Find(Field).GetAttribute("aria-invalid"));
        // The swatch keeps the last good color.
        Assert.Contains("rgb(255 0 0)", cut.Find("[data-slot=input-color-swatch]").GetAttribute("style"));

        cut.Find(Field).Change("#00ff00");

        Assert.Equal(["#00ff00"], values);
        Assert.False(cut.Find(Field).HasAttribute("aria-invalid"));
    }

    [Fact]
    public void A_value_set_from_outside_replaces_an_invalid_entry()
    {
        var cut = Render<BzInputColor>(p => p
            .Add(x => x.Value, "#ff0000")
            .Add(x => x.ValueChanged, (string? _) => { }));
        cut.Find(Field).Change("oops");
        Assert.Equal("oops", Shown(cut));

        cut.Render(p => p.Add(x => x.Value, "#0000ff"));

        Assert.Equal("#0000ff", Shown(cut));
        Assert.False(cut.Find(Field).HasAttribute("aria-invalid"));
    }

    [Fact]
    public void A_field_left_to_itself_keeps_what_was_typed()
    {
        var cut = Render<BzInputColor>(p => p.Add(x => x.DefaultValue, "#ff0000"));

        cut.Find(Field).Change("rgb(0 0 255)");
        Assert.Equal("#0000ff", Shown(cut));

        cut.Find(Field).Change("");
        Assert.Equal("", Shown(cut));
    }

    [Theory]
    [InlineData(false, "#ff0000")]
    [InlineData(true, "#ff000080")]
    public void Transparency_is_kept_only_when_the_field_shows_alpha(bool alpha, string written)
    {
        var values = new List<string?>();
        var cut = Render<BzInputColor>(p => p
            .Add(x => x.ShowAlpha, alpha)
            .Add(x => x.Value, "#0000ff80")
            .Add(x => x.ValueChanged, (string? value) => values.Add(value)));
        Assert.Equal(alpha ? "#0000ff80" : "#0000ff", Shown(cut));

        cut.Find(Field).Change("rgb(255 0 0 / 0.5)");

        Assert.Equal([written], values);
    }

    [Fact]
    public void A_perceptual_value_in_the_fields_own_format_is_kept_as_written()
    {
        var values = new List<string?>();
        var cut = Render<BzInputColor>(p => p
            .Add(x => x.Format, ColorFormat.Oklch)
            .Add(x => x.Value, "oklch(0.7 0.185 285)")
            .Add(x => x.ValueChanged, (string? value) => values.Add(value)));
        Assert.Equal("oklch(0.7 0.185 285)", Shown(cut));

        cut.Find(Field).Change("oklch(0.62 0.21 259.1234)");
        // Typed in another format, it is converted like any other.
        cut.Find(Field).Change("#ff0000");

        Assert.Equal(["oklch(0.62 0.21 259.1234)", "oklch(0.628 0.258 29.23)"], values);
    }

    [Fact]
    public void Hex_reads_uppercase_and_the_field_is_as_wide_as_its_format_needs()
    {
        var hex = Render<BzInputColor>().Find(Field);
        Assert.Contains("uppercase", hex.ClassList);
        Assert.Contains("w-[7ch]", hex.ClassList);
        Assert.Contains("w-[9ch]", Render<BzInputColor>(p => p.Add(x => x.ShowAlpha, true)).Find(Field).ClassList);

        var rgb = Render<BzInputColor>(p => p.Add(x => x.Format, ColorFormat.Rgb)).Find(Field);
        Assert.DoesNotContain("uppercase", rgb.ClassList);
        Assert.Contains("w-[18ch]", rgb.ClassList);
        Assert.Contains("w-[24ch]", Render<BzInputColor>(p => p.Add(x => x.Format, ColorFormat.Cmyk)).Find(Field).ClassList);
    }

    [Fact]
    public void The_swatch_and_the_picker_button_can_be_left_out()
    {
        var cut = Render<BzInputColor>(p => p.Add(x => x.ShowSwatch, false).Add(x => x.ShowPicker, false));

        Assert.Empty(cut.FindAll("[data-slot=input-color-swatch]"));
        Assert.Empty(cut.FindAll("button"));
        Assert.Single(cut.FindAll(Field));
    }

    [Fact]
    public void Class_goes_to_the_box_and_other_attributes_to_the_text_field()
    {
        var cut = Render<BzInputColor>(p => p
            .Add(x => x.Class, "w-full")
            .Add(x => x.Placeholder, "Brand color")
            .Add(x => x.AutoFocus, true)
            .AddUnmatched("id", "brand")
            .AddUnmatched("aria-describedby", "brand-help"));

        Assert.Contains("w-full", cut.Find("[data-slot=input-color]").ClassList);
        var field = cut.Find(Field);
        Assert.Equal("brand", field.Id);
        Assert.Equal("brand-help", field.GetAttribute("aria-describedby"));
        Assert.Equal("Brand color", field.GetAttribute("placeholder"));
        Assert.True(field.HasAttribute("data-bz-autofocus"));
    }

    [Fact]
    public void Disabled_disables_the_field_and_the_picker_button()
    {
        var cut = Render<BzInputColor>(p => p.Add(x => x.Disabled, true).Add(x => x.DefaultValue, "#ff0000"));

        Assert.True(cut.Find("[data-slot=input-color]").HasAttribute("data-disabled"));
        Assert.True(cut.Find(Field).HasAttribute("disabled"));
        Assert.True(cut.Find(OpenPicker).HasAttribute("disabled"));
        Assert.Equal("#ff0000", Shown(cut));
    }

    // ---- the popup ----------------------------------------------------------------------------

    [Fact]
    public void The_button_opens_a_picker_on_the_fields_color()
    {
        var cut = Render<BzInputColor>(p => p.Add(x => x.DefaultValue, "#00ff00"));
        var button = cut.Find(OpenPicker);
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-slot=color-picker]"));

        button.Click();

        Assert.Equal("true", cut.Find(OpenPicker).GetAttribute("aria-expanded"));
        Assert.Equal("Color picker", cut.Find("[data-slot=popover-content]").GetAttribute("aria-label"));
        Assert.Equal("120", cut.Find("[data-color-slider=hue] [role=slider]").GetAttribute("aria-valuenow"));
        // The field above already shows the text, so the popup has no text field of its own.
        Assert.Empty(cut.FindAll("input[data-slot=color-input]"));
    }

    [Fact]
    public void A_color_picked_in_the_popup_lands_in_the_field_and_the_popup_stays_open()
    {
        var values = new List<string?>();
        var cut = Render<BzInputColor>(p => p
            .Add(x => x.Value, "#ff0000")
            .Add(x => x.Format, ColorFormat.Rgb)
            .Add(x => x.Swatches, ["#00ff00", "#0000ff"])
            .Add(x => x.ValueChanged, (string? value) => values.Add(value)));
        cut.Find(OpenPicker).Click();

        cut.FindAll("[data-slot=color-swatch]")[1].Click();

        Assert.Equal(["rgb(0 0 255)"], values);
        Assert.Equal("true", cut.Find(OpenPicker).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Dragging_in_the_popup_updates_a_field_left_to_itself()
    {
        var cut = Render<BzInputColor>(p => p.Add(x => x.DefaultValue, "#ff0000"));
        cut.Find(OpenPicker).Click();

        var hue = cut.FindComponents<BaseSlider>().Single(s => s.Markup.Contains("data-color-slider=\"hue\""));
        hue.InvokeAsync(() => hue.Instance.OnInput(0, 240));

        Assert.Equal("#0000ff", Shown(cut));
        Assert.Contains("rgb(0 0 255)", cut.Find("[data-slot=input-color-swatch]").GetAttribute("style"));
    }

    [Fact]
    public void The_popup_follows_the_alpha_and_format_options_of_the_field()
    {
        var plain = Render<BzInputColor>(p => p.Add(x => x.DefaultValue, "#ff0000"));
        plain.Find(OpenPicker).Click();
        Assert.Empty(plain.FindAll("[data-color-slider=alpha]"));
        Assert.Empty(plain.FindAll("[data-slot=color-format]"));

        var full = Render<BzInputColor>(p => p
            .Add(x => x.DefaultValue, "#ff0000")
            .Add(x => x.ShowAlpha, true)
            .Add(x => x.ShowFormatPicker, true));
        full.Find(OpenPicker).Click();
        Assert.Single(full.FindAll("[data-color-slider=alpha]"));
        Assert.Single(full.FindAll("[data-slot=color-format]"));
    }

    [Fact]
    public void A_format_picked_in_the_popup_becomes_the_fields_format()
    {
        var formats = new List<ColorFormat>();
        var cut = Render<BzInputColor>(p => p
            .Add(x => x.DefaultValue, "#ff0000")
            .Add(x => x.ShowFormatPicker, true));
        cut.Find(OpenPicker).Click();

        cut.FindAll("[data-slot=color-format] [role=option]").Single(o => o.TextContent.Trim() == "HSL").Click();

        Assert.Equal("hsl(0 100% 50%)", Shown(cut));
        Assert.DoesNotContain("uppercase", cut.Find(Field).ClassList);

        // Bound, the page is told and decides.
        var bound = Render<BzInputColor>(p => p
            .Add(x => x.DefaultValue, "#ff0000")
            .Add(x => x.ShowFormatPicker, true)
            .Add(x => x.FormatChanged, (ColorFormat format) => formats.Add(format)));
        bound.Find(OpenPicker).Click();
        bound.FindAll("[data-slot=color-format] [role=option]").Single(o => o.TextContent.Trim() == "HSL").Click();
        Assert.Equal([ColorFormat.Hsl], formats);
    }
}
