using NexusWorks.Guardian.Acquisition;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Reporting;

namespace NexusWorks.Guardian.Orchestration;

public sealed class GuardianRunCoordinator
{
    private readonly IInputPreparationService _inputPreparationService;
    private readonly GuardianComparisonEngine _comparisonEngine;
    private readonly GuardianReportService _reportService;

    public GuardianRunCoordinator(
        IInputPreparationService inputPreparationService,
        GuardianComparisonEngine comparisonEngine,
        GuardianReportService reportService)
    {
        _inputPreparationService = inputPreparationService;
        _comparisonEngine = comparisonEngine;
        _reportService = reportService;
    }

    public async Task<ExecutionReport> RunAsync(GuardianRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prepared = await _inputPreparationService.PrepareAsync(request, cancellationToken);
        var comparisonRequest = new ComparisonExecutionRequest(
            prepared.Current.EffectiveLocalRootPath,
            prepared.Patch.EffectiveLocalRootPath,
            prepared.BaselinePath,
            prepared.Options);

        var timeout = prepared.Options.Timeout ?? GuardianExecutionRunner.DefaultTimeout;
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var comparisonResult = _comparisonEngine.Execute(comparisonRequest, linkedCts.Token);
            var mergedPerformance = MergePerformance(prepared.AcquisitionSummary.PreparationPerformance, comparisonResult.Performance, prepared.StartedAt, comparisonResult.CompletedAt);
            var mergedResult = comparisonResult with
            {
                StartedAt = prepared.StartedAt,
                Performance = mergedPerformance,
            };

            return _reportService.WriteReports(
                prepared.OutputRootPath,
                comparisonRequest,
                mergedResult,
                prepared.ReportTitle,
                prepared.AcquisitionSummary);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Guardian execution timed out after {timeout.TotalSeconds:0}s.");
        }
    }

    private static ExecutionPerformanceSummary MergePerformance(
        ExecutionPerformanceSummary preparation,
        ExecutionPerformanceSummary? comparison,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        var stages = preparation.Stages.Concat(comparison?.Stages ?? Array.Empty<ExecutionStageMetric>()).ToArray();
        var totalDurationMs = Math.Max(0, (completedAt - startedAt).TotalMilliseconds);
        return new ExecutionPerformanceSummary(totalDurationMs, stages);
    }
}
