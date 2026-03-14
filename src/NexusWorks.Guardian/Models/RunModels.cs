namespace NexusWorks.Guardian.Models;

public enum InputSourceMode
{
    Local,
    Sftp,
}

public enum InputSide
{
    Current,
    Patch,
}

public enum SftpAuthenticationMode
{
    Password,
    PrivateKey,
}

public sealed record SftpInputRequest(
    string Host,
    int Port,
    string Username,
    string RemoteRootPath,
    string? Password = null,
    string? HostFingerprint = null,
    bool ClearTargetBeforeDownload = false,
    SftpAuthenticationMode AuthenticationMode = SftpAuthenticationMode.Password,
    string? PrivateKeyPath = null,
    string? PrivateKeyPassphrase = null);

public sealed record InputSourceRequest(
    InputSide Side,
    InputSourceMode Mode,
    string LocalRootPath,
    SftpInputRequest? Sftp = null);

public sealed record GuardianRunRequest(
    InputSourceRequest CurrentInput,
    InputSourceRequest PatchInput,
    string BaselinePath,
    string OutputRootPath,
    string ReportTitle,
    ComparisonOptions? Options = null)
{
    public GuardianRunRequest(
        string currentRootPath,
        string patchRootPath,
        string baselinePath,
        string outputRootPath,
        string reportTitle,
        ComparisonOptions? options = null)
        : this(
            new InputSourceRequest(InputSide.Current, InputSourceMode.Local, currentRootPath),
            new InputSourceRequest(InputSide.Patch, InputSourceMode.Local, patchRootPath),
            baselinePath,
            outputRootPath,
            reportTitle,
            options)
    {
    }

    public ComparisonOptions EffectiveOptions => Options ?? ComparisonOptions.Default;
    public string CurrentRootPath => CurrentInput.LocalRootPath;
    public string PatchRootPath => PatchInput.LocalRootPath;

    public static GuardianRunRequest CreateLocal(
        string currentRootPath,
        string patchRootPath,
        string baselinePath,
        string outputRootPath,
        string reportTitle,
        ComparisonOptions? options = null)
        => new(
            new InputSourceRequest(InputSide.Current, InputSourceMode.Local, currentRootPath),
            new InputSourceRequest(InputSide.Patch, InputSourceMode.Local, patchRootPath),
            baselinePath,
            outputRootPath,
            reportTitle,
            options);
}

public sealed record InputAcquisitionSideSummary(
    InputSide Side,
    InputSourceMode Mode,
    string EffectiveLocalRootPath,
    string? Host,
    int? Port,
    string? Username,
    string? RemoteRootPath,
    int DownloadedFileCount,
    long DownloadedBytes,
    bool ClearedTargetBeforeDownload,
    string? HostFingerprint,
    IReadOnlyList<string> Warnings,
    SftpAuthenticationMode? AuthenticationMode = null)
{
    public IReadOnlyList<string> Warnings { get; init; } = Warnings.Count == 0
        ? Array.Empty<string>()
        : Warnings.ToArray();
}

public sealed record InputAcquisitionSummary(
    IReadOnlyList<InputAcquisitionSideSummary> Sides,
    ExecutionPerformanceSummary PreparationPerformance)
{
    public IReadOnlyList<InputAcquisitionSideSummary> Sides { get; init; } = Sides.Count == 0
        ? Array.Empty<InputAcquisitionSideSummary>()
        : Sides.ToArray();
}
