using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SuperTutty.Services;

public enum CommandPlatform
{
    Linux,
    Windows
}

public sealed class CommandPreset
{
    public CommandPreset(CommandPlatform platform, string name, string command, string description, string? caution = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Command preset name cannot be null or empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command text cannot be null or empty.", nameof(command));
        }

        Platform = platform;
        Name = name;
        Command = command;
        Description = description;
        Caution = caution;
    }

    public CommandPlatform Platform { get; }

    public string Name { get; }

    public string Command { get; }

    public string Description { get; }

    public string? Caution { get; }
}

public interface ICommandCatalog
{
    IReadOnlyCollection<CommandPreset> Presets { get; }

    IReadOnlyList<CommandPreset> GetByPlatform(CommandPlatform platform);
}

public sealed class CommandCatalog : ICommandCatalog
{
    private readonly IReadOnlyCollection<CommandPreset> _presets;

    public CommandCatalog()
    {
        _presets = BuildDefaults();
    }

    public IReadOnlyCollection<CommandPreset> Presets => _presets;

    public IReadOnlyList<CommandPreset> GetByPlatform(CommandPlatform platform)
    {
        return _presets
            .Where(preset => preset.Platform == platform)
            .OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyCollection<CommandPreset> BuildDefaults()
    {
        return new List<CommandPreset>
        {
            // Linux - File & Directory
            new(CommandPlatform.Linux, "List files", "ls -la", "Inspect current directory with permissions and hidden entries."),
            new(CommandPlatform.Linux, "Find files by name", "find . -name '*.log'", "Search files by name pattern recursively.", "Replace *.log with your target pattern."),
            new(CommandPlatform.Linux, "Find files by size", "find . -size +100M", "Find files larger than 100MB in current directory."),
            new(CommandPlatform.Linux, "Find recent files", "find . -mtime -1", "Find files modified in the last 24 hours."),
            new(CommandPlatform.Linux, "Locate file", "locate <filename>", "Fast file search using system index.", "Run 'sudo updatedb' first if results are outdated."),
            
            // Linux - Text Search
            new(CommandPlatform.Linux, "Grep in files", "grep -rn 'pattern' .", "Search text pattern recursively with line numbers."),
            new(CommandPlatform.Linux, "Grep with context", "grep -rn -B2 -A2 'pattern' .", "Search with 2 lines before/after match."),
            new(CommandPlatform.Linux, "Grep in logs", "grep -i 'error' /var/log/syslog | tail -20", "Find errors in syslog (case-insensitive)."),
            new(CommandPlatform.Linux, "Grep exclude", "grep -rn --exclude-dir={node_modules,.git} 'pattern' .", "Search excluding specific directories."),
            new(CommandPlatform.Linux, "Awk filter", "awk '/pattern/ {print $1, $2}' file.txt", "Extract specific columns from matching lines."),
            
            // Linux - Process Management
            new(CommandPlatform.Linux, "Find process by name", "ps aux | grep <process>", "Search running processes by name.", "Replace <process> with target process name."),
            new(CommandPlatform.Linux, "Find PID by port", "sudo lsof -i :<port>", "Find process using a specific port.", "Replace <port> with actual port number."),
            new(CommandPlatform.Linux, "Find PID by name", "pgrep -f <pattern>", "Get PID of process matching pattern."),
            new(CommandPlatform.Linux, "Process tree", "ps -ef --forest | head", "Show top processes with hierarchy for quick inspection."),
            new(CommandPlatform.Linux, "Top processes CPU", "ps aux --sort=-%cpu | head -10", "Top 10 CPU consuming processes."),
            new(CommandPlatform.Linux, "Top processes memory", "ps aux --sort=-%mem | head -10", "Top 10 memory consuming processes."),
            new(CommandPlatform.Linux, "Kill by PID", "kill -9 <pid>", "Force kill a process by PID.", "Use with caution - data loss possible."),
            new(CommandPlatform.Linux, "Kill by name", "pkill -f <pattern>", "Kill all processes matching pattern.", "Verify pattern matches only intended processes."),
            
            // Linux - System Resources
            new(CommandPlatform.Linux, "Disk usage", "df -h", "View mounted volumes with human-readable sizes."),
            new(CommandPlatform.Linux, "Directory size", "du -sh *", "Show size of each item in current directory."),
            new(CommandPlatform.Linux, "Memory usage", "free -h", "Check RAM consumption and swap status."),
            new(CommandPlatform.Linux, "CPU info", "lscpu | head -15", "Display CPU architecture and cores info."),
            new(CommandPlatform.Linux, "System uptime", "uptime", "Show system uptime and load averages."),
            
            // Linux - Network
            new(CommandPlatform.Linux, "Network summary", "ip -brief address", "Summarize network interfaces and IP assignments."),
            new(CommandPlatform.Linux, "Port check", "ss -tulpn | head", "List listening ports and owning processes."),
            new(CommandPlatform.Linux, "Active connections", "netstat -an | grep ESTABLISHED", "Show established network connections."),
            new(CommandPlatform.Linux, "DNS lookup", "nslookup <domain>", "Query DNS for domain information."),
            new(CommandPlatform.Linux, "Ping host", "ping -c 4 <host>", "Test network connectivity to a host."),
            
            // Linux - Logs & Services
            new(CommandPlatform.Linux, "Tail syslog", "sudo tail -f /var/log/syslog", "Stream system log entries live.", "Requires sudo privileges on most distros."),
            new(CommandPlatform.Linux, "Journal errors", "journalctl -p err -b", "Show error-level journal entries since boot."),
            new(CommandPlatform.Linux, "Service status", "sudo systemctl status <service>", "Check status of a systemd service."),
            new(CommandPlatform.Linux, "Restart service", "sudo systemctl restart <service>", "Restart a systemd service with substitution.", "Replace <service> with an actual unit name to avoid errors."),
            new(CommandPlatform.Linux, "Package updates", "sudo apt update && sudo apt upgrade", "Refresh apt indexes and upgrade packages.", "Confirm distro uses apt before running."),
            
            // Windows - File & Directory
            new(CommandPlatform.Windows, "List files", "dir", "Show files and folders in the current directory."),
            new(CommandPlatform.Windows, "Find files by name", "Get-ChildItem -Recurse -Filter '*.log'", "Search files by name pattern recursively."),
            new(CommandPlatform.Windows, "Find files by size", "Get-ChildItem -Recurse | Where-Object { $_.Length -gt 100MB }", "Find files larger than 100MB."),
            new(CommandPlatform.Windows, "Find recent files", "Get-ChildItem -Recurse | Where-Object { $_.LastWriteTime -gt (Get-Date).AddDays(-1) }", "Find files modified in the last 24 hours."),
            new(CommandPlatform.Windows, "Where command", "where.exe <command>", "Find location of executable in PATH."),
            
            // Windows - Text Search
            new(CommandPlatform.Windows, "Select-String", "Select-String -Path *.log -Pattern 'error'", "Search text pattern in files (like grep)."),
            new(CommandPlatform.Windows, "Recursive search", "Get-ChildItem -Recurse -Filter *.cs | Select-String 'pattern'", "Search text in all .cs files recursively."),
            new(CommandPlatform.Windows, "Search with context", "Select-String -Path *.log -Pattern 'error' -Context 2,2", "Search with 2 lines before/after match."),
            new(CommandPlatform.Windows, "Findstr", "findstr /s /i /n 'pattern' *.txt", "CMD-style recursive search in text files."),
            
            // Windows - Process Management
            new(CommandPlatform.Windows, "Find process by name", "Get-Process | Where-Object { $_.Name -like '*pattern*' }", "Search running processes by name pattern."),
            new(CommandPlatform.Windows, "Find PID by port", "Get-NetTCPConnection -LocalPort <port> | Select-Object OwningProcess", "Find process ID using a specific port."),
            new(CommandPlatform.Windows, "Find PID by name", "Get-Process -Name <name> | Select-Object Id,Name,CPU", "Get PID and details of process by name."),
            new(CommandPlatform.Windows, "Process tree", "Get-Process | Sort-Object StartTime | Select-Object -First 10 Name,Id,StartTime", "Inspect recent processes with IDs."),
            new(CommandPlatform.Windows, "Top processes CPU", "Get-Process | Sort-Object CPU -Descending | Select-Object -First 10 Name,Id,CPU", "Top 10 CPU consuming processes."),
            new(CommandPlatform.Windows, "Top processes memory", "Get-Process | Sort-Object WorkingSet -Descending | Select-Object -First 10 Name,Id,@{N='Mem(MB)';E={[math]::Round($_.WorkingSet/1MB,2)}}", "Top 10 memory consuming processes."),
            new(CommandPlatform.Windows, "Kill by PID", "Stop-Process -Id <pid> -Force", "Force kill a process by PID.", "Use with caution - data loss possible."),
            new(CommandPlatform.Windows, "Kill by name", "Stop-Process -Name <name> -Force", "Kill all processes with specified name.", "Verify name matches only intended processes."),
            
            // Windows - System Resources
            new(CommandPlatform.Windows, "Disk usage", "Get-Volume", "Display volumes with labels, sizes, and health (PowerShell)."),
            new(CommandPlatform.Windows, "Directory size", "Get-ChildItem | ForEach-Object { $_.Name + ': ' + '{0:N2} MB' -f ((Get-ChildItem $_.FullName -Recurse -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum/1MB) }", "Show size of each folder in current directory."),
            new(CommandPlatform.Windows, "Memory usage", "Get-Process | Sort-Object WorkingSet -Descending | Select-Object -First 5 Name,WorkingSet", "Top processes by working set in PowerShell."),
            new(CommandPlatform.Windows, "System info", "systeminfo | Select-String 'OS','Memory','Processor'", "Display key system information."),
            new(CommandPlatform.Windows, "Uptime", "[System.TimeSpan]::FromMilliseconds([Environment]::TickCount).ToString()", "Show system uptime."),
            
            // Windows - Network
            new(CommandPlatform.Windows, "Network summary", "Get-NetIPAddress | Where-Object { $_.AddressState -eq 'Preferred' }", "List active IP assignments."),
            new(CommandPlatform.Windows, "Port check", "Get-NetTCPConnection -State Listen | Select-Object -First 10 LocalPort,OwningProcess", "Identify listening TCP ports."),
            new(CommandPlatform.Windows, "Active connections", "Get-NetTCPConnection -State Established | Select-Object LocalAddress,LocalPort,RemoteAddress,RemotePort", "Show established connections."),
            new(CommandPlatform.Windows, "DNS lookup", "Resolve-DnsName <domain>", "Query DNS for domain information."),
            new(CommandPlatform.Windows, "Test connection", "Test-NetConnection -ComputerName <host> -Port <port>", "Test TCP connection to host and port."),
            
            // Windows - Logs & Services
            new(CommandPlatform.Windows, "Tail system log", "Get-EventLog -LogName System -Newest 50", "Quick view of the latest system events."),
            new(CommandPlatform.Windows, "Find errors in log", "Get-EventLog -LogName Application -EntryType Error -Newest 20", "Recent application error events."),
            new(CommandPlatform.Windows, "Service status", "Get-Service -Name <service>", "Check status of a Windows service."),
            new(CommandPlatform.Windows, "Restart service", "Restart-Service -Name <service>", "Restart a Windows service by name.", "Replace <service> with a valid service name to avoid stop failures."),
            new(CommandPlatform.Windows, "Package updates", "winget upgrade --all", "Upgrade all available packages via winget."),
        }.ToImmutableArray();
    }
}