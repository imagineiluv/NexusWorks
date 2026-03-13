using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Reporting;

namespace NexusWorks.Guardian.Orchestration;

public sealed class GuardianExecutionRunner
{
    /// <summary>Default execution timeout (10 minutes).</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    private readonly GuardianComparisonEngine _comparisonEngine;
    private readonly GuardianReportService _reportService;

    public GuardianExecutionRunner(
        GuardianComparisonEngine comparisonEngine,
        GuardianReportService reportService)
    {
        _comparisonEngine = comparisonEngine;
        _reportService = reportService;
    }

    public ExecutionReport ExecuteAndWriteReports(
        ComparisonExecutionRequest request,
        string outputRootPath,
        string? reportTitle = null,
        CancellationToken cancellationToken = default)
        => ExecuteAndWriteReports(request, outputRootPath, reportTitle, request.EffectiveOptions.Timeout ?? DefaultTimeout, cancellationToken);

    public ExecutionReport ExecuteAndWriteReports(
        ComparisonExecutionRequest request,
        string outputRootPath,
        string? reportTitle,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var comparisonResult = _comparisonEngine.Execute(request, linkedCts.Token);
            return _reportService.WriteReports(outputRootPath, request, comparisonResult, reportTitle);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Guardian execution timed out after {timeout.TotalSeconds:0}s.");
        }
    }
}
