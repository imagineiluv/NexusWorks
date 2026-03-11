using System.Threading.Tasks;
using SuperTutty.Services.Remote;
using Xunit;

namespace SuperTutty.Tests.Remote;

public sealed class SshRemoteShellTests
{
    [Fact]
    public async Task SearchAsync_UsesRipgrepWhenJsonSupported()
    {
        var runner = new FakeCommandRunner();
        var shell = new SshRemoteShell(runner, new ShellCapabilities { SupportsJsonOutput = true, SupportsRegexWithContext = true });

        await shell.SearchAsync("/var/log", "error", new SearchOptions(ContextLines: 2));

        Assert.Contains("rg --json", runner.LastCommand);
        Assert.Contains("--context 2", runner.LastCommand);
    }

    [Fact]
    public async Task SearchAsync_FallsBackToGrepWithoutJson()
    {
        var runner = new FakeCommandRunner();
        var shell = new SshRemoteShell(runner, new ShellCapabilities { SupportsJsonOutput = false, SupportsRegexWithContext = true });

        await shell.SearchAsync("/var/log", "warning", new SearchOptions(UseRegex: false, IgnoreCase: false));

        Assert.StartsWith("grep -n --color=never", runner.LastCommand);
        Assert.Contains("-F", runner.LastCommand);
        Assert.DoesNotContain("-i", runner.LastCommand);
    }

    [Fact]
    public async Task FindAsync_UsesDepthAndTypeFlags()
    {
        var runner = new FakeCommandRunner();
        var shell = new SshRemoteShell(runner);

        await shell.FindAsync("/var/log", "*.log", new FindOptions(MaxDepth: 3));

        Assert.Contains("-maxdepth 3", runner.LastCommand);
        Assert.Contains("-type f", runner.LastCommand);
    }
}
