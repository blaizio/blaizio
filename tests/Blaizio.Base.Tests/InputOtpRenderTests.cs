using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render tests for the headless <see cref="BaseInputOtp"/>. The selection/caret mechanics live in
/// ts/inputOtp.ts and are verified in-browser; here we cover the C# contract - the hidden input wiring,
/// the value -> per-slot char mapping, how the JS callbacks drive active/fake-caret state, and the
/// value + OnComplete bookkeeping. JSInterop is Loose so the module import is a no-op.
/// </summary>
public class InputOtpRenderTests : TestContext
{
    public InputOtpRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // A probe that captures the cascaded context so tests can assert the computed slot states.
    private sealed class ContextProbe : ComponentBase
    {
        [CascadingParameter] public InputOtpContext? Context { get; set; }
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) { }
    }

    [Fact]
    public void Renders_container_and_hidden_input()
    {
        var cut = RenderComponent<BaseInputOtp>(p => p
            .Add(x => x.MaxLength, 4)
            .Add(x => x.DefaultValue, "12")
            .Add(x => x.InputMode, "numeric"));

        Assert.NotNull(cut.Find("[data-slot=input-otp]"));
        var input = cut.Find("input[data-input-otp]");
        Assert.Equal("4", input.GetAttribute("maxlength"));
        Assert.Equal("12", input.GetAttribute("value"));
        Assert.Equal("numeric", input.GetAttribute("inputmode"));
        Assert.Equal("one-time-code", input.GetAttribute("autocomplete"));
    }

    [Fact]
    public void Disabled_marks_input_and_container()
    {
        var cut = RenderComponent<BaseInputOtp>(p => p.Add(x => x.Disabled, true));
        Assert.True(cut.Find("input[data-input-otp]").HasAttribute("disabled"));
        Assert.Equal("true", cut.Find("[data-slot=input-otp]").GetAttribute("data-disabled"));
    }

    [Fact]
    public void Cascades_one_slot_per_maxlength_with_chars_from_value()
    {
        var cut = RenderComponent<BaseInputOtp>(p => p
            .Add(x => x.MaxLength, 4)
            .Add(x => x.DefaultValue, "12")
            .AddChildContent<ContextProbe>());

        var ctx = cut.FindComponent<ContextProbe>().Instance.Context!;
        Assert.Equal(4, ctx.Slots.Count);
        Assert.Equal('1', ctx.Slots[0].Char);
        Assert.Equal('2', ctx.Slots[1].Char);
        Assert.Null(ctx.Slots[2].Char);
        Assert.Null(ctx.Slots[3].Char);
        Assert.False(ctx.IsFocused); // no focus yet -> nothing active
        Assert.All(ctx.Slots, s => Assert.False(s.IsActive));
    }

    [Fact]
    public async Task Focus_and_selection_drive_active_slot_and_fake_caret()
    {
        var cut = RenderComponent<BaseInputOtp>(p => p
            .Add(x => x.MaxLength, 4)
            .Add(x => x.DefaultValue, "12")
            .AddChildContent<ContextProbe>());

        // A range selection [1,2) lights the char at index 1.
        await cut.InvokeAsync(() => cut.Instance.OnFocus(1, 2));
        var ctx = cut.FindComponent<ContextProbe>().Instance.Context!;
        Assert.True(ctx.IsFocused);
        Assert.True(ctx.Slots[1].IsActive);
        Assert.False(ctx.Slots[1].HasFakeCaret); // has a char

        // Caret in the empty slot 2 shows the fake caret.
        await cut.InvokeAsync(() => cut.Instance.OnSelectionChange(2, 2));
        ctx = cut.FindComponent<ContextProbe>().Instance.Context!;
        Assert.True(ctx.Slots[2].IsActive);
        Assert.True(ctx.Slots[2].HasFakeCaret);

        // Blur clears the active state.
        await cut.InvokeAsync(() => cut.Instance.OnBlur());
        ctx = cut.FindComponent<ContextProbe>().Instance.Context!;
        Assert.False(ctx.IsFocused);
        Assert.All(ctx.Slots, s => Assert.False(s.IsActive));
    }

    [Fact]
    public async Task OnChange_updates_value_uncontrolled()
    {
        var cut = RenderComponent<BaseInputOtp>(p => p
            .Add(x => x.MaxLength, 4)
            .AddChildContent<ContextProbe>());

        await cut.InvokeAsync(() => cut.Instance.OnChange("34"));

        var ctx = cut.FindComponent<ContextProbe>().Instance.Context!;
        Assert.Equal('3', ctx.Slots[0].Char);
        Assert.Equal('4', ctx.Slots[1].Char);
    }

    [Fact]
    public async Task OnComplete_fires_once_when_value_fills()
    {
        var completed = new List<string>();
        var cut = RenderComponent<BaseInputOtp>(p => p
            .Add(x => x.MaxLength, 3)
            .Add(x => x.OnComplete, EventCallback.Factory.Create<string>(this, completed.Add)));

        await cut.InvokeAsync(() => cut.Instance.OnChange("12"));
        Assert.Empty(completed); // not full yet

        await cut.InvokeAsync(() => cut.Instance.OnChange("123"));
        Assert.Equal(new[] { "123" }, completed); // fired exactly once on filling
    }
}
