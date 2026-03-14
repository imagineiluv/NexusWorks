using Microsoft.Maui.Storage;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.UI.Services;

public interface ISftpSecretStore
{
    Task<string?> GetPasswordAsync(InputSide side);
    Task SavePasswordAsync(InputSide side, string? password);
    Task<string?> GetPrivateKeyPassphraseAsync(InputSide side);
    Task SavePrivateKeyPassphraseAsync(InputSide side, string? passphrase);
}

public sealed class MauiSftpSecretStore : ISftpSecretStore
{
    public Task<string?> GetPasswordAsync(InputSide side)
        => GetAsync(BuildKey(side, "password"));

    public Task SavePasswordAsync(InputSide side, string? password)
        => SaveAsync(BuildKey(side, "password"), password);

    public Task<string?> GetPrivateKeyPassphraseAsync(InputSide side)
        => GetAsync(BuildKey(side, "private-key-passphrase"));

    public Task SavePrivateKeyPassphraseAsync(InputSide side, string? passphrase)
        => SaveAsync(BuildKey(side, "private-key-passphrase"), passphrase);

    private static string BuildKey(InputSide side, string secretName)
        => $"guardian:sftp:{side.ToString().ToLowerInvariant()}:{secretName}";

    private static async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task SaveAsync(string key, string? value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                SecureStorage.Default.Remove(key);
                return;
            }

            await SecureStorage.Default.SetAsync(key, value);
        }
        catch (Exception)
        {
        }
    }
}
