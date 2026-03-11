using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SuperTutty.Services.Remote;

public sealed class PowerShellRemoteShell : RemoteShellBase
{
    public PowerShellRemoteShell(IRemoteCommandRunner runner, ShellCapabilities? capabilities = null)
        : base(runner, capabilities ?? new ShellCapabilities { SupportsJsonOutput = false, SupportsRegexWithContext = true, UsesUtf8ByDefault = true })
    {
    }

    public override Task<CommandResult> SearchAsync(string path, string pattern, SearchOptions options, CancellationToken cancellationToken = default)
    {
        EnsureValidPath(path);
        EnsurePattern(pattern);
        ArgumentNullException.ThrowIfNull(options);

        var command = BuildSearchCommand(path, pattern, options);
        return Runner.RunAsync(command, options.Timeout, cancellationToken);
    }

    public override Task<CommandResult> FindAsync(string path, string namePattern, FindOptions options, CancellationToken cancellationToken = default)
    {
        EnsureValidPath(path);
        EnsurePattern(namePattern);
        ArgumentNullException.ThrowIfNull(options);

        var command = BuildFindCommand(path, namePattern, options);
        return Runner.RunAsync(command, options.Timeout, cancellationToken);
    }

    public override Task<CommandResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        return base.ExecuteAsync(command, cancellationToken);
    }

    private string BuildSearchCommand(string path, string pattern, SearchOptions options)
    {
        var builder = new StringBuilder("Select-String");
        builder.Append($" -Path '{EscapePowerShell(path)}'");
        builder.Append($" -Pattern '{EscapePowerShell(pattern)}'");
        builder.Append(options.UseRegex ? string.Empty : " -SimpleMatch");
        builder.Append(options.IgnoreCase ? " -CaseSensitive:$false" : " -CaseSensitive:$true");

        var contextSegment = BuildContextSegment(options.ContextLines);
        if (int.TryParse(contextSegment, out var context) && context > 0)
        {
            builder.Append($" -Context {context},{context}");
        }

        builder.Append(" -Encoding UTF8");
        return builder.ToString();
    }

    private string BuildFindCommand(string path, string namePattern, FindOptions options)
    {
        var builder = new StringBuilder("Get-ChildItem");
        builder.Append($" -Path '{EscapePowerShell(path)}' -Recurse");
        builder.Append(options.FilesOnly ? " -File" : string.Empty);

        if (options.MaxDepth.HasValue)
        {
            builder.Append($" -Depth {options.MaxDepth.Value}");
        }

        builder.Append($" -Filter '{EscapePowerShell(namePattern)}'");
        builder.Append(" | Select-Object -ExpandProperty FullName");
        return builder.ToString();
    }
}
