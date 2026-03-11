using System;
using System.Threading;
using System.Threading.Tasks;

namespace SuperTutty.Services.Remote;

public interface IRemoteCommandRunner : IDisposable
{
    Task<CommandResult> RunAsync(string command, TimeSpan? timeout, CancellationToken cancellationToken);
}
