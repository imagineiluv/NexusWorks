namespace NexusWorks.Guardian.Models;

public sealed record ExecutionHistoryEntry(
    string ExecutionId,
    string ReportTitle,
    DateTimeOffset CompletedAt,
    string OutputDirectory,
    string HtmlReportPath,
    string ExcelReportPath,
    string JsonResultPath,
    string LogPath,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyDictionary<string, int> SeverityCounts);

public sealed record ExecutionSummary(
    string ExecutionId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int TotalFileCount,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyDictionary<string, int> SeverityCounts,
    IReadOnlyDictionary<string, int> FileTypeCounts,
    ExecutionPerformanceSummary? Performance = null)
{
    public double TotalDurationMs => Performance?.TotalDurationMs > 0
        ? Performance.TotalDurationMs
        : Math.Max(0, (CompletedAt - StartedAt).TotalMilliseconds);

    public int PeakConcurrency => Performance?.PeakConcurrency ?? 1;

    public IReadOnlyList<ExecutionStageMetric> StageMetrics => Performance?.Stages ?? Array.Empty<ExecutionStageMetric>();
}

public sealed record ExecutionArtifacts(
    string OutputDirectory,
    string HtmlReportPath,
    string ExcelReportPath,
    string JsonResultPath,
    string LogPath);

public sealed record ExecutionReport(
    string ReportTitle,
    ComparisonExecutionRequest Request,
    ComparisonExecutionResult Result,
    ExecutionSummary Summary,
    ExecutionArtifacts Artifacts);
