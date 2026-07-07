namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// Wires Ctrl+C to a process-wide <see cref="CancellationToken"/> commands thread through Core.
/// First press cancels gracefully (registry fetches abort, child processes are killed); a second
/// press falls through to the runtime's hard exit.
/// </summary>
internal static class CliCancellation
{
    private static readonly CancellationTokenSource Source = new();
    private static int _presses;

    /// <summary>The token every long-running command operation should observe.</summary>
    public static CancellationToken Token => Source.Token;

    /// <summary>Hook <see cref="Console.CancelKeyPress"/>. Call once at startup.</summary>
    public static void Install() =>
        Console.CancelKeyPress += (_, e) =>
        {
            // Graceful on the first press only; let the second one kill the process.
            e.Cancel = Interlocked.Increment(ref _presses) == 1;
            Source.Cancel();
        };
}
