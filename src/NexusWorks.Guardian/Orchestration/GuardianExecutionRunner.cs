using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Reporting;

namespace NexusWorks.Guardian.Orchestration;

public sealed class GuardianExecutionRunner
{
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
    {
        var comparisonResult = _comparisonEngine.Execute(request, cancellationToken);
        return _reportService.WriteReports(outputRootPath, request, comparisonResult, reportTitle);
    }
}
