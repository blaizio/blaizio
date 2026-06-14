using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render tests for the headless <see cref="BlazeDialogHost"/>: the end-to-end presence-aware
/// lifecycle of an imperatively shown dialog (open -> close -> exit animation -> removal), stacking,
/// and the load-bearing skin contract that a forgotten OnExitComplete leaks the entry. JSInterop is
/// Loose so the presence/scroll-lock module imports are no-ops; the animationend callback is
/// simulated by invoking OnCloseFinished, as in <see cref="DialogRenderTests"/>.
/// </summary>
public class DialogHostRenderTests : TestContext
{
    public DialogHostRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static readonly RenderFragment Body = b => b.AddContent(0, "Body");

    // A minimal test skin: a controlled BlazeDialog wrapping BlazeDialogContent, mirroring the real Ui
    // DialogHost. With wireExit=false it "forgets" to wire OnExitComplete, to prove the leak.
    private static RenderFragment<DialogHostContext> Skin(bool wireExit = true) => ctx => builder =>
    {
        var instance = ctx.Instance;
        builder.OpenComponent<BlazeDialog>(0);
        builder.AddComponentParameter(1, nameof(BlazeDialog.Open), instance.IsOpen);
        builder.AddComponentParameter(2, nameof(BlazeDialog.ChildContent), (RenderFragment)(content =>
        {
            content.OpenComponent<BlazeDialogContent>(0);
            if (wireExit)
                content.AddComponentParameter(1, nameof(BlazeDialogContent.OnExitComplete), ctx.OnExitComplete);
            content.AddComponentParameter(2, nameof(BlazeDialogContent.ChildContent), instance.Content);
            content.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task Renders_an_open_dialog_then_removes_it_after_the_exit_animation()
    {
        var store = new DialogStore();
        Services.AddSingleton<IDialogStore>(store);
        var cut = RenderComponent<BlazeDialogHost>(p => p.Add(x => x.ChildContent, Skin()));

        Assert.Empty(cut.FindAll("[role=dialog]"));

        Task<DialogResult> result = default!;
        await cut.InvokeAsync(() => { result = store.OpenAsync(_ => Body, new DialogOptions()); });
        Assert.Single(cut.FindAll("[role=dialog]"));

        var instance = store.Instances[0];
        await cut.InvokeAsync(() => instance.Close(DialogResult.Ok(42)));

        // The caller's await resumes immediately, while the dialog is still animating out.
        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(42, (await result).ValueAs<int>());
        Assert.Equal("closed", cut.Find("[role=dialog]").GetAttribute("data-state"));
        Assert.Single(store.Instances);

        // animationend -> OnExitComplete -> NotifyExited -> the store drops it and the host unmounts.
        await cut.InvokeAsync(() => cut.FindComponent<BlazeDialogContent>().Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=dialog]"));
        Assert.Empty(store.Instances);
    }

    [Fact]
    public async Task Leaks_the_instance_when_the_skin_forgets_OnExitComplete()
    {
        var store = new DialogStore();
        Services.AddSingleton<IDialogStore>(store);
        var cut = RenderComponent<BlazeDialogHost>(p => p.Add(x => x.ChildContent, Skin(wireExit: false)));

        await cut.InvokeAsync(() => { store.OpenAsync(_ => Body, new DialogOptions()); });
        var instance = store.Instances[0];
        await cut.InvokeAsync(() => instance.Close(DialogResult.Cancel()));
        await cut.InvokeAsync(() => cut.FindComponent<BlazeDialogContent>().Instance.OnCloseFinished());

        // The content unmounted itself, but with no OnExitComplete wired the host never learns -> leak.
        Assert.Empty(cut.FindAll("[role=dialog]"));
        Assert.Single(store.Instances);
    }

    [Fact]
    public async Task Stacks_two_dialogs_concurrently()
    {
        var store = new DialogStore();
        Services.AddSingleton<IDialogStore>(store);
        var cut = RenderComponent<BlazeDialogHost>(p => p.Add(x => x.ChildContent, Skin()));

        await cut.InvokeAsync(() => { store.OpenAsync(_ => Body, new DialogOptions()); });
        await cut.InvokeAsync(() => { store.OpenAsync(_ => Body, new DialogOptions()); });

        Assert.Equal(2, cut.FindAll("[role=dialog]").Count);
        Assert.Equal(2, store.Instances.Count);
    }
}
