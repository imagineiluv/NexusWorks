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

        var statusCards = BuildCards(report.Summary.StatusCounts, "Status");
        var severityCards = BuildCards(report.Summary.SeverityCounts, "Severity");
        var performanceRows = BuildPerformanceRows(report.Summary.StageMetrics);
        var itemsJson = JsonSerializer.Serialize(report.Result.Items, JsonOptions);

        var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{{WebUtility.HtmlEncode(report.ReportTitle)}}</title>
  <style>
    :root {
      --canvas: #f3f5f7;
      --panel: #ffffff;
      --ink: #14202b;
      --line: #d7dde4;
      --muted: #5f6f7f;
      --primary: #0f5f88;
      --ok: #0b8f57;
      --changed: #c57b00;
      --added: #0f75d9;
      --removed: #5d6976;
      --missing: #d22f27;
      --error: #a12665;
      --shadow: 0 20px 45px rgba(20, 32, 43, 0.08);
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: "Inter", "Segoe UI", sans-serif;
      background: linear-gradient(180deg, #e7edf3 0%, var(--canvas) 32%);
      color: var(--ink);
    }
    .page {
      max-width: 1440px;
      margin: 0 auto;
      padding: 32px 24px 48px;
    }
    .hero {
      background: #11202e;
      color: #f5f7fa;
      border-radius: 24px;
      padding: 28px 32px;
      box-shadow: var(--shadow);
    }
    .hero h1 { margin: 0 0 8px; font-size: 32px; }
    .hero p { margin: 0; color: #b8c6d3; }
    .meta {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 12px;
      margin-top: 18px;
    }
    .meta-card, .card, .table-card, .detail-card {
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: 20px;
      box-shadow: var(--shadow);
    }
    .meta-card {
      padding: 16px 18px;
      background: rgba(255,255,255,0.1);
      border-color: rgba(255,255,255,0.12);
    }
    .sections {
      display: grid;
      grid-template-columns: 2fr 1fr;
      gap: 24px;
      margin-top: 24px;
    }
    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
      gap: 16px;
      margin-top: 24px;
    }
    .card {
      padding: 18px;
    }
    .card .eyebrow {
      display: inline-flex;
      font-size: 12px;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--muted);
    }
    .card .value {
      margin-top: 12px;
      font-size: 28px;
      font-weight: 700;
    }
    .card .key {
      margin-top: 6px;
      color: var(--muted);
      font-size: 13px;
    }
    .table-card, .detail-card {
      overflow: hidden;
    }
    .toolbar {
      padding: 16px 18px;
      border-bottom: 1px solid var(--line);
      display: flex;
      justify-content: space-between;
      gap: 12px;
      align-items: center;
    }
    .toolbar input {
      width: min(360px, 100%);
      border: 1px solid var(--line);
      border-radius: 12px;
      padding: 10px 12px;
      font: inherit;
    }
    table {
      width: 100%;
      border-collapse: collapse;
    }
    th, td {
      padding: 12px 14px;
      text-align: left;
      border-bottom: 1px solid #ecf0f3;
      font-size: 14px;
      vertical-align: top;
    }
    th {
      background: #f8fafc;
      color: var(--muted);
      position: sticky;
      top: 0;
      z-index: 1;
    }
    tbody tr {
      cursor: pointer;
    }
    tbody tr:hover,
    tbody tr.is-active {
      background: #f3f8fc;
    }
    .table-wrap {
      max-height: 760px;
      overflow: auto;
    }
    .badge {
      display: inline-flex;
      align-items: center;
      border-radius: 999px;
      padding: 4px 10px;
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: white;
    }
    .badge.ok { background: var(--ok); }
    .badge.changed { background: var(--changed); }
    .badge.added { background: var(--added); }
    .badge.removed { background: var(--removed); }
    .badge.missingrequired { background: var(--missing); }
    .badge.error { background: var(--error); }
    .detail-card {
      padding: 20px;
      min-height: 640px;
    }
    .detail-card h2 {
      margin: 0 0 10px;
      font-size: 24px;
    }
    .detail-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 12px;
      margin-top: 18px;
    }
    .detail-field {
      padding: 14px;
      border-radius: 14px;
      background: #f8fafc;
      border: 1px solid #e6edf3;
    }
    .detail-field strong {
      display: block;
      font-size: 12px;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--muted);
      margin-bottom: 8px;
    }
    .mono {
      font-family: "JetBrains Mono", "SFMono-Regular", monospace;
      word-break: break-all;
    }
    .list {
      margin: 10px 0 0;
      padding-left: 18px;
    }
    .stack {
      display: grid;
      gap: 12px;
    }
    @media (max-width: 1120px) {
      .sections { grid-template-columns: 1fr; }
    }
    @media (max-width: 720px) {
      .page { padding: 18px 14px 28px; }
      .hero { padding: 20px; }
      .detail-grid { grid-template-columns: 1fr; }
      .toolbar { flex-direction: column; align-items: stretch; }
    }
  </style>
