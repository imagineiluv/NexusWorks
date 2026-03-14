using System.Security.Cryptography;
using Renci.SshNet;
using Renci.SshNet.Common;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Acquisition;

public interface ISftpDownloadService
{
    Task<SftpDownloadResult> DownloadAsync(SftpDownloadRequest request, CancellationToken cancellationToken = default);
}

public sealed class SftpDownloadService : ISftpDownloadService
{
    public Task<SftpDownloadResult> DownloadAsync(SftpDownloadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => Download(request, cancellationToken), cancellationToken);
    }

    private static SftpDownloadResult Download(SftpDownloadRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? observedFingerprint = null;
        var warnings = new List<string>();

        using var client = CreateClient(request, fingerprint => observedFingerprint = fingerprint);
        try
        {
            client.Connect();
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedRemoteRoot = NormalizeRemotePath(request.RemoteRootPath);
            if (!client.Exists(normalizedRemoteRoot))
            {
                throw new DirectoryNotFoundException($"Remote path not found for '{request.Side}': {normalizedRemoteRoot}");
            }

            var attributes = client.GetAttributes(normalizedRemoteRoot);
            if (!attributes.IsDirectory)
            {
                throw new InvalidOperationException($"Remote path for '{request.Side}' must be a directory: {normalizedRemoteRoot}");
            }

            var downloadedFileCount = 0;
            long downloadedBytes = 0;
            DownloadDirectory(
                client,
                normalizedRemoteRoot,
                request.LocalRootPath,
                warnings,
                ref downloadedFileCount,
                ref downloadedBytes,
                cancellationToken);

            return new SftpDownloadResult(
                downloadedFileCount,
                downloadedBytes,
                request.ClearTargetBeforeDownload,
                observedFingerprint,
                warnings);
        }
        catch (SshAuthenticationException ex)
        {
            throw new InvalidOperationException($"SFTP authentication failed for '{request.Side}': {ex.Message}", ex);
        }
        catch (SshConnectionException ex)
        {
            throw new InvalidOperationException($"SFTP connection failed for '{request.Side}': {ex.Message}", ex);
        }
        finally
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }

    private static void DownloadDirectory(
        SftpClient client,
        string remoteDirectory,
        string localDirectory,
        List<string> warnings,
        ref int downloadedFileCount,
        ref long downloadedBytes,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(localDirectory);

        foreach (var entry in client.ListDirectory(remoteDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.Name is "." or "..")
            {
                continue;
            }

            if (entry.IsSymbolicLink)
            {
                warnings.Add($"Skipped symbolic link: {entry.FullName}");
                continue;
            }

            var localPath = Path.Combine(localDirectory, entry.Name);
            if (entry.IsDirectory)
            {
                DownloadDirectory(client, NormalizeRemotePath(entry.FullName), localPath, warnings, ref downloadedFileCount, ref downloadedBytes, cancellationToken);
                continue;
            }

            if (!entry.IsRegularFile)
            {
                warnings.Add($"Skipped unsupported remote entry: {entry.FullName}");
                continue;
            }

            using var localStream = File.Create(localPath);
            client.DownloadFile(entry.FullName, localStream);
            localStream.Flush();
            File.SetLastWriteTimeUtc(localPath, entry.LastWriteTimeUtc);
            downloadedFileCount++;
            downloadedBytes += entry.Attributes.Size;
        }
    }

    private static SftpClient CreateClient(SftpDownloadRequest request, Action<string> fingerprintObserver)
    {
        var expectedFingerprint = NormalizeFingerprint(request.HostFingerprint);
        var authenticationMethod = CreateAuthenticationMethod(request);
        var connectionInfo = new ConnectionInfo(
            request.Host,
            request.Port,
            request.Username,
            authenticationMethod);

        var client = new SftpClient(connectionInfo);
        client.HostKeyReceived += (_, eventArgs) =>
        {
            var observedFingerprint = FormatFingerprint(eventArgs.HostKey);
            fingerprintObserver(observedFingerprint);
            eventArgs.CanTrust = string.IsNullOrWhiteSpace(expectedFingerprint)
                || string.Equals(observedFingerprint, expectedFingerprint, StringComparison.OrdinalIgnoreCase);
        };

        return client;
    }

    private static AuthenticationMethod CreateAuthenticationMethod(SftpDownloadRequest request)
        => request.AuthenticationMode switch
        {
            SftpAuthenticationMode.Password => new PasswordAuthenticationMethod(
                request.Username,
                request.Password ?? throw new InvalidOperationException($"Password is required for '{request.Side}' SFTP authentication.")),
            SftpAuthenticationMode.PrivateKey => CreatePrivateKeyAuthenticationMethod(request),
            _ => throw new ArgumentOutOfRangeException(nameof(request.AuthenticationMode), request.AuthenticationMode, null),
        };

    private static AuthenticationMethod CreatePrivateKeyAuthenticationMethod(SftpDownloadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PrivateKeyPath))
        {
            throw new InvalidOperationException($"Private key path is required for '{request.Side}' SFTP authentication.");
        }

        var fullKeyPath = Path.GetFullPath(request.PrivateKeyPath);
        if (!File.Exists(fullKeyPath))
        {
            throw new FileNotFoundException($"Private key file was not found for '{request.Side}'.", fullKeyPath);
        }

        PrivateKeyFile privateKeyFile;
        try
        {
            privateKeyFile = string.IsNullOrWhiteSpace(request.PrivateKeyPassphrase)
                ? new PrivateKeyFile(fullKeyPath)
                : new PrivateKeyFile(fullKeyPath, request.PrivateKeyPassphrase);
        }
        catch (SshPassPhraseNullOrEmptyException ex)
        {
            throw new InvalidOperationException($"The private key for '{request.Side}' requires a passphrase.", ex);
        }

        return new PrivateKeyAuthenticationMethod(request.Username, privateKeyFile);
    }

    private static string FormatFingerprint(byte[] hostKey)
    {
        var hash = SHA256.HashData(hostKey);
        return $"SHA256:{Convert.ToBase64String(hash).TrimEnd('=')}";
    }

    private static string NormalizeFingerprint(string? fingerprint)
        => string.IsNullOrWhiteSpace(fingerprint)
            ? string.Empty
            : fingerprint.Trim();

    private static string NormalizeRemotePath(string remotePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);
        var trimmed = remotePath.Trim().Replace('\\', '/');
        return trimmed.EndsWith("/", StringComparison.Ordinal) && trimmed.Length > 1
            ? trimmed.TrimEnd('/')
            : trimmed;
    }
}

public sealed record SftpDownloadRequest(
    InputSide Side,
    string Host,
    int Port,
    string Username,
    string? Password,
    string RemoteRootPath,
    string LocalRootPath,
    string? HostFingerprint = null,
    bool ClearTargetBeforeDownload = false,
    SftpAuthenticationMode AuthenticationMode = SftpAuthenticationMode.Password,
    string? PrivateKeyPath = null,
    string? PrivateKeyPassphrase = null);

public sealed record SftpDownloadResult(
    int DownloadedFileCount,
    long DownloadedBytes,
    bool ClearedTargetBeforeDownload,
    string? HostFingerprint,
    IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<string> Warnings { get; init; } = Warnings.Count == 0
        ? Array.Empty<string>()
        : Warnings.ToArray();
}
