using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace SuperTutty.Services.Remote;

public sealed class PowerShellCommandRunner : IRemoteCommandRunner
{
    private readonly PowerShell _powerShell;
    private readonly Runspace? _runspace;
    private bool _disposed;

    public PowerShellCommandRunner(PowerShell? powerShell = null, WSManConnectionInfo? connectionInfo = null, Runspace? runspace = null)
    {
        // IMPORTANT:
        // If we host PowerShell in-proc without an explicit runspace, PowerShell can infer its
        // module/assembly base from the app's output directory. That can break built-in modules
        // (e.g., Microsoft.PowerShell.Diagnostics) by making it look for module assemblies next
        // to our app instead of the system PowerShell installation.
        //
        // To avoid this, always create an explicit runspace with a sane InitialSessionState.
        // - For remote WinRM, CreateRunspace(connectionInfo) already handles this.
        // - For local execution, CreateDefault2() uses the system module paths.
        _runspace = runspace
            ?? (connectionInfo != null
                ? RunspaceFactory.CreateRunspace(connectionInfo)
                : CreateLocalRunspaceForWindowsPowerShell());

        _runspace.Open();

        _powerShell = powerShell ?? PowerShell.Create();
        _powerShell.Runspace = _runspace;
    }

    private static Runspace CreateLocalRunspaceForWindowsPowerShell()
    {
        // In-proc hosting via the System.Management.Automation NuGet package is effectively
        // PowerShell (Core) runtime hosted inside our process. Some inbox modules in Windows
        // PowerShell 5.1 are implemented as snap-ins and won't be present as assemblies next
        // to our app (hence the "Microsoft.PowerShell.Commands.Diagnostics.dll" missing error).
        //
        // The practical fix: don't rely on those snap-in based modules in local mode.
        // CreateDefault2() yields a sane session for the hosted runtime.
        var iss = InitialSessionState.CreateDefault2();
        return RunspaceFactory.CreateRunspace(iss);
    }

    public Task<CommandResult> RunAsync(string command, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout.HasValue)
            {
                linkedCts.CancelAfter(timeout.Value);
            }

            _powerShell.Commands.Clear();
            _powerShell.Streams.Error.Clear();
            _powerShell.AddScript(command);

            var asyncResult = _powerShell.BeginInvoke();
            try
            {
                while (!asyncResult.IsCompleted)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromMilliseconds(50), linkedCts.Token).ConfigureAwait(false);
                }

                var output = _powerShell.EndInvoke(asyncResult);
                var stdOut = string.Join(Environment.NewLine, output.Select(o => o?.BaseObject?.ToString() ?? string.Empty));
                var stdErr = string.Join(Environment.NewLine, _powerShell.Streams.Error.Select(e => e?.ToString() ?? string.Empty));
                var exitCode = _powerShell.HadErrors ? 1 : 0;
                return new CommandResult(exitCode, stdOut, stdErr);
            }
            catch (OperationCanceledException)
            {
                _powerShell.Stop();
                throw;
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _powerShell.Dispose();
        _runspace?.Dispose();
        _disposed = true;
    }
}
