using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// Text that is still waiting on <c>DebounceDelay</c> is raised when the user is done with the
/// field (it loses focus, or Enter is pressed), and the first arrow key on an empty time segment
/// lands on the step's grid.
/// </summary>
public sealed class InputFlushTests : BunitContext
{
    public InputFlushTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    [Fact]
    public void Leaving_the_field_raises_the_text_still_waiting_on_the_pause()
    {
        var raised = new List<string?>();
        var cut = Render<BzInputText>(p => p
            .Add(x => x.DebounceDelay, 60_000)
            .Add(x => x.ValueChanged, (string? v) => raised.Add(v)));

        cut.Find("input").Input("abc");
        Assert.Empty(raised);

        cut.Find("input").Blur();
        Assert.Equal(["abc"], raised);
    }

    [Fact]
    public void Enter_raises_the_waiting_text_before_the_key_handler_runs()
    {
        // Submit on Enter reads the bound value in its key handler: it used to see the text from
        // before the last burst of typing, and the change arrived after it.
        var log = new List<string>();
        var cut = Render<BzInputText>(p => p
            .Add(x => x.DebounceDelay, 60_000)
            .Add(x => x.ValueChanged, (string? v) => log.Add("value:" + v))
            .Add(x => x.OnKeyDown, (KeyboardEventArgs e) => log.Add("key:" + e.Key)));

        cut.Find("input").Input("abc");
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(["value:abc", "key:Enter"], log);

        // Raised once: leaving the field afterwards has nothing left to send.
        cut.Find("input").Blur();
        Assert.Equal(2, log.Count);
    }

    [Fact]
    public void A_field_with_no_pause_and_no_handlers_listens_for_neither_keys_nor_blur()
    {
        var cut = Render<BzInputText>();

        Assert.Throws<MissingEventHandlerException>(() => cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "a" }));
        Assert.Throws<MissingEventHandlerException>(() => cut.Find("input").Blur());
    }

    [Theory]
    [InlineData("ArrowUp", "00")]
    [InlineData("ArrowDown", "45")]
    public void The_first_arrow_on_an_empty_minute_lands_on_the_steps_grid(string key, string expected)
    {
        var cut = Render<BzInputTime>(p => p
            .Add(x => x.HourCycle, InputTimeHourCycle.TwentyFourHour)
            .Add(x => x.MinuteStep, 15));

        // Up used to wrap from 59 and land on 14.
        cut.Find("input[aria-label=Minute]").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Equal(expected, cut.Find("input[aria-label=Minute]").GetAttribute("value"));
    }
}
