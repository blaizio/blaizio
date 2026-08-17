using Blaizio;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration for Blaizio - the single call an app needs.</summary>
public static class BlaizioServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything Blaizio needs from the container: the app-wide <see cref="BlaizioOptions"/>
    /// defaults (configured via <paramref name="configure"/>), the browser-side <see cref="ICore"/>
    /// services (reliable focus, key guards), the imperative <see cref="IDialogService"/>
    /// and <see cref="IToastService"/> APIs with the scoped <see cref="IDialogStore"/> /
    /// <see cref="IToastStore"/> they drive (rendered by a <c>DialogProvider</c> / <c>ToastProvider</c> at
    /// the app root), the <see cref="IThemeService"/> for programmatic theme / style / direction control,
    /// and the scoped <see cref="HoverCardCoordinator"/> that keeps at most one hover card open at a time.
    /// Scoped means per-circuit on Blazor Server and per-app on WebAssembly. Safe to call more than once -
    /// the first registration (and its options) wins.
    /// </summary>
    public static IServiceCollection AddBlaizio(this IServiceCollection services, Action<BlaizioOptions>? configure = null)
    {
        var options = new BlaizioOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        services.TryAddScoped<ICore, Core>();
        services.TryAddScoped<IDialogStore, DialogStore>();
        services.TryAddScoped<IDialogService, DialogService>();
        services.TryAddScoped<IToastStore, ToastStore>();
        services.TryAddScoped<IToastService, ToastService>();
        services.TryAddScoped<IThemeService, ThemeService>();
        services.TryAddScoped<HoverCardCoordinator>();
        return services;
    }
}
