using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using SuperTutty.Services;
using SuperTutty.Services.Remote;

namespace SuperTutty.UI.Services;

public interface IRemoteShellService
{
    Task<CommandResult> SearchAsync(SshSession session, string path, string pattern, SearchOptions options, CancellationToken cancellationToken = default);

    Task<CommandResult> FindAsync(SshSession session, string path, string namePattern, FindOptions options, CancellationToken cancellationToken = default);

    Task<CommandResult> ExecuteAsync(SshSession session, string command, CancellationToken cancellationToken = default);

    Task<bool> CheckConnectionAsync(SshSession session, CancellationToken cancellationToken = default);
}

internal sealed class RemoteShellService : IRemoteShellService
{
    public async Task<CommandResult> SearchAsync(SshSession session, string path, string pattern, SearchOptions options, CancellationToken cancellationToken = default)
    {
        await using var shell = await CreateShellAsync(session).ConfigureAwait(false);
        return await shell.SearchAsync(path, pattern, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CommandResult> FindAsync(SshSession session, string path, string namePattern, FindOptions options, CancellationToken cancellationToken = default)
    {
        await using var shell = await CreateShellAsync(session).ConfigureAwait(false);
        return await shell.FindAsync(path, namePattern, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CommandResult> ExecuteAsync(SshSession session, string command, CancellationToken cancellationToken = default)
    {
        await using var shell = await CreateShellAsync(session).ConfigureAwait(false);
        return await shell.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CheckConnectionAsync(SshSession session, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            var probeCommand = session.IsWindows
                ? "Write-Output \"supertutty-remote-shell\""
                : "echo \"supertutty-remote-shell\"";

            var result = await ExecuteAsync(session, probeCommand, timeout.Token).ConfigureAwait(false);
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    private static Task<RemoteShellHandle> CreateShellAsync(SshSession session)
    {
        // Local mode for Linux/macOS-style sessions: run a local POSIX shell (no SSH).
        if (!session.IsWindows && session.Platform == SessionPlatform.Linux && IsLocalPosixTarget(session.Host))
        {
            var runner = new PosixShellProcessCommandRunner();
            var shell = new SshRemoteShell(runner);
            return Task.FromResult(new RemoteShellHandle(shell, runner));
        }

        if (session.IsWindows)
        {
            // Local mode: run PowerShell in a local runspace (no WinRM).
            if (IsLocalWindowsTarget(session.Host))
            {
                var runner = new WindowsPowerShellProcessCommandRunner();
                var shell = new PowerShellRemoteShell(runner);
                return Task.FromResult(new RemoteShellHandle(shell, runner));
            }

            // Remote mode: use WSMan (WinRM).
            var connectionInfo = BuildConnectionInfo(session);
            var remoteRunner = new PowerShellCommandRunner(connectionInfo: connectionInfo);
            var remoteShell = new PowerShellRemoteShell(remoteRunner);
            return Task.FromResult(new RemoteShellHandle(remoteShell, remoteRunner));
        }

        var client = new SshClient(session.Host, session.Port, session.Username, session.Password);
        var sshRunner = new SshCommandRunner(client);
        var sshShell = new SshRemoteShell(sshRunner);
        return Task.FromResult(new RemoteShellHandle(sshShell, sshRunner));
    }

    private static bool IsLocalWindowsTarget(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalized = host.Trim();
        return string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, ".", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalPosixTarget(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalized = host.Trim();
        return string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, ".", StringComparison.OrdinalIgnoreCase);
    }

    private static WSManConnectionInfo BuildConnectionInfo(SshSession session)
    {
        var securePassword = new SecureString();
        foreach (var c in session.Password ?? string.Empty)
        {
            securePassword.AppendChar(c);
        }
        securePassword.MakeReadOnly();

        var credential = new PSCredential(session.Username, securePassword);
        var connectionUri = new Uri($"http://{session.Host}:{session.Port}/wsman");

        var info = new WSManConnectionInfo(connectionUri, "http://schemas.microsoft.com/powershell/Microsoft.PowerShell", credential)
        {
            OperationTimeout = 2 * 60 * 1000,
            OpenTimeout = 30 * 1000,

            // These are meaningful for HTTPS (5986) and harmless for HTTP.
            SkipCACheck = true,
            SkipCNCheck = true,
            SkipRevocationCheck = true
        };

        // For non-domain / non-Kerberos scenarios, defaulting to Negotiate is more flexible.
        // It can fall back to NTLM when Kerberos isn't available.
        info.AuthenticationMechanism = AuthenticationMechanism.Negotiate;

        // Keep proxy auth from interfering in some corp environments.
        info.ProxyAuthentication = AuthenticationMechanism.Negotiate;

        return info;
    }
}

internal sealed class RemoteShellHandle : IRemoteShell, IAsyncDisposable
{
    private readonly IRemoteShell _inner;
    private readonly IRemoteCommandRunner _runner;

    public RemoteShellHandle(IRemoteShell inner, IRemoteCommandRunner runner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public Task<CommandResult> SearchAsync(string path, string pattern, SearchOptions options, CancellationToken cancellationToken = default)
    {
        return _inner.SearchAsync(path, pattern, options, cancellationToken);
    }

    public Task<CommandResult> FindAsync(string path, string namePattern, FindOptions options, CancellationToken cancellationToken = default)
    {
        return _inner.FindAsync(path, namePattern, options, cancellationToken);
    }

    public Task<CommandResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        return _inner.ExecuteAsync(command, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _runner.Dispose();
        return ValueTask.CompletedTask;
    }
}
