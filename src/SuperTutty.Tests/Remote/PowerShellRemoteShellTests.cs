using System.Threading.Tasks;
using SuperTutty.Services.Remote;
using Xunit;

namespace SuperTutty.Tests.Remote;

public sealed class PowerShellRemoteShellTests
{
    [Fact]
    public async Task SearchAsync_UsesSelectStringWithExpectedFlags()
    {
        var runner = new FakeCommandRunner();
        var shell = new PowerShellRemoteShell(runner);

        await shell.SearchAsync("C:/logs", "timeout", new SearchOptions(IgnoreCase: false, ContextLines: 1));

        Assert.StartsWith("Select-String", runner.LastCommand);
        Assert.Contains("-CaseSensitive:$true", runner.LastCommand);
        Assert.Contains("-Context 1,1", runner.LastCommand);
    }

    [Fact]
    public async Task FindAsync_UsesDepthAndFilter()
    {
        var runner = new FakeCommandRunner();
        var shell = new PowerShellRemoteShell(runner);

        await shell.FindAsync("C:/logs", "*.log", new FindOptions(MaxDepth: 2, FilesOnly: false));

        Assert.Contains("-Depth 2", runner.LastCommand);
        Assert.Contains("-Filter '*.log'", runner.LastCommand);
        Assert.DoesNotContain("-File", runner.LastCommand);
    }
}
