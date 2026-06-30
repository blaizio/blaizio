using Blaizio;
using Microsoft.AspNetCore.Components;

namespace Blaizio.Ui;

/// <inheritdoc cref="IToastService"/>
public sealed class ToastService(IToastStore store) : IToastService
{
    /// <inheritdoc/>
    public string Show(string title, ToastOptions? options = null) =>
        store.Show(Build(title, null, ToastType.Normal, options), options?.Id);

    /// <inheritdoc/>
    public string Show(RenderFragment content, ToastOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return store.Show(Build(null, content, ToastType.Normal, options), options?.Id);
    }

    /// <inheritdoc/>
    public string Message(string title, ToastOptions? options = null) => Show(title, options);

    /// <inheritdoc/>
    public string Success(string title, ToastOptions? options = null) =>
        store.Show(Build(title, null, ToastType.Success, options), options?.Id);

    /// <inheritdoc/>
    public string Info(string title, ToastOptions? options = null) =>
        store.Show(Build(title, null, ToastType.Info, options), options?.Id);

    /// <inheritdoc/>
    public string Warning(string title, ToastOptions? options = null) =>
        store.Show(Build(title, null, ToastType.Warning, options), options?.Id);

    /// <inheritdoc/>
    public string Error(string title, ToastOptions? options = null) =>
        store.Show(Build(title, null, ToastType.Error, options), options?.Id);

    /// <inheritdoc/>
    public string Loading(string title, ToastOptions? options = null) =>
        store.Show(Build(title, null, ToastType.Loading, options), options?.Id);

    /// <inheritdoc/>
    public async Task Track<T>(Func<Task<T>> operation, ToastTrackOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(options);

        // One toast, updated in place: loading -> success / error (the shared id is the hinge).
        var id = store.Show(Build(options.Loading, null, ToastType.Loading, new ToastOptions { Position = options.Position }));
        try
        {
            var result = await operation();
            var message = options.Success?.Invoke(result) ?? "Success";
            store.Show(Build(message, null, ToastType.Success, new ToastOptions { Position = options.Position }), id);
        }
        catch (Exception ex)
        {
            var message = options.Error?.Invoke(ex) ?? "Something went wrong";
            store.Show(Build(message, null, ToastType.Error, new ToastOptions { Position = options.Position }), id);
        }
    }

    /// <inheritdoc/>
    public void Dismiss(string? id = null) => store.Dismiss(id);

    private static ToastData Build(string? title, RenderFragment? content, ToastType type, ToastOptions? o) => new()
    {
        Type = type,
        Title = title,
        TitleContent = content,
        Description = o?.Description,
        DescriptionContent = o?.DescriptionContent,
        Duration = o?.Duration,
        Action = o?.Action,
        Cancel = o?.Cancel,
        CloseButton = o?.CloseButton,
        Dismissible = o?.Dismissible ?? true,
        RichColors = o?.RichColors,
        Position = o?.Position,
        Direction = o?.Direction,
        Icon = o?.Icon is { } icon ? IconFragment(icon) : null,
        ShowIcon = o?.ShowIcon ?? true,
        Persistent = o?.Persistent ?? false,
        OnDismiss = o?.OnDismiss,
        OnAutoClose = o?.OnAutoClose,
    };

    // Wraps a Blaizio.Icons value as the headless layer's icon-agnostic RenderFragment slot.
    private static RenderFragment IconFragment(Icon icon) => builder =>
    {
        builder.OpenComponent<BzIcon>(0);
        builder.AddComponentParameter(1, nameof(BzIcon.Icon), icon);
        builder.CloseComponent();
    };
}
