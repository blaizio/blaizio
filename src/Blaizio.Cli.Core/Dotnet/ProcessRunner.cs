using System.Diagnostics;
using System.Text;

namespace Blaizio.Cli.Core.Dotnet;

/// <summary>The captured result of running an external process.</summary>
public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    /// <summary>True when the process exited zero.</summary>
    public bool Success => ExitCode == 0;
}

/// <summary>Runs external processes and captures their output. Thin wrapper over <see cref="Process"/>.</summary>
public static class ProcessRunner
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

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start '{fileName}'.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
