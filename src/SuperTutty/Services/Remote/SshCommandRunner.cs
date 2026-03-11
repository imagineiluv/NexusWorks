using System;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace SuperTutty.Services.Remote;

public sealed class SshCommandRunner : IRemoteCommandRunner
{
    private readonly SshClient _client;

    public SshCommandRunner(SshClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<CommandResult> RunAsync(string command, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_client.IsConnected)
        {
            _client.Connect();
        }

        var sshCommand = _client.CreateCommand(command);
        if (timeout.HasValue)
        {
            sshCommand.CommandTimeout = timeout.Value;
        }

        return Task.Run(() => ExecuteCommand(sshCommand, cancellationToken), cancellationToken);
    }

    private static CommandResult ExecuteCommand(SshCommand sshCommand, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var output = sshCommand.Execute();
        var exitCode = sshCommand.ExitStatus ?? 0;
        var stdErr = sshCommand.Error ?? string.Empty;
        return new CommandResult(exitCode, output ?? string.Empty, stdErr);
    }

    public void Dispose()
    {
        if (_client.IsConnected)
        {
            _client.Disconnect();
        }

        _client.Dispose();
    }
}
