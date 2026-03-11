using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SuperTutty.Services.Remote;

/// <summary>
/// Executes commands via Windows PowerShell 5.1 (powershell.exe) in a child process.
/// This is used for Windows(Local) sessions to ensure inbox modules and cmdlets
/// (e.g., Microsoft.PowerShell.Utility) are available.
/// </summary>
public sealed class WindowsPowerShellProcessCommandRunner : IRemoteCommandRunner
{
    private bool _disposed;

    public async Task<CommandResult> RunAsync(string command, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsPowerShellProcessCommandRunner));
        }

        using var timeoutCts = timeout.HasValue
            ? new CancellationTokenSource(timeout.Value)
            : new CancellationTokenSource();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        // Use -EncodedCommand to avoid quoting/escaping issues.
        // Windows PowerShell expects UTF-16LE for encoded commands.
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command ?? string.Empty));

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

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
