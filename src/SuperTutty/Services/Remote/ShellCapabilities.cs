namespace SuperTutty.Services.Remote;

public sealed class ShellCapabilities
{
    public bool SupportsJsonOutput { get; init; }
    public bool SupportsRegexWithContext { get; init; }
    public bool SupportsSymlinkTraversal { get; init; }
    public bool UsesUtf8ByDefault { get; init; } = true;
}
