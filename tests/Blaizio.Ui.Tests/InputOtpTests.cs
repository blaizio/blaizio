using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>BzInputOtp: the slots a code is shown in, masking, completion, and where each attribute lands.</summary>
public class InputOtpTests : BunitContext
{
    public InputOtpTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    // One group of slots, as many as the code is long.
    private IRenderedComponent<BzInputOtp> Otp(int length, Action<ComponentParameterCollectionBuilder<BzInputOtp>>? more = null) =>
        Render<BzInputOtp>(p =>
        {
            p.Add(x => x.MaxLength, length);
            p.AddChildContent<BzInputOtpGroup>(group =>
            {
                for (var i = 0; i < length; i++)
                {
                    var index = i;
                    group.AddChildContent<BzInputOtpSlot>(slot => slot.Add(x => x.Index, index));
                }
            });
            more?.Invoke(p);
        });

    private static string[] Slots(IRenderedComponent<BzInputOtp> cut) =>
        [.. cut.FindAll("[data-slot=input-otp-slot]").Select(slot => slot.TextContent.Trim())];

    // What the script reports from the one real field laid over the slots.
    private static Task TypeAsync(IRenderedComponent<BzInputOtp> cut, string text)
    {
        var field = cut.FindComponent<BaseInputOtp>();
        return field.InvokeAsync(() => field.Instance.OnChange(text));
    }

    private static Task CaretAsync(IRenderedComponent<BzInputOtp> cut, int start, int end)
    {
        var field = cut.FindComponent<BaseInputOtp>();
        return field.InvokeAsync(() => field.Instance.OnFocus(start, end));
    }

    [Fact]
    public void Each_character_of_the_value_shows_in_its_slot()
    {
        var cut = Otp(4, p => p.Add(x => x.DefaultValue, "12"));

        Assert.Equal(["1", "2", "", ""], Slots(cut));
        Assert.Equal("12", cut.Find("input[data-input-otp]").GetAttribute("value"));
        Assert.Single(cut.FindAll("[data-slot=input-otp-group]"));
    }

    [Fact]
    public async Task Typing_fills_the_slots_and_reports_the_code()
    {
        var values = new List<string?>();
        var cut = Otp(4, p => p.Add(x => x.ValueChanged, (string? value) => values.Add(value)));

        await TypeAsync(cut, "7");
        cut.Render(p => p.Add(x => x.Value, "7"));
        await TypeAsync(cut, "73");
        cut.Render(p => p.Add(x => x.Value, "73"));

        Assert.Equal(["7", "73"], values);
        Assert.Equal(["7", "3", "", ""], Slots(cut));
    }

    [Fact]
    public async Task A_field_left_to_itself_keeps_what_was_typed()
    {
        var cut = Otp(4);

        await TypeAsync(cut, "90");

        Assert.Equal(["9", "0", "", ""], Slots(cut));
    }

    [Fact]
    public async Task OnComplete_is_raised_each_time_the_code_becomes_full()
    {
        var completed = new List<string>();
        var cut = Otp(3, p => p.Add(x => x.OnComplete, (string code) => completed.Add(code)));

        await TypeAsync(cut, "12");
        Assert.Empty(completed);

        await TypeAsync(cut, "123");
        Assert.Equal(["123"], completed);

        // One character replaced in a full code is not a new completion.
        await TypeAsync(cut, "124");
        Assert.Equal(["123"], completed);

        await TypeAsync(cut, "12");
        await TypeAsync(cut, "125");
        Assert.Equal(["123", "125"], completed);
    }

    [Fact]
    public void A_value_set_from_outside_replaces_the_slots()
    {
        var cut = Otp(4, p => p.Add(x => x.Value, "1234").Add(x => x.ValueChanged, (string? _) => { }));

        cut.Render(p => p.Add(x => x.Value, "98"));

        Assert.Equal(["9", "8", "", ""], Slots(cut));
    }

