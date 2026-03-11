namespace SuperTutty.Services;

public sealed class SelectedSessionStore
{
    private SshSession? _selected;

    public void Set(SshSession session)
    {
        _selected = session;
    }

    public SshSession? Current => _selected;

    public void Clear() => _selected = null;
}