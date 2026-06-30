using Blaizio.Ui;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration for the styled Blaizio.Ui services.</summary>
public static class BlaizioUiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the styled Blaizio.Ui services and the headless layer they build on - the scoped
    /// <see cref="IDialogService"/> (behind <c>&lt;DialogProvider /&gt;</c>) and the scoped
    /// <see cref="IToastService"/> (behind <c>&lt;ToastProvider /&gt;</c>), plus their headless
    /// <see cref="Blaizio.IDialogStore"/> / <see cref="Blaizio.IToastStore"/>. Scoped means per-circuit on
    /// Blazor Server and per-app on WebAssembly. Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddBlaizioUi(this IServiceCollection services)
    {
        services.AddBlaizioBase();
        services.TryAddScoped<IDialogService, DialogService>();
        services.TryAddScoped<IToastService, ToastService>();
        return services;
    }
}
