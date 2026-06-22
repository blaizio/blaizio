using Microsoft.AspNetCore.Components;

namespace Blazeo.Ui;

/// <summary>
/// The imperative toast API: raise notifications from C# anywhere. Requires a single
/// <c>&lt;Toaster /&gt;</c> at the app root and <c>AddBlazeoUi()</c> in DI. Every method returns the
/// toast's id, which can be passed back via <see cref="ToastOptions.Id"/> to update it, or to
/// <see cref="Dismiss"/> to close it.
/// </summary>
public interface IToastService
{
    /// <summary>Shows a plain toast.</summary>
    string Show(string title, ToastOptions? options = null);

    /// <summary>Shows a toast with fully custom content as its body.</summary>
    string Show(RenderFragment content, ToastOptions? options = null);

    /// <summary>Shows a plain toast (alias of <see cref="Show(string, ToastOptions)"/>).</summary>
    string Message(string title, ToastOptions? options = null);

    /// <summary>Shows a success toast (green check).</summary>
    string Success(string title, ToastOptions? options = null);

    /// <summary>Shows an informational toast.</summary>
    string Info(string title, ToastOptions? options = null);

    /// <summary>Shows a warning toast.</summary>
    string Warning(string title, ToastOptions? options = null);

    /// <summary>Shows an error toast.</summary>
    string Error(string title, ToastOptions? options = null);

    /// <summary>Shows a persistent loading toast (spinner) - dismiss or update it when the work finishes.</summary>
    string Loading(string title, ToastOptions? options = null);

    /// <summary>
    /// Runs <paramref name="action"/> behind a single toast: a loading spinner while it's in flight,
    /// then the success or error message in place. Exceptions are surfaced as the error toast and
    /// swallowed (the toast is the feedback), so this never throws.
    /// </summary>
    Task Promise<T>(Func<Task<T>> action, PromiseToastOptions<T> options);

    /// <summary>Dismisses the toast with <paramref name="id"/>, or every toast when it is <see langword="null"/>.</summary>
    void Dismiss(string? id = null);
}
