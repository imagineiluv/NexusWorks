using System;
using System.Threading;
using System.Threading.Tasks;
using SuperTutty.Services.Remote;

namespace SuperTutty.Tests.Remote;

internal sealed class FakeCommandRunner : IRemoteCommandRunner
{
    public string? LastCommand { get; private set; }
    public TimeSpan? LastTimeout { get; private set; }

    public Task<CommandResult> RunAsync(string command, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        LastCommand = command;
        LastTimeout = timeout;
        return Task.FromResult(new CommandResult(0, string.Empty, string.Empty));
    }

    public void Dispose()
    {
    }
}
