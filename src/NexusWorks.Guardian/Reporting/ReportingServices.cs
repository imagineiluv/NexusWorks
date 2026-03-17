using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Reporting;

public interface IResultAggregator
{
    ExecutionSummary Create(string executionId, ComparisonExecutionResult result);
}

public interface IHtmlReportWriter
{
    void Write(ExecutionReport report);
}

public interface IExcelReportWriter
{
    void Write(ExecutionReport report);
}

public sealed class ResultAggregator : IResultAggregator
{
    public ExecutionSummary Create(string executionId, ComparisonExecutionResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentNullException.ThrowIfNull(result);

        return new ExecutionSummary(
            executionId,
            result.StartedAt,
            result.CompletedAt,
            result.Items.Count,
            GroupByName(result.Items, static item => item.Status.ToString()),
            GroupByName(result.Items, static item => item.Severity.ToString()),
            GroupByName(result.Items, static item => item.FileType.ToString()),
            result.Performance);
    }

    private static IReadOnlyDictionary<string, int> GroupByName(
        IReadOnlyList<ComparisonItemResult> items,
        Func<ComparisonItemResult, string> selector)
        => items
            .GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
}

public sealed class StaticHtmlReportWriter : IHtmlReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = ReportingJson.CreateOptions(writeIndented: false);

    public void Write(ExecutionReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var css = HtmlReportStyleGenerator.Generate();
        var statusCards = HtmlReportDataConverter.BuildCards(report.Summary.StatusCounts, "Status");
        var severityCards = HtmlReportDataConverter.BuildCards(report.Summary.SeverityCounts, "Severity");
        var performanceRows = HtmlReportDataConverter.BuildPerformanceRows(report.Summary.StageMetrics);
        var itemsJson = JsonSerializer.Serialize(report.Result.Items, JsonOptions);
        var js = HtmlReportScriptGenerator.Generate(itemsJson);

        var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{{WebUtility.HtmlEncode(report.ReportTitle)}}</title>
  <style>
    {{css}}
  </style>
</head>
<body>
  <div class="page">
    <section class="hero">
      <h1>{{WebUtility.HtmlEncode(report.ReportTitle)}}</h1>
      <p>Guardian patch inspection report generated at {{report.Summary.CompletedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}}.</p>
      <div class="meta">
        <div class="meta-card"><strong>Execution ID</strong><div class="mono">{{WebUtility.HtmlEncode(report.Summary.ExecutionId)}}</div></div>
        <div class="meta-card"><strong>Total Duration</strong><div class="mono">{{WebUtility.HtmlEncode(HtmlReportDataConverter.FormatDuration(report.Summary.TotalDurationMs))}}</div></div>
        <div class="meta-card"><strong>Peak Workers</strong><div class="mono">{{report.Summary.PeakConcurrency}}</div></div>
        <div class="meta-card"><strong>Current Root</strong><div class="mono">{{WebUtility.HtmlEncode(report.Request.CurrentRootPath)}}</div></div>
        <div class="meta-card"><strong>Patch Root</strong><div class="mono">{{WebUtility.HtmlEncode(report.Request.PatchRootPath)}}</div></div>
        <div class="meta-card"><strong>Baseline</strong><div class="mono">{{WebUtility.HtmlEncode(report.Request.BaselinePath)}}</div></div>
      </div>
    </section>

    <section class="cards">
      <article class="card">
        <span class="eyebrow">Summary</span>
        <div class="value">{{report.Summary.TotalFileCount}}</div>
        <div class="key">Total Items</div>
      </article>
      {{statusCards}}
      {{severityCards}}
    </section>

    <section class="table-card" style="margin-top: 24px;">
      <div class="toolbar">
        <div>
          <strong>Performance</strong>
          <div style="color: var(--muted); font-size: 13px;">Measured execution stages with duration, throughput, and worker usage.</div>
        </div>
      </div>
      <div class="table-wrap" style="max-height: none;">
        <table>
          <thead>
            <tr>
              <th>Stage</th>
              <th>Items</th>
              <th>Duration</th>
              <th>Throughput</th>
              <th>Workers</th>
            </tr>
          </thead>
          <tbody>
            {{performanceRows}}
          </tbody>
        </table>
      </div>
    </section>

    <section class="sections">
      <section class="table-card">
        <div class="toolbar">
          <div>
            <strong>Results</strong>
            <div style="color: var(--muted); font-size: 13px;">Click a row to inspect hashes, messages, and JAR/XML/YAML details.</div>
          </div>
          <input id="searchBox" type="search" placeholder="Search by path, rule, status, severity" />
        </div>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Path</th>
                <th>Status</th>
                <th>Severity</th>
                <th>Rule</th>
                <th>Type</th>
                <th>Summary</th>
              </tr>
            </thead>
            <tbody id="resultsBody"></tbody>
          </table>
        </div>
        <div class="pagination">
          <button id="btnFirst" title="First page">&#x00AB;</button>
          <button id="btnPrev" title="Previous page">&#x2039;</button>
          <span id="pageInfo" class="page-info"></span>
          <button id="btnNext" title="Next page">&#x203A;</button>
          <button id="btnLast" title="Last page">&#x00BB;</button>
          <label>Per page:
            <select id="pageSizeSelect">
              <option value="50">50</option>
              <option value="100" selected>100</option>
              <option value="250">250</option>
              <option value="500">500</option>
              <option value="0">All</option>
            </select>
          </label>
        </div>
      </section>

      <aside class="detail-card">
        <div id="detailPanel" class="stack"></div>
      </aside>
    </section>
  </div>

  <script>
    {{js}}
  </script>
</body>
</html>
""";

        File.WriteAllText(report.Artifacts.HtmlReportPath, html, Encoding.UTF8);
    }
}

public sealed class ClosedXmlExcelReportWriter : IExcelReportWriter
{
    public void Write(ExecutionReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        using var workbook = new XLWorkbook();
        WriteSummaryWorksheet(workbook.AddWorksheet("SUMMARY"), report);
        WriteDetailWorksheet(workbook.AddWorksheet("DETAIL"), report);
        WriteJarDetailWorksheet(workbook.AddWorksheet("JAR_DETAIL"), report);
        WriteXmlDetailWorksheet(workbook.AddWorksheet("XML_DETAIL"), report);
        WriteYamlDetailWorksheet(workbook.AddWorksheet("YAML_DETAIL"), report);

        workbook.SaveAs(report.Artifacts.ExcelReportPath);
    }

    private static void WriteSummaryWorksheet(IXLWorksheet worksheet, ExecutionReport report)
    {
        worksheet.Cell(1, 1).Value = "ReportTitle";
        worksheet.Cell(1, 2).Value = report.ReportTitle;
        worksheet.Cell(2, 1).Value = "ExecutionId";
        worksheet.Cell(2, 2).Value = report.Summary.ExecutionId;
        worksheet.Cell(3, 1).Value = "StartedAt";
        worksheet.Cell(3, 2).Value = report.Summary.StartedAt.ToString("O", CultureInfo.InvariantCulture);
        worksheet.Cell(4, 1).Value = "CompletedAt";
        worksheet.Cell(4, 2).Value = report.Summary.CompletedAt.ToString("O", CultureInfo.InvariantCulture);
        worksheet.Cell(5, 1).Value = "TotalFileCount";
        worksheet.Cell(5, 2).Value = report.Summary.TotalFileCount;
        worksheet.Cell(6, 1).Value = "TotalDurationMs";
        worksheet.Cell(6, 2).Value = report.Summary.TotalDurationMs;
        worksheet.Cell(7, 1).Value = "PeakConcurrency";
        worksheet.Cell(7, 2).Value = report.Summary.PeakConcurrency;

        var performanceStartRow = 9;
        WritePerformanceTable(worksheet, performanceStartRow, report.Summary.StageMetrics);

        var countStartRow = performanceStartRow + Math.Max(report.Summary.StageMetrics.Count, 1) + 3;
        WriteCountTable(worksheet, countStartRow, "Status", report.Summary.StatusCounts);
        WriteCountTable(worksheet, countStartRow, "Severity", report.Summary.SeverityCounts, startColumn: 4);
        WriteCountTable(worksheet, countStartRow, "FileType", report.Summary.FileTypeCounts, startColumn: 7);

        ApplyHeaderStyle(worksheet.Range(1, 1, 7, 1));
        worksheet.Columns().AdjustToContents();
    }

    private static void WriteCountTable(
        IXLWorksheet worksheet,
        int startRow,
        string title,
        IReadOnlyDictionary<string, int> counts,
        int startColumn = 1)
    {
        worksheet.Cell(startRow, startColumn).Value = title;
        worksheet.Cell(startRow, startColumn + 1).Value = "Count";
        ApplyHeaderStyle(worksheet.Range(startRow, startColumn, startRow, startColumn + 1));

        var row = startRow + 1;
        foreach (var pair in counts.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            worksheet.Cell(row, startColumn).Value = pair.Key;
            worksheet.Cell(row, startColumn + 1).Value = pair.Value;
            row++;
        }
    }

    private static void WritePerformanceTable(IXLWorksheet worksheet, int startRow, IReadOnlyList<ExecutionStageMetric> stageMetrics)
    {
        worksheet.Cell(startRow, 1).Value = "Stage";
        worksheet.Cell(startRow, 2).Value = "Items";
        worksheet.Cell(startRow, 3).Value = "DurationMs";
        worksheet.Cell(startRow, 4).Value = "ThroughputPerSecond";
        worksheet.Cell(startRow, 5).Value = "Concurrency";
        ApplyHeaderStyle(worksheet.Range(startRow, 1, startRow, 5));

        var row = startRow + 1;
        if (stageMetrics.Count == 0)
        {
            worksheet.Cell(row, 1).Value = "(none)";
            worksheet.Cell(row, 2).Value = 0;
            worksheet.Cell(row, 3).Value = 0;
            worksheet.Cell(row, 4).Value = 0;
            worksheet.Cell(row, 5).Value = 1;
            return;
        }

        foreach (var stage in stageMetrics)
        {
            worksheet.Cell(row, 1).Value = stage.StageName;
            worksheet.Cell(row, 2).Value = stage.ItemCount;
            worksheet.Cell(row, 3).Value = stage.DurationMs;
            worksheet.Cell(row, 4).Value = stage.ItemsPerSecond;
            worksheet.Cell(row, 5).Value = stage.Concurrency;
            row++;
        }
    }

    private static void WriteDetailWorksheet(IXLWorksheet worksheet, ExecutionReport report)
    {
        var headers = new[]
        {
            "RelativePath",
            "RuleId",
            "FileType",
            "CompareMode",
            "Status",
            "Severity",
            "Summary",
            "CurrentExists",
            "PatchExists",
            "CurrentHash",
            "PatchHash",
        };

        WriteHeaderRow(worksheet, headers);

        var row = 2;
        foreach (var item in report.Result.Items.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            worksheet.Cell(row, 1).Value = item.RelativePath;
            worksheet.Cell(row, 2).Value = item.RuleId;
            worksheet.Cell(row, 3).Value = item.FileType.ToString();
            worksheet.Cell(row, 4).Value = item.CompareMode.ToString();
            worksheet.Cell(row, 5).Value = item.Status.ToString();
            worksheet.Cell(row, 6).Value = item.Severity.ToString();
            worksheet.Cell(row, 7).Value = item.Summary;
            worksheet.Cell(row, 8).Value = item.CurrentExists;
            worksheet.Cell(row, 9).Value = item.PatchExists;
            worksheet.Cell(row, 10).Value = item.CurrentHash ?? string.Empty;
            worksheet.Cell(row, 11).Value = item.PatchHash ?? string.Empty;
            row++;
        }

        FinalizeTable(worksheet, row - 1, headers.Length);
    }

    private static void WriteJarDetailWorksheet(IXLWorksheet worksheet, ExecutionReport report)
    {
        var headers = new[]
        {
            "RelativePath",
            "RuleId",
            "Status",
            "Severity",
            "ManifestChanged",
            "AddedClassCount",
            "RemovedClassCount",
            "ChangedClassCount",
            "PackageSummaries",
            "AddedEntries",
            "RemovedEntries",
            "ChangedEntries",
        };

        WriteHeaderRow(worksheet, headers);

        var row = 2;
        foreach (var item in report.Result.Items.Where(static item => item.JarDetail is not null))
        {
            var detail = item.JarDetail!;
            worksheet.Cell(row, 1).Value = item.RelativePath;
            worksheet.Cell(row, 2).Value = item.RuleId;
            worksheet.Cell(row, 3).Value = item.Status.ToString();
            worksheet.Cell(row, 4).Value = item.Severity.ToString();
            worksheet.Cell(row, 5).Value = detail.ManifestChanged;
            worksheet.Cell(row, 6).Value = detail.AddedClassCount;
            worksheet.Cell(row, 7).Value = detail.RemovedClassCount;
            worksheet.Cell(row, 8).Value = detail.ChangedClassCount;
            worksheet.Cell(row, 9).Value = string.Join(Environment.NewLine, detail.PackageSummaries.Select(static summary => $"{summary.PackageName}: +{summary.AddedClassCount} / -{summary.RemovedClassCount} / Δ{summary.ChangedClassCount}"));
            worksheet.Cell(row, 10).Value = string.Join(Environment.NewLine, detail.AddedEntries);
            worksheet.Cell(row, 11).Value = string.Join(Environment.NewLine, detail.RemovedEntries);
            worksheet.Cell(row, 12).Value = string.Join(Environment.NewLine, detail.ChangedEntries);
            row++;
        }

        FinalizeTable(worksheet, Math.Max(row - 1, 1), headers.Length);
    }

    private static void WriteXmlDetailWorksheet(IXLWorksheet worksheet, ExecutionReport report)
    {
        var headers = new[]
        {
            "RelativePath",
            "RuleId",
            "Status",
            "Severity",
            "AddedNodes",
            "RemovedNodes",
            "ChangedNodeCount",
            "ChangedXPaths",
            "ChangeDetails",
        };

        WriteHeaderRow(worksheet, headers);

        var row = 2;
        foreach (var item in report.Result.Items.Where(static item => item.XmlDetail is not null))
        {
            var detail = item.XmlDetail!;
            worksheet.Cell(row, 1).Value = item.RelativePath;
            worksheet.Cell(row, 2).Value = item.RuleId;
            worksheet.Cell(row, 3).Value = item.Status.ToString();
            worksheet.Cell(row, 4).Value = item.Severity.ToString();
            worksheet.Cell(row, 5).Value = detail.AddedNodes;
            worksheet.Cell(row, 6).Value = detail.RemovedNodes;
            worksheet.Cell(row, 7).Value = detail.ChangedNodeCount;
            worksheet.Cell(row, 8).Value = string.Join(Environment.NewLine, detail.ChangedXPaths);
            worksheet.Cell(row, 9).Value = string.Join(
                Environment.NewLine,
                detail.Changes.Select(static change => $"{change.ChangeKind} {change.Path}: {change.CurrentValue ?? "-"} -> {change.PatchValue ?? "-"}"));
            row++;
        }

        FinalizeTable(worksheet, Math.Max(row - 1, 1), headers.Length);
    }

    private static void WriteYamlDetailWorksheet(IXLWorksheet worksheet, ExecutionReport report)
    {
        var headers = new[]
        {
            "RelativePath",
            "RuleId",
            "Status",
            "Severity",
            "AddedKeys",
            "RemovedKeys",
            "ChangedNodeCount",
            "ChangedPaths",
        };

        WriteHeaderRow(worksheet, headers);

        var row = 2;
        foreach (var item in report.Result.Items.Where(static item => item.YamlDetail is not null))
        {
            var detail = item.YamlDetail!;
            worksheet.Cell(row, 1).Value = item.RelativePath;
            worksheet.Cell(row, 2).Value = item.RuleId;
            worksheet.Cell(row, 3).Value = item.Status.ToString();
            worksheet.Cell(row, 4).Value = item.Severity.ToString();
            worksheet.Cell(row, 5).Value = detail.AddedKeys;
            worksheet.Cell(row, 6).Value = detail.RemovedKeys;
            worksheet.Cell(row, 7).Value = detail.ChangedNodeCount;
            worksheet.Cell(row, 8).Value = string.Join(Environment.NewLine, detail.ChangedPaths);
            row++;
        }

        FinalizeTable(worksheet, Math.Max(row - 1, 1), headers.Length);
    }

    private static void WriteHeaderRow(IXLWorksheet worksheet, IReadOnlyList<string> headers)
    {
        for (var column = 0; column < headers.Count; column++)
        {
            worksheet.Cell(1, column + 1).Value = headers[column];
        }

        ApplyHeaderStyle(worksheet.Range(1, 1, 1, headers.Count));
    }

    private static void FinalizeTable(IXLWorksheet worksheet, int lastRow, int lastColumn)
    {
        worksheet.SheetView.FreezeRows(1);
        worksheet.Range(1, 1, lastRow, lastColumn).SetAutoFilter();
        worksheet.Columns().AdjustToContents();
        worksheet.Rows().Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        worksheet.Rows().Style.Alignment.WrapText = true;
    }

    private static void ApplyHeaderStyle(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#dbe7f0");
    }
}

public sealed class GuardianReportService
{
    private static readonly JsonSerializerOptions JsonOptions = ReportingJson.CreateOptions(writeIndented: true);

    private readonly IResultAggregator _resultAggregator;
    private readonly IHtmlReportWriter _htmlReportWriter;
    private readonly IExcelReportWriter _excelReportWriter;

    public GuardianReportService(
        IResultAggregator resultAggregator,
        IHtmlReportWriter htmlReportWriter,
        IExcelReportWriter excelReportWriter)
    {
        _resultAggregator = resultAggregator;
        _htmlReportWriter = htmlReportWriter;
        _excelReportWriter = excelReportWriter;
    }

    public ExecutionReport WriteReports(
        string outputRootPath,
        ComparisonExecutionRequest request,
        ComparisonExecutionResult result,
        string? reportTitle = null,
        InputAcquisitionSummary? inputAcquisition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRootPath);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var executionId = Guid.NewGuid().ToString("N")[..12];
        var artifacts = ExecutionOutputLayout.Create(outputRootPath, result.CompletedAt, executionId);
        var summary = _resultAggregator.Create(executionId, result);
        var report = new ExecutionReport(
            reportTitle ?? "Guardian Patch Inspection",
            request,
            result,
            summary,
            artifacts,
            inputAcquisition);

        _htmlReportWriter.Write(report);
        _excelReportWriter.Write(report);
        WriteJsonReport(report);
        WriteExecutionLog(report);
        return report;
    }

    private static void WriteJsonReport(ExecutionReport report)
        => File.WriteAllText(report.Artifacts.JsonResultPath, JsonSerializer.Serialize(report, JsonOptions), Encoding.UTF8);

    private static void WriteExecutionLog(ExecutionReport report)
    {
        var lines = new List<string>
        {
            $"ExecutionId={report.Summary.ExecutionId}",
            $"StartedAt={report.Summary.StartedAt:O}",
            $"CompletedAt={report.Summary.CompletedAt:O}",
            $"TotalFileCount={report.Summary.TotalFileCount}",
            $"TotalDurationMs={report.Summary.TotalDurationMs:0.###}",
            $"PeakConcurrency={report.Summary.PeakConcurrency}",
            $"StatusCounts={string.Join(", ", report.Summary.StatusCounts.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}:{pair.Value}"))}",
            $"SeverityCounts={string.Join(", ", report.Summary.SeverityCounts.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}:{pair.Value}"))}",
            $"StageMetrics={string.Join(" | ", report.Summary.StageMetrics.Select(stage => $"{stage.StageName}:{stage.ItemCount}@{stage.DurationMs:0.###}ms/{stage.ItemsPerSecond:0.##}ips/{stage.Concurrency}w"))}",
            $"HtmlReportPath={report.Artifacts.HtmlReportPath}",
            $"ExcelReportPath={report.Artifacts.ExcelReportPath}",
            $"JsonResultPath={report.Artifacts.JsonResultPath}",
        };

        if (report.InputAcquisition is not null)
        {
            lines.Add($"InputModes={string.Join(", ", report.InputAcquisition.Sides.Select(side => $"{side.Side}:{side.Mode}"))}");
            lines.Add($"PreparationStages={string.Join(" | ", report.InputAcquisition.PreparationPerformance.Stages.Select(stage => $"{stage.StageName}:{stage.ItemCount}@{stage.DurationMs:0.###}ms"))}");

            foreach (var side in report.InputAcquisition.Sides)
            {
                lines.Add($"Input[{side.Side}] LocalRoot={side.EffectiveLocalRootPath}");
                if (side.Mode == Models.InputSourceMode.Sftp)
                {
                    lines.Add($"Input[{side.Side}] Remote={side.Username}@{side.Host}:{side.Port}{side.RemoteRootPath}");
                    lines.Add($"Input[{side.Side}] Auth={side.AuthenticationMode}");
                    lines.Add($"Input[{side.Side}] DownloadedFiles={side.DownloadedFileCount}");
                    lines.Add($"Input[{side.Side}] DownloadedBytes={side.DownloadedBytes}");
                    lines.Add($"Input[{side.Side}] ClearedTarget={side.ClearedTargetBeforeDownload}");
                    if (!string.IsNullOrWhiteSpace(side.HostFingerprint))
                    {
                        lines.Add($"Input[{side.Side}] HostFingerprint={side.HostFingerprint}");
                    }
                }
            }
        }

        var errorItems = report.Result.Items
            .Where(static item => item.Status == Models.CompareStatus.Error)
            .OrderBy(static item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (errorItems.Length > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"# Error Items ({errorItems.Length})");
            foreach (var item in errorItems)
            {
                lines.Add($"  [{item.RuleId}] {item.RelativePath}: {item.Summary}");
            }
        }

        var criticalItems = report.Result.Items
            .Where(static item => item.Severity == Models.Severity.Critical && item.Status != Models.CompareStatus.Error)
            .OrderBy(static item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (criticalItems.Length > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"# Critical Items ({criticalItems.Length})");
            foreach (var item in criticalItems)
            {
                lines.Add($"  [{item.RuleId}] {item.RelativePath}: {item.Status} - {item.Summary}");
            }
        }

        File.WriteAllLines(report.Artifacts.LogPath, lines, Encoding.UTF8);
    }

}

internal static class ExecutionOutputLayout
{
    public static ExecutionArtifacts Create(string outputRootPath, DateTimeOffset completedAt, string executionId)
    {
        var guardianRoot = Path.Combine(outputRootPath, "guardian");
        Directory.CreateDirectory(guardianRoot);

        var folderName = completedAt.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var outputDirectory = Path.Combine(guardianRoot, folderName);
        if (Directory.Exists(outputDirectory))
        {
            outputDirectory = Path.Combine(guardianRoot, $"{folderName}-{executionId}");
        }

        Directory.CreateDirectory(outputDirectory);
        var logsDirectory = Path.Combine(outputDirectory, "logs");
        Directory.CreateDirectory(logsDirectory);

        return new ExecutionArtifacts(
            outputDirectory,
            Path.Combine(outputDirectory, "report.html"),
            Path.Combine(outputDirectory, "report.xlsx"),
            Path.Combine(outputDirectory, "results.json"),
            Path.Combine(logsDirectory, "execution.log"));
    }
}

internal static class ReportingJson
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
