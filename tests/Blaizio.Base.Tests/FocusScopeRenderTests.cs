using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The focus scope asks the app before it moves focus, on open and on close, and only when a
/// handler is bound: the script is told which handlers exist and calls back for those. These pin
/// that contract (the option names the script reads, the methods it calls) and the two earlier
/// parameter names, which stay as hidden forwarders.
/// </summary>
public class FocusScopeRenderTests : BunitContext
{
    private const string Module = "./_content/blaizio.base/dist/focusScope.js";

    private BunitJSModuleInterop SetupScript()
    {
        var module = JSInterop.SetupModule(Module);
        module.Mode = JSRuntimeMode.Loose;
        module.SetupModule(invocation => invocation.Identifier == "createFocusScope").Mode = JSRuntimeMode.Loose;
        return module;
    }

    // The options as the script receives them: interop serializes with camelCase names.
    private static JsonElement Options(BunitJSModuleInterop module)
    {
        var create = Assert.Single(module.Invocations, i => i.Identifier == "createFocusScope");
        return JsonSerializer.SerializeToElement(create.Arguments[2], new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Fact]
    public void The_script_is_told_which_auto_focus_handlers_are_bound()
    {
        var module = SetupScript();

        Render<BaseFocusScope>(p => p
            .Add(x => x.OnOpenAutoFocus, EventCallback.Factory.Create<FocusScopeEvent>(this, _ => { })));

        var options = Options(module);
        Assert.True(options.GetProperty("hasOpenAutoFocus").GetBoolean());
        Assert.False(options.GetProperty("hasCloseAutoFocus").GetBoolean());
    }

    [Fact]
    public async Task A_handler_that_prevents_the_default_is_reported_back_to_the_script()
    {
        SetupScript();
        var opened = 0;
        var closed = 0;
        var cut = Render<BaseFocusScope>(p => p
            .Add(x => x.OnOpenAutoFocus, EventCallback.Factory.Create<FocusScopeEvent>(this, e => { opened++; e.PreventDefault(); }))
            .Add(x => x.OnCloseAutoFocus, EventCallback.Factory.Create<FocusScopeEvent>(this, _ => closed++)));

        Assert.True(await cut.InvokeAsync(cut.Instance.HandleOpenAutoFocus));
        Assert.False(await cut.InvokeAsync(cut.Instance.HandleCloseAutoFocus));
        Assert.Equal((1, 1), (opened, closed));
    }

#pragma warning disable CS0618
    [Fact]
    public async Task The_earlier_parameter_names_still_bind_the_handlers()
    {
        var module = SetupScript();
        var opened = 0;
        var closed = 0;
        var cut = Render<BaseFocusScope>(p => p
            .Add(x => x.OnMountAutoFocus, EventCallback.Factory.Create<FocusScopeEvent>(this, _ => opened++))
            .Add(x => x.OnUnmountAutoFocus, EventCallback.Factory.Create<FocusScopeEvent>(this, e => { closed++; e.PreventDefault(); })));

        var options = Options(module);
        Assert.True(options.GetProperty("hasOpenAutoFocus").GetBoolean());
        Assert.True(options.GetProperty("hasCloseAutoFocus").GetBoolean());

        Assert.False(await cut.InvokeAsync(cut.Instance.HandleOpenAutoFocus));
        Assert.True(await cut.InvokeAsync(cut.Instance.HandleCloseAutoFocus));
        Assert.Equal((1, 1), (opened, closed));
    }
#pragma warning restore CS0618
}
