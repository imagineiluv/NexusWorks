using System.Diagnostics;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Acquisition;

public interface IInputPreparationService
{
    Task<PreparedExecutionContext> PrepareAsync(GuardianRunRequest request, CancellationToken cancellationToken = default);
}

public sealed class InputPreparationService : IInputPreparationService
{
    private readonly ISftpDownloadService _sftpDownloadService;

    public InputPreparationService(ISftpDownloadService sftpDownloadService)
    {
        _sftpDownloadService = sftpDownloadService;
    }

    public async Task<PreparedExecutionContext> PrepareAsync(GuardianRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = DateTimeOffset.UtcNow;
        var totalStopwatch = Stopwatch.StartNew();

        var normalizedOutputRoot = Path.GetFullPath(request.OutputRootPath);
        var normalizedCurrentRoot = Path.GetFullPath(request.CurrentInput.LocalRootPath);
        var normalizedPatchRoot = Path.GetFullPath(request.PatchInput.LocalRootPath);

        ValidateTargetPair(normalizedCurrentRoot, normalizedPatchRoot);
        ValidateTargetDoesNotOverlapOutput(normalizedCurrentRoot, normalizedOutputRoot, nameof(request.CurrentInput.LocalRootPath));
        ValidateTargetDoesNotOverlapOutput(normalizedPatchRoot, normalizedOutputRoot, nameof(request.PatchInput.LocalRootPath));

        var stageMetrics = new List<ExecutionStageMetric>();
        var current = await PrepareSideAsync(request.CurrentInput with { LocalRootPath = normalizedCurrentRoot }, stageMetrics, cancellationToken);
        var patch = await PrepareSideAsync(request.PatchInput with { LocalRootPath = normalizedPatchRoot }, stageMetrics, cancellationToken);

        totalStopwatch.Stop();
        var performance = new ExecutionPerformanceSummary(totalStopwatch.Elapsed.TotalMilliseconds, stageMetrics);
        var acquisitionSummary = new InputAcquisitionSummary([current.Summary, patch.Summary], performance);

        return new PreparedExecutionContext(
            current,
            patch,
            Path.GetFullPath(request.BaselinePath),
            normalizedOutputRoot,
            request.ReportTitle,
            request.EffectiveOptions,
            startedAt,
            acquisitionSummary);
    }

    private async Task<PreparedInputSide> PrepareSideAsync(
        InputSourceRequest request,
        List<ExecutionStageMetric> stageMetrics,
        CancellationToken cancellationToken)
    {
        var stageName = request.Side switch
        {
            InputSide.Current => "Current Input Preparation",
            InputSide.Patch => "Patch Input Preparation",
            _ => "Input Preparation",
        };

        var stopwatch = Stopwatch.StartNew();

        if (request.Mode == InputSourceMode.Local)
        {
            if (!Directory.Exists(request.LocalRootPath))
            {
                throw new DirectoryNotFoundException($"Directory not found for '{request.Side}': {request.LocalRootPath}");
            }

            stopwatch.Stop();
            stageMetrics.Add(new ExecutionStageMetric(stageName, 1, stopwatch.Elapsed.TotalMilliseconds, 1));

            return new PreparedInputSide(
                request.Side,
                request.Mode,
                request.LocalRootPath,
                new InputAcquisitionSideSummary(
                    request.Side,
                    request.Mode,
                    request.LocalRootPath,
                    Host: null,
                    Port: null,
                    Username: null,
                    RemoteRootPath: null,
                    DownloadedFileCount: 0,
                    DownloadedBytes: 0,
                    ClearedTargetBeforeDownload: false,
                    HostFingerprint: null,
                    Warnings: Array.Empty<string>(),
                    AuthenticationMode: null));
        }

        var sftp = request.Sftp ?? throw new InvalidOperationException($"SFTP configuration is missing for '{request.Side}'.");
        PrepareSftpTargetDirectory(request.LocalRootPath, sftp.ClearTargetBeforeDownload, request.Side);
        var download = await _sftpDownloadService.DownloadAsync(
            new SftpDownloadRequest(
                request.Side,
                sftp.Host,
                sftp.Port,
                sftp.Username,
                sftp.Password,
                sftp.RemoteRootPath,
                request.LocalRootPath,
                sftp.HostFingerprint,
                sftp.ClearTargetBeforeDownload,
                sftp.AuthenticationMode,
                sftp.PrivateKeyPath,
                sftp.PrivateKeyPassphrase),
            cancellationToken);

        stopwatch.Stop();
        stageMetrics.Add(new ExecutionStageMetric(stageName, Math.Max(1, download.DownloadedFileCount), stopwatch.Elapsed.TotalMilliseconds, 1));

        return new PreparedInputSide(
            request.Side,
            request.Mode,
            request.LocalRootPath,
            new InputAcquisitionSideSummary(
                request.Side,
                request.Mode,
                request.LocalRootPath,
                sftp.Host,
                sftp.Port,
                sftp.Username,
                sftp.RemoteRootPath,
                download.DownloadedFileCount,
                download.DownloadedBytes,
                download.ClearedTargetBeforeDownload,
                download.HostFingerprint,
                download.Warnings,
                sftp.AuthenticationMode));
    }

    private static void ValidateTargetPair(string currentRootPath, string patchRootPath)
    {
        if (PathsOverlap(currentRootPath, patchRootPath))
        {
            throw new InvalidOperationException("Current and patch local roots must not be the same path or overlap.");
        }
    }

    private static void ValidateTargetDoesNotOverlapOutput(string targetPath, string outputRootPath, string paramName)
    {
        if (PathsOverlap(targetPath, outputRootPath))
        {
            throw new InvalidOperationException($"'{paramName}' must not be the same path as, inside, or contain the output root.");
        }
    }

    private static bool PathsOverlap(string left, string right)
        => IsSameOrDescendant(left, right) || IsSameOrDescendant(right, left);

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        var normalizedCandidate = EnsureTrailingSeparator(Path.GetFullPath(candidate));
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static void PrepareSftpTargetDirectory(string path, bool clearTargetBeforeDownload, InputSide side)
    {
        ValidateSafeTargetPath(path, side);

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            return;
        }

        if (!Directory.EnumerateFileSystemEntries(path).Any())
        {
            return;
        }

        if (!clearTargetBeforeDownload)
        {
            throw new InvalidOperationException(
                $"The local target path for '{side}' is not empty. Enable clear-before-download or choose an empty folder: {path}");
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }
    }

    private static void ValidateSafeTargetPath(string path, InputSide side)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath);
        if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), rootPath?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The local target path for '{side}' cannot be a drive root: {fullPath}");
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile)
            && string.Equals(
                fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                userProfile.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The local target path for '{side}' cannot be the user profile root: {fullPath}");
        }
    }
}

public sealed record PreparedInputSide(
    InputSide Side,
    InputSourceMode Mode,
    string EffectiveLocalRootPath,
    InputAcquisitionSideSummary Summary);

public sealed record PreparedExecutionContext(
    PreparedInputSide Current,
    PreparedInputSide Patch,
    string BaselinePath,
    string OutputRootPath,
    string ReportTitle,
    ComparisonOptions Options,
    DateTimeOffset StartedAt,
    InputAcquisitionSummary AcquisitionSummary);
