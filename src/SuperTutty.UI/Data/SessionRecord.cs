using System;
using SuperTutty.Services;

namespace SuperTutty.UI.Data;

public class SessionRecord
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public SessionStatus Status { get; set; }
    public SshSession Session { get; init; } = default!;
    public bool IsMenuOpen { get; set; }
}

public enum SessionStatus
{
    Connected,
    Disconnected,
    Error
}