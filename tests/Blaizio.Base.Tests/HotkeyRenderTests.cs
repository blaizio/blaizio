using Bunit;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// A disabled hotkey has to be off in the script as well as in C#. The script is what prevents the
/// browser's default for the combo, so a hotkey that was only ignored in C# still left the key
/// dead on the page.
/// </summary>
public class HotkeyRenderTests : BunitContext
{
    [Fact]
    public void Disabled_reaches_the_script_at_creation_and_on_every_change()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/hotkeys.js");
        module.Mode = JSRuntimeMode.Loose;
        var instance = module.SetupModule(invocation => invocation.Identifier == "createHotkey");
        instance.Mode = JSRuntimeMode.Loose;

        var cut = Render<BaseHotkey>(p => p.Add(x => x.Combo, "mod+k").Add(x => x.Disabled, true));

        // createHotkey(combo, ref, preventDefault, skipContentEditable, enabled)
        var create = Assert.Single(module.Invocations, i => i.Identifier == "createHotkey");
        Assert.Equal(false, create.Arguments[4]);
        Assert.DoesNotContain(instance.Invocations, i => i.Identifier == "setEnabled");

        cut.Render(p => p.Add(x => x.Disabled, false));
        var enable = Assert.Single(instance.Invocations, i => i.Identifier == "setEnabled");
        Assert.Equal(true, enable.Arguments[0]);

        cut.Render(p => p.Add(x => x.Disabled, true));
        Assert.Equal(false, instance.Invocations.Last(i => i.Identifier == "setEnabled").Arguments[0]);
    }
}
