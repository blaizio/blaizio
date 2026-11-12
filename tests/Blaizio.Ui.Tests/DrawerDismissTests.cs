using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// The drag gesture slides the panel out first and asks afterwards. The answer has to say whether
/// the drawer really closed: a parent that keeps it open was told "closed", and the drawer stayed
/// open off screen. Snap points are judged by the fractions the script can use.
/// </summary>
public sealed class DrawerDismissTests : BunitContext
{
    public DrawerDismissTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private IRenderedComponent<BzDrawer> RenderDrawer(
        Action<ComponentParameterCollectionBuilder<BzDrawer>> root,
        IReadOnlyList<double>? snapPoints = null) =>
        Render<BzDrawer>(p =>
        {
            root(p);
            p.AddChildContent<BzDrawerContent>(c => c
                .Add(x => x.SnapPoints, snapPoints)
                .AddChildContent("Body"));
        });

    [Fact]
    public async Task A_drag_dismissal_the_parent_refuses_is_answered_with_false()
    {
        // An unsaved-changes guard: the parent hears the request and keeps the drawer open.
        var cut = RenderDrawer(p => p.Add(x => x.Open, true).Add(x => x.OpenChanged, (bool _) => { }));
        var content = cut.FindComponent<BzDrawerContent>();

        var closed = await cut.InvokeAsync(() => content.Instance.OnDragDismiss());

        Assert.False(closed);
    }

    [Fact]
    public async Task A_drag_dismissal_that_closes_the_drawer_is_answered_with_true()
    {
        var cut = RenderDrawer(p => p.Add(x => x.DefaultOpen, true));
        var content = cut.FindComponent<BzDrawerContent>();

        var closed = await cut.InvokeAsync(() => content.Instance.OnDragDismiss());

        Assert.True(closed);
    }

    [Fact]
    public void Snap_points_with_no_usable_fraction_do_not_mark_the_drawer_as_snapping()
    {
        // Percentages where fractions are expected: the script keeps none of them, so the drawer
        // must open as a plain one. Marked as snapping it was parked off screen with nowhere to go.
        var plain = RenderDrawer(p => p.Add(x => x.DefaultOpen, true), [25, 50, 100]);
        Assert.Empty(plain.FindAll("[data-snap]"));

        var snapping = RenderDrawer(p => p.Add(x => x.DefaultOpen, true), [0.25, 0.5, 1]);
        Assert.NotEmpty(snapping.FindAll("[data-snap]"));
    }
}
