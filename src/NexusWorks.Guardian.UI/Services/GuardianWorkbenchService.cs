using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Orchestration;
using NexusWorks.Guardian.Preferences;
using NexusWorks.Guardian.Reporting;

namespace NexusWorks.Guardian.UI.Services;

public sealed class GuardianWorkbenchService
{
    private readonly GuardianExecutionRunner _executionRunner;
    private readonly IExecutionHistoryStore _historyStore;
    private readonly IRecentPathStore _recentPathStore;
    private readonly IBaselinePreviewService _baselinePreviewService;

    public GuardianWorkbenchService(
        GuardianExecutionRunner executionRunner,
        IExecutionHistoryStore historyStore,
        IRecentPathStore recentPathStore,
        IBaselinePreviewService baselinePreviewService)
    {
        _executionRunner = executionRunner;
        _historyStore = historyStore;
        _recentPathStore = recentPathStore;
        _baselinePreviewService = baselinePreviewService;
    }

    public ExecutionReport? LastReport { get; private set; }

    public async Task<ExecutionReport> RunAsync(GuardianRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await Task.Run(
            () =>
            {
                var executionReport = _executionRunner.ExecuteAndWriteReports(
                    new ComparisonExecutionRequest(request.CurrentRootPath, request.PatchRootPath, request.BaselinePath),
                    request.OutputRootPath,
                    request.ReportTitle,
                    cancellationToken);

                _recentPathStore.Remember(RecentPathKind.CurrentRoot, request.CurrentRootPath);
                _recentPathStore.Remember(RecentPathKind.PatchRoot, request.PatchRootPath);
                _recentPathStore.Remember(RecentPathKind.BaselineFile, request.BaselinePath);
                _recentPathStore.Remember(RecentPathKind.OutputRoot, request.OutputRootPath);
                return executionReport;
            },
            cancellationToken);

        LastReport = report;
        return report;
    }

    public Task<IReadOnlyList<ExecutionHistoryEntry>> ListRecentAsync(string outputRootPath, int maxCount = 8)
        => Task.Run(() => _historyStore.ListRecent(outputRootPath, maxCount));

    public Task<IReadOnlyList<string>> ListRecentPathsAsync(RecentPathKind kind, int maxCount = 6)
        => Task.Run(() => _recentPathStore.List(kind, maxCount));

    public Task RememberPathAsync(RecentPathKind kind, string path, int maxCount = 6)
        => Task.Run(() => _recentPathStore.Remember(kind, path, maxCount));

    public Task<BaselinePreviewSummary?> PreviewBaselineAsync(string baselinePath)
        => Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(baselinePath) || !File.Exists(baselinePath))
            {
                return null;
            }

            return _baselinePreviewService.Load(baselinePath);
        });

    public Task<ExecutionReport?> LoadAsync(string jsonResultPath)
        => Task.Run(() =>
        {
            var report = _historyStore.Load(jsonResultPath);
            if (report is not null)
            {
                LastReport = report;
            }

            return report;
        });

    public Task<bool> DeleteHistoryAsync(string outputDirectory)
        => Task.Run(() =>
        {
            var deleted = _historyStore.Delete(outputDirectory);
            if (deleted && string.Equals(LastReport?.Artifacts.OutputDirectory, outputDirectory, StringComparison.OrdinalIgnoreCase))
            {
                LastReport = null;
            }

            return deleted;
        });
}

public sealed record GuardianRunRequest(
    string CurrentRootPath,
    string PatchRootPath,
    string BaselinePath,
    string OutputRootPath,
    string ReportTitle);