</head>
<body>
  <div class="page">
    <section class="hero">
      <h1>{{WebUtility.HtmlEncode(report.ReportTitle)}}</h1>
      <p>Guardian patch inspection report generated at {{report.Summary.CompletedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}}.</p>
      <div class="meta">
        <div class="meta-card"><strong>Execution ID</strong><div class="mono">{{WebUtility.HtmlEncode(report.Summary.ExecutionId)}}</div></div>
        <div class="meta-card"><strong>Total Duration</strong><div class="mono">{{WebUtility.HtmlEncode(FormatDuration(report.Summary.TotalDurationMs))}}</div></div>
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
      </section>

      <aside class="detail-card">
        <div id="detailPanel" class="stack"></div>
      </aside>
    </section>
  </div>

  <script>
    const items = {{itemsJson}};
    const searchBox = document.getElementById('searchBox');
    const resultsBody = document.getElementById('resultsBody');
    const detailPanel = document.getElementById('detailPanel');
    let activeIndex = 0;

    function escapeHtml(value) {
      return (value ?? '').toString()
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('\"', '&quot;')
        .replaceAll(\"'\", '&#39;');
    }

    function badgeClass(status) {
      return String(status).replaceAll('_', '').toLowerCase();
    }

    function renderRows(filterText) {
      const normalized = filterText.trim().toLowerCase();
      const filtered = items
        .map((item, index) => ({ item, index }))
        .filter(({ item }) => {
          if (!normalized) {
            return true;
          }

          const target = [
            item.relativePath,
            item.ruleId,
            item.status,
            item.severity,
            item.fileType,
            item.summary
          ].join(' ').toLowerCase();

          return target.includes(normalized);
        });

      if (filtered.length === 0) {
        resultsBody.innerHTML = '<tr><td colspan="6">No results match the current filter.</td></tr>';
        detailPanel.innerHTML = '<div class="detail-field">No item selected.</div>';
        return;
      }

      if (!filtered.some(entry => entry.index === activeIndex)) {
        activeIndex = filtered[0].index;
      }

      resultsBody.innerHTML = filtered.map(({ item, index }) => `
        <tr data-index="${index}" class="${index === activeIndex ? 'is-active' : ''}">
          <td class="mono">${escapeHtml(item.relativePath)}</td>
          <td><span class="badge ${badgeClass(item.status)}">${escapeHtml(item.status)}</span></td>
          <td>${escapeHtml(item.severity)}</td>
          <td class="mono">${escapeHtml(item.ruleId)}</td>
          <td>${escapeHtml(item.fileType)}</td>
          <td>${escapeHtml(item.summary)}</td>
        </tr>`).join('');

      for (const row of resultsBody.querySelectorAll('tr[data-index]')) {
        row.addEventListener('click', () => {
          activeIndex = Number(row.dataset.index);
          renderRows(searchBox.value);
        });
      }

      renderDetail(items[activeIndex]);
    }

    function renderDetail(item) {
      const messages = (item.messages ?? []).map(message => `<li>${escapeHtml(message)}</li>`).join('');
      const jarDetail = item.jarDetail
        ? `
          <div class="detail-field">
            <strong>JAR Detail</strong>
            <div>Manifest Changed: ${escapeHtml(String(item.jarDetail.manifestChanged))}</div>
            <div>Added: ${item.jarDetail.addedEntries.length}</div>
            <div>Removed: ${item.jarDetail.removedEntries.length}</div>
            <div>Changed: ${item.jarDetail.changedEntries.length}</div>
            <div>Class Delta: +${item.jarDetail.addedClassCount} / -${item.jarDetail.removedClassCount} / Δ${item.jarDetail.changedClassCount}</div>
            <ul class="list mono">
              ${item.jarDetail.addedEntries.map(entry => `<li>+ ${escapeHtml(entry)}</li>`).join('')}
              ${item.jarDetail.removedEntries.map(entry => `<li>- ${escapeHtml(entry)}</li>`).join('')}
              ${item.jarDetail.changedEntries.map(entry => `<li>* ${escapeHtml(entry)}</li>`).join('')}
            </ul>
            <ul class="list">
              ${item.jarDetail.packageSummaries.map(summary => `<li>${escapeHtml(summary.packageName)}: +${summary.addedClassCount} / -${summary.removedClassCount} / Δ${summary.changedClassCount}</li>`).join('')}
            </ul>
          </div>`
        : '';
      const xmlDetail = item.xmlDetail
        ? `
          <div class="detail-field">
            <strong>XML Detail</strong>
            <div>Added Nodes: ${item.xmlDetail.addedNodes}</div>
            <div>Removed Nodes: ${item.xmlDetail.removedNodes}</div>
            <div>Changed Nodes: ${item.xmlDetail.changedNodeCount}</div>
            <ul class="list mono">
              ${item.xmlDetail.changedXPaths.map(path => `<li>${escapeHtml(path)}</li>`).join('')}
            </ul>
            <ul class="list">
              ${item.xmlDetail.changes.map(change => `<li><strong>${escapeHtml(change.changeKind)}</strong> ${escapeHtml(change.path)}<br />${escapeHtml(change.currentValue ?? '-')} -> ${escapeHtml(change.patchValue ?? '-')}</li>`).join('')}
            </ul>
          </div>`
        : '';
      const yamlDetail = item.yamlDetail
        ? `
          <div class="detail-field">
            <strong>YAML Detail</strong>
            <div>Added Keys: ${item.yamlDetail.addedKeys}</div>
            <div>Removed Keys: ${item.yamlDetail.removedKeys}</div>
            <div>Changed Nodes: ${item.yamlDetail.changedNodeCount}</div>
            <ul class="list mono">
              ${item.yamlDetail.changedPaths.map(path => `<li>${escapeHtml(path)}</li>`).join('')}
            </ul>
          </div>`
        : '';

      detailPanel.innerHTML = `
        <div>
          <h2>${escapeHtml(item.relativePath)}</h2>
          <span class="badge ${badgeClass(item.status)}">${escapeHtml(item.status)}</span>
        </div>
        <div class="detail-grid">
          <div class="detail-field"><strong>Rule ID</strong><div class="mono">${escapeHtml(item.ruleId)}</div></div>
          <div class="detail-field"><strong>Severity</strong><div>${escapeHtml(item.severity)}</div></div>
          <div class="detail-field"><strong>File Type</strong><div>${escapeHtml(item.fileType)}</div></div>
          <div class="detail-field"><strong>Compare Mode</strong><div>${escapeHtml(item.compareMode)}</div></div>
          <div class="detail-field"><strong>Current Hash</strong><div class="mono">${escapeHtml(item.currentHash ?? '-')}</div></div>
          <div class="detail-field"><strong>Patch Hash</strong><div class="mono">${escapeHtml(item.patchHash ?? '-')}</div></div>
          <div class="detail-field"><strong>Current Exists</strong><div>${escapeHtml(String(item.currentExists))}</div></div>
          <div class="detail-field"><strong>Patch Exists</strong><div>${escapeHtml(String(item.patchExists))}</div></div>
          <div class="detail-field" style="grid-column: 1 / -1;">
            <strong>Summary</strong>
            <div>${escapeHtml(item.summary)}</div>
          </div>
          <div class="detail-field" style="grid-column: 1 / -1;">
            <strong>Messages</strong>
            <ul class="list">
              ${messages || '<li>No additional messages.</li>'}
            </ul>
          </div>
          ${jarDetail}
          ${xmlDetail}
          ${yamlDetail}
        </div>`;
    }

    searchBox.addEventListener('input', event => renderRows(event.target.value));
    renderRows('');
  </script>
</body>
</html>
""";

        File.WriteAllText(report.Artifacts.HtmlReportPath, html, Encoding.UTF8);
    }

    private static string BuildCards(IReadOnlyDictionary<string, int> counts, string eyebrow)
    {
        var builder = new StringBuilder();
        foreach (var pair in counts.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($$"""
      <article class="card">
        <span class="eyebrow">{{WebUtility.HtmlEncode(eyebrow)}}</span>
        <div class="value">{{pair.Value}}</div>
        <div class="key">{{WebUtility.HtmlEncode(pair.Key)}}</div>
      </article>
""");
        }

        return builder.ToString();
    }

    private static string BuildPerformanceRows(IReadOnlyList<ExecutionStageMetric> stages)
    {
        if (stages.Count == 0)
        {
            return """
            <tr>
              <td colspan="5">No stage metrics captured.</td>
            </tr>
            """;
        }

        var builder = new StringBuilder();
        foreach (var stage in stages)
        {
            builder.AppendLine($$"""
            <tr>
              <td>{{WebUtility.HtmlEncode(stage.StageName)}}</td>
              <td>{{stage.ItemCount}}</td>
              <td>{{WebUtility.HtmlEncode(FormatDuration(stage.DurationMs))}}</td>
              <td>{{WebUtility.HtmlEncode(FormatThroughput(stage.ItemsPerSecond))}}</td>
              <td>{{stage.Concurrency}}</td>
            </tr>
            """);
        }

        return builder.ToString();
    }

    private static string FormatDuration(double durationMs)
        => durationMs >= 1000d
            ? $"{durationMs / 1000d:0.00} s"
            : $"{durationMs:0.##} ms";

    private static string FormatThroughput(double itemsPerSecond)
        => itemsPerSecond <= 0
            ? "-"
            : $"{itemsPerSecond:0.##} items/s";
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
