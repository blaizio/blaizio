using Blazeo.Ui;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration for the styled Blazeo.Ui services.</summary>
public static class BlazeoUiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the styled Blazeo.Ui services and the headless layer they build on - the scoped
    /// <see cref="IDialogService"/> (behind <c>&lt;DialogHost /&gt;</c>) and the scoped
    /// <see cref="IToastService"/> (behind <c>&lt;ToastProvider /&gt;</c>), plus their headless
    /// <see cref="Blazeo.IDialogStore"/> / <see cref="Blazeo.IToastStore"/>. Scoped means per-circuit on
    /// Blazor Server and per-app on WebAssembly. Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddBlazeoUi(this IServiceCollection services)
    {
        services.AddBlazeoBase();
        services.TryAddScoped<IDialogService, DialogService>();
        services.TryAddScoped<IToastService, ToastService>();
        return services;
    }
}
