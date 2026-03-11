using System;

namespace SuperTutty.Services.Remote;

public sealed record SearchOptions(bool IgnoreCase = true, bool UseRegex = true, int? ContextLines = null, TimeSpan? Timeout = null);

public sealed record FindOptions(int? MaxDepth = null, bool FilesOnly = true, TimeSpan? Timeout = null);

public sealed record CommandResult(int ExitCode, string StdOut, string StdErr)
{
    public bool IsSuccess => ExitCode == 0;
}