    [Theory]
    [InlineData(null, "•")]
    [InlineData("*", "*")]
    public void A_masked_code_shows_the_mask_in_filled_slots_only(string? mask, string shown)
    {
        var cut = Otp(4, p =>
        {
            p.Add(x => x.DefaultValue, "12").Add(x => x.Mask, true);
            if (mask is not null) p.Add(x => x.MaskChar, mask);
        });

        Assert.Equal([shown, shown, "", ""], Slots(cut));
        // The field itself still holds the code: it is what a form posts and a manager fills.
        Assert.Equal("12", cut.Find("input[data-input-otp]").GetAttribute("value"));
    }

    [Fact]
    public async Task The_slot_under_the_caret_is_marked_and_an_empty_one_shows_a_caret()
    {
        var cut = Otp(4, p => p.Add(x => x.DefaultValue, "12"));
        Assert.All(cut.FindAll("[data-slot=input-otp-slot]"), slot => Assert.Equal("false", slot.GetAttribute("data-active")));

        await CaretAsync(cut, 2, 2);

        var slots = cut.FindAll("[data-slot=input-otp-slot]");
        Assert.Equal(["false", "false", "true", "false"], slots.Select(slot => slot.GetAttribute("data-active")));
        Assert.NotNull(slots[2].QuerySelector(".animate-caret-blink"));

        // On a filled slot the character is selected instead: no caret is drawn over it.
        await CaretAsync(cut, 1, 2);
        slots = cut.FindAll("[data-slot=input-otp-slot]");
        Assert.Equal(["false", "true", "false", "false"], slots.Select(slot => slot.GetAttribute("data-active")));
        Assert.Empty(cut.FindAll(".animate-caret-blink"));

        var field = cut.FindComponent<BaseInputOtp>();
        await field.InvokeAsync(() => field.Instance.OnBlur());
        Assert.All(cut.FindAll("[data-slot=input-otp-slot]"), slot => Assert.Equal("false", slot.GetAttribute("data-active")));
    }

    [Fact]
    public void The_field_carries_what_a_one_time_code_needs_by_default()
    {
        var field = Otp(6).Find("input[data-input-otp]");

        Assert.Equal("6", field.GetAttribute("maxlength"));
        Assert.Equal("numeric", field.GetAttribute("inputmode"));
        Assert.Equal("one-time-code", field.GetAttribute("autocomplete"));
    }

    [Fact]
    public void Length_keyboard_and_autofill_hint_can_be_changed()
    {
        var field = Otp(8, p => p
            .Add(x => x.InputMode, "text")
            .Add(x => x.AutoComplete, "off")
            .Add(x => x.AriaLabel, "Recovery code")
            .Add(x => x.InputClass, "tracking-widest")).Find("input[data-input-otp]");

        Assert.Equal("8", field.GetAttribute("maxlength"));
        Assert.Equal("text", field.GetAttribute("inputmode"));
        Assert.Equal("off", field.GetAttribute("autocomplete"));
        Assert.Equal("Recovery code", field.GetAttribute("aria-label"));
        Assert.Contains("tracking-widest", field.ClassList);
    }

    [Fact]
    public void The_pattern_and_the_length_are_handed_to_the_script_that_filters_typing()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/inputOtp.js");
        module.Mode = JSRuntimeMode.Loose;

        Otp(5, p => p.Add(x => x.Pattern, "^[A-Z0-9]+$"));

