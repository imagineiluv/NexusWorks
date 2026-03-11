using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SuperTutty.Services.Remote;

public sealed class SshRemoteShell : RemoteShellBase
{
    public SshRemoteShell(IRemoteCommandRunner runner, ShellCapabilities? capabilities = null)
        : base(runner, capabilities ?? new ShellCapabilities { SupportsJsonOutput = true, SupportsRegexWithContext = true, UsesUtf8ByDefault = true })
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
        var contextSegment = BuildContextSegment(options.ContextLines);
        var escapedPattern = EscapePosix(pattern);
        var escapedPath = EscapePosix(path);

        if (Capabilities.SupportsJsonOutput)
        {
            var fixedSwitch = options.UseRegex ? string.Empty : " --fixed-strings";
            var ignoreCaseSwitch = options.IgnoreCase ? " --ignore-case" : " --case-sensitive";
            return $"rg --json --context {contextSegment}{ignoreCaseSwitch}{fixedSwitch} \"{escapedPattern}\" \"{escapedPath}\"";
        }

        var builder = new StringBuilder("grep -n --color=never");
        builder.Append($" -C {contextSegment}");
        builder.Append(options.IgnoreCase ? " -i" : string.Empty);
        builder.Append(options.UseRegex ? string.Empty : " -F");
        builder.Append($" \"{escapedPattern}\" \"{escapedPath}\"");
        return builder.ToString();
    }

    private string BuildFindCommand(string path, string namePattern, FindOptions options)
    {
        var depthSegment = options.MaxDepth.HasValue ? $"-maxdepth {options.MaxDepth.Value} " : string.Empty;
        var typeSegment = options.FilesOnly ? "-type f " : string.Empty;
        return $"find \"{EscapePosix(path)}\" {depthSegment}{typeSegment}-name \"{EscapePosix(namePattern)}\"".Trim();
    }
}
