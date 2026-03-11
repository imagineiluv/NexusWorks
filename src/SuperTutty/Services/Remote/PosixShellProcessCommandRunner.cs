using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SuperTutty.Services.Remote;

/// <summary>
/// Executes commands via a local POSIX shell (bash/sh) in a child process.
/// Used for Linux/macOS local sessions (e.g., host=localhost) to avoid SSH.
/// </summary>
public sealed class PosixShellProcessCommandRunner : IRemoteCommandRunner
{
    private bool _disposed;

    public async Task<CommandResult> RunAsync(string command, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PosixShellProcessCommandRunner));
        }

        using var timeoutCts = timeout.HasValue
            ? new CancellationTokenSource(timeout.Value)
            : new CancellationTokenSource();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var shellPath = File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";

        var startInfo = new ProcessStartInfo
        {
            FileName = shellPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // -l: login shell (best-effort for PATH), -c: execute command
        startInfo.ArgumentList.Add("-lc");
        startInfo.ArgumentList.Add(command ?? string.Empty);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new CommandResult(1, string.Empty, ex.Message);
        }

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        return new CommandResult(process.ExitCode, stdOut?.TrimEnd() ?? string.Empty, stdErr?.TrimEnd() ?? string.Empty);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best-effort
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
