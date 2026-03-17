using System.ComponentModel.DataAnnotations;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.UI.Components.Pages;

internal sealed class RunFormModel
{
    public RunFormModel()
    {
        ReportTitle = "Guardian Patch Inspection";
        CurrentMode = InputSourceMode.Local;
        PatchMode = InputSourceMode.Local;
        CurrentSftpPort = 22;
        CurrentSftpAuthenticationMode = SftpAuthenticationMode.Password;
        PatchSftpPort = 22;
        PatchSftpAuthenticationMode = SftpAuthenticationMode.Password;
        PatchUseCurrentSftpConnection = true;
        OutputRootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NexusWorks.Guardian",
            "output");
    }

    [Required]
    public string ReportTitle { get; set; }

    [Required]
    public string CurrentRootPath { get; set; } = string.Empty;

    public InputSourceMode CurrentMode { get; set; }

    public string CurrentSftpHost { get; set; } = string.Empty;

    public int CurrentSftpPort { get; set; }

    public string CurrentSftpUsername { get; set; } = string.Empty;

    public SftpAuthenticationMode CurrentSftpAuthenticationMode { get; set; }

    public string CurrentSftpPassword { get; set; } = string.Empty;

    public string CurrentSftpPrivateKeyPath { get; set; } = string.Empty;

    public string CurrentSftpPrivateKeyPassphrase { get; set; } = string.Empty;

    public string CurrentSftpRemoteRoot { get; set; } = string.Empty;

    public string CurrentSftpFingerprint { get; set; } = string.Empty;

    public bool CurrentSftpClearTargetBeforeDownload { get; set; }

    [Required]
    public string PatchRootPath { get; set; } = string.Empty;

    public InputSourceMode PatchMode { get; set; }

    public bool PatchUseCurrentSftpConnection { get; set; }

    public string PatchSftpHost { get; set; } = string.Empty;

    public int PatchSftpPort { get; set; }

    public string PatchSftpUsername { get; set; } = string.Empty;

    public SftpAuthenticationMode PatchSftpAuthenticationMode { get; set; }

    public string PatchSftpPassword { get; set; } = string.Empty;

    public string PatchSftpPrivateKeyPath { get; set; } = string.Empty;

    public string PatchSftpPrivateKeyPassphrase { get; set; } = string.Empty;

    public string PatchSftpRemoteRoot { get; set; } = string.Empty;

    public string PatchSftpFingerprint { get; set; } = string.Empty;

    public bool PatchSftpClearTargetBeforeDownload { get; set; }

    [Required]
    public string BaselinePath { get; set; } = string.Empty;

    [Required]
    public string OutputRootPath { get; set; }
}

internal sealed record SummaryCard(string Title, string Value, string Badge, string BadgeClass, string Note);

internal sealed record ShortcutEntry(IReadOnlyList<string> Keys, string Title, string Description);

public sealed record PathStatusBadge(string Label, string BadgeClass, bool CanProceed);

internal sealed record RunReadinessStatus(bool IsReady, string Label, string BadgeClass, string Description);

internal sealed record SampleDatasetInfo(string RootPath, string CurrentRootPath, string PatchRootPath, string BaselinePath, string OutputRootPath, string ReadmePath);

internal enum UiNoticeKind
{
    Info,
    Success,
}

internal enum ReportArtifactShortcut
{
    Html,
    Excel,
    Json,
    Log,
    OutputDirectory,
}

internal enum ResultFilter
{
    All,
    Changed,
    MissingRequired,
    Error,
    Ok,
}

internal enum PathField
{
    CurrentRoot,
    PatchRoot,
    BaselinePath,
    OutputRoot,
}
