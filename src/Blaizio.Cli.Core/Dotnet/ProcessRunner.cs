using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Blaizio.Cli.Core.Dotnet;

/// <summary>The captured result of running an external process.</summary>
public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    /// <summary>True when the process exited zero.</summary>
    public bool Success => ExitCode == 0;

    /// <summary>
    /// The most useful error text for a failed run: stderr and stdout merged (tools like
    /// <c>dotnet add package</c> report most errors on stdout).
    /// </summary>
    public string ErrorText
    {
        get
        {
            var err = StdErr.Trim();
            var outText = StdOut.Trim();
            return (err.Length, outText.Length) switch
            {
                (0, _) => outText,
                (_, 0) => err,
                _ => $"{err}{Environment.NewLine}{outText}",
            };
        }
    }
}

/// <summary>Runs external processes and captures their output. Thin wrapper over <see cref="Process"/>.</summary>
internal static class ProcessRunner
{
    /// <summary>Run <paramref name="fileName"/> with <paramref name="arguments"/> in <paramref name="workingDir"/>.</summary>
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDir,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Failed to start '{fileName}'.");
        }
        catch (Win32Exception ex)
        {
            // The executable isn't on PATH (or isn't runnable) — surface a friendly error instead
            // of the raw OS one.
            throw new InvalidOperationException(
                $"Could not run '{fileName}' - is it installed and on PATH? ({ex.Message})", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Don't leave an orphaned child running after a cancel.
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Exited between the check and the kill — nothing to clean up.
            }
            throw;
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
