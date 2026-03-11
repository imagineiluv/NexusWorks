using System.Threading;
using System.Threading.Tasks;

namespace SuperTutty.Services.Remote;

public interface IRemoteShell
{
    Task<CommandResult> SearchAsync(string path, string pattern, SearchOptions options, CancellationToken cancellationToken = default);
    Task<CommandResult> FindAsync(string path, string namePattern, FindOptions options, CancellationToken cancellationToken = default);
    Task<CommandResult> ExecuteAsync(string command, CancellationToken cancellationToken = default);
}
