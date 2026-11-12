using Bunit;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Pasted text that holds a delimiter is split into tags, and what is left of it (often nothing)
/// becomes the query. That is also what was rendered before the paste, so the renderer has no
/// change to send: the box has to be written through the script, or the pasted text stays in it.
/// </summary>
public class InputTagsPasteTests : BunitContext
{
    [Fact]
    public void Pasted_text_that_was_split_is_cleared_from_the_box_through_the_script()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/inputTags.js");
        module.Mode = JSRuntimeMode.Loose;

        var cut = Render<BaseInputTags>(p => p.AddChildContent<BaseInputTagsInput>());
        var input = cut.Find("input");

        input.Input("x1,x2,");

        var write = Assert.Single(module.Invocations, i => i.Identifier == "setInputText");
        Assert.Equal(input.Id, write.Arguments[0]);
        Assert.Equal("", write.Arguments[1]);
    }

    [Fact]
    public void Plain_typing_writes_nothing_through_the_script()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/inputTags.js");
        module.Mode = JSRuntimeMode.Loose;

        var cut = Render<BaseInputTags>(p => p.AddChildContent<BaseInputTagsInput>());
        cut.Find("input").Input("x1");

        Assert.DoesNotContain(module.Invocations, i => i.Identifier == "setInputText");
    }
}