        var create = module.Invocations["createInputOtp"].Single();
        Assert.Equal(5, create.Arguments[2]);
        Assert.Equal("^[A-Z0-9]+$", create.Arguments[3]);
    }

    [Fact]
    public void Disabled_disables_the_field()
    {
        var cut = Otp(4, p => p.Add(x => x.Disabled, true));

        Assert.True(cut.Find("input[data-input-otp]").HasAttribute("disabled"));
        Assert.Equal("true", cut.Find("[data-slot=input-otp]").GetAttribute("data-disabled"));
        Assert.Contains("disabled:cursor-not-allowed", cut.Find("input[data-input-otp]").ClassList);
    }

    [Fact]
    public void What_names_or_describes_the_control_goes_to_the_field_and_the_rest_to_the_row()
    {
        var cut = Otp(4, p => p
            .Add(x => x.Class, "justify-center")
            .AddUnmatched("id", "code")
            .AddUnmatched("name", "otp")
            .AddUnmatched("required", true)
            .AddUnmatched("aria-describedby", "code-help")
            .AddUnmatched("aria-invalid", "true")
            .AddUnmatched("aria-labelledby", "code-label")
            .AddUnmatched("data-testid", "otp-row")
            .AddUnmatched("title", "Six digits"));

        var field = cut.Find("input[data-input-otp]");
        Assert.Equal("code", field.Id);
        Assert.Equal("otp", field.GetAttribute("name"));
        Assert.True(field.HasAttribute("required"));
        Assert.Equal("code-help", field.GetAttribute("aria-describedby"));
        Assert.Equal("true", field.GetAttribute("aria-invalid"));
        Assert.Equal("code-label", field.GetAttribute("aria-labelledby"));
        Assert.False(field.HasAttribute("data-testid"));

        var row = cut.Find("[data-slot=input-otp]");
        Assert.Equal("otp-row", row.GetAttribute("data-testid"));
        Assert.Equal("Six digits", row.GetAttribute("title"));
        Assert.Contains("justify-center", row.ClassList);
        Assert.False(row.HasAttribute("id"));
        Assert.False(row.HasAttribute("aria-invalid"));
        Assert.False(row.HasAttribute("name"));
    }

    [Fact]
    public void A_splatted_name_wins_over_the_parameter()
    {
        var field = Otp(4, p => p
            .Add(x => x.AriaLabel, "Code")
            .AddUnmatched("aria-label", "Verification code")).Find("input[data-input-otp]");

        Assert.Equal("Verification code", field.GetAttribute("aria-label"));
    }

    [Fact]
    public void AutoFocus_marks_the_row_for_the_focus_guard()
    {
        Assert.True(Otp(4, p => p.Add(x => x.AutoFocus, true)).Find("[data-slot=input-otp]").HasAttribute("data-bz-autofocus"));
        Assert.False(Otp(4).Find("[data-slot=input-otp]").HasAttribute("data-bz-autofocus"));
    }

    [Fact]
    public void A_separator_divides_groups_with_a_dash_or_with_content_of_its_own()
    {
        var cut = Render<BzInputOtp>(p => p
            .Add(x => x.MaxLength, 2)
            .Add(x => x.DefaultValue, "42")
            .AddChildContent<BzInputOtpGroup>(g => g.AddChildContent<BzInputOtpSlot>(s => s.Add(x => x.Index, 0)))
            .AddChildContent<BzInputOtpSeparator>()
            .AddChildContent<BzInputOtpGroup>(g => g
                .Add(x => x.Class, "ms-1")
                .AddChildContent<BzInputOtpSlot>(s => s.Add(x => x.Index, 1).Add(x => x.Class, "size-12")))
            .AddChildContent<BzInputOtpSeparator>(s => s.AddChildContent("<b>:</b>")));

        var separators = cut.FindAll("[data-slot=input-otp-separator]");
        Assert.All(separators, separator => Assert.Equal("separator", separator.GetAttribute("role")));
        Assert.NotNull(separators[0].QuerySelector("svg"));
        Assert.Equal(":", separators[1].TextContent.Trim());
        Assert.Null(separators[1].QuerySelector("svg"));

        Assert.Equal(["4", "2"], Slots(cut));
        Assert.Contains("ms-1", cut.FindAll("[data-slot=input-otp-group]")[1].ClassList);
        Assert.Contains("size-12", cut.FindAll("[data-slot=input-otp-slot]")[1].ClassList);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void A_slot_with_no_place_in_the_code_stays_empty(int index)
    {
        var cut = Render<BzInputOtp>(p => p
            .Add(x => x.MaxLength, 4)
            .Add(x => x.DefaultValue, "1234")
            .AddChildContent<BzInputOtpSlot>(s => s.Add(x => x.Index, index)));

        var slot = cut.Find("[data-slot=input-otp-slot]");
        Assert.Equal("", slot.TextContent.Trim());
        Assert.Equal("false", slot.GetAttribute("data-active"));
    }

    [Fact]
    public void A_slot_outside_a_code_field_renders_empty()
    {
        var slot = Render<BzInputOtpSlot>(p => p.Add(x => x.Index, 0)).Find("[data-slot=input-otp-slot]");

        Assert.Equal("", slot.TextContent.Trim());
    }
}
