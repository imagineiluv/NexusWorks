using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Renci.SshNet;

namespace SuperTutty.Services.Remote;

public static class RemoteShellFactory
{
    public static IRemoteShell CreateLinuxShell(SshClient client, ShellCapabilities? capabilities = null)
    {
        var runner = new SshCommandRunner(client);
        return new SshRemoteShell(runner, capabilities);
    }

    public static IRemoteShell CreateWindowsShell(PowerShell? powerShell = null, ShellCapabilities? capabilities = null, WSManConnectionInfo? connectionInfo = null)
    {
        var runner = new PowerShellCommandRunner(powerShell, connectionInfo);
        return new PowerShellRemoteShell(runner, capabilities);
    }
}
