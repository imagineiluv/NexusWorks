using System;
using System.Threading;
using System.Threading.Tasks;

namespace SuperTutty.Services.Remote;

public abstract class RemoteShellBase : IRemoteShell
{
    protected RemoteShellBase(IRemoteCommandRunner runner, ShellCapabilities capabilities)
    {
        Runner = runner ?? throw new ArgumentNullException(nameof(runner));
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    protected IRemoteCommandRunner Runner { get; }

    protected ShellCapabilities Capabilities { get; }

    public abstract Task<CommandResult> SearchAsync(string path, string pattern, SearchOptions options, CancellationToken cancellationToken = default);

    public abstract Task<CommandResult> FindAsync(string path, string namePattern, FindOptions options, CancellationToken cancellationToken = default);

    public virtual Task<CommandResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        EnsureCommand(command);
        return Runner.RunAsync(command, null, cancellationToken);
    }

    protected static void EnsureValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must be provided.", nameof(path));
        }
    }

    protected static void EnsurePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("Pattern must be provided.", nameof(pattern));
        }
    }

    protected static void EnsureCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command must be provided.", nameof(command));
        }
    }

    protected static string EscapePosix(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    protected static string EscapePowerShell(string value)
    {
        return value.Replace("'", "''");
    }

    protected static string BuildContextSegment(int? contextLines)
    {
        return (contextLines ?? 0) switch
        {
            > 0 and var lines => lines.ToString(),
            _ => "0"
        };
    }
}
