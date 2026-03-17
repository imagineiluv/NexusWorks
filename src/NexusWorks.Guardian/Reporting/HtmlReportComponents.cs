using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Reporting;

/// <summary>Generates the CSS &lt;style&gt; block for the HTML report.</summary>
internal static class HtmlReportStyleGenerator
{
    public static string Generate() => """
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
    .pagination {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 18px;
      border-top: 1px solid var(--line);
      font-size: 13px;
      color: var(--muted);
      flex-wrap: wrap;
    }
    .pagination button {
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 6px 12px;
      background: var(--panel);
      font: inherit;
      font-size: 13px;
      cursor: pointer;
      color: var(--ink);
    }
    .pagination button:hover:not(:disabled) {
      background: #f3f8fc;
      border-color: var(--primary);
    }
    .pagination button:disabled {
      opacity: 0.4;
      cursor: default;
    }
    .pagination select {
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 6px 8px;
      font: inherit;
      font-size: 13px;
      background: var(--panel);
    }
    .pagination .page-info {
      margin-left: auto;
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
    """;
}

/// <summary>Generates the client-side JavaScript for the HTML report.</summary>
internal static class HtmlReportScriptGenerator
{
    public static string Generate(string itemsJson) => $$"""
    const items = {{itemsJson}};
    const searchBox = document.getElementById('searchBox');
    const resultsBody = document.getElementById('resultsBody');
    const detailPanel = document.getElementById('detailPanel');
    const btnFirst = document.getElementById('btnFirst');
    const btnPrev = document.getElementById('btnPrev');
    const btnNext = document.getElementById('btnNext');
    const btnLast = document.getElementById('btnLast');
    const pageInfo = document.getElementById('pageInfo');
    const pageSizeSelect = document.getElementById('pageSizeSelect');
    let activeIndex = 0;
    let currentPage = 0;
    let pageSize = 100;
    let lastFiltered = [];

    function escapeHtml(value) {
      return (value ?? '').toString()
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('\"', '&quot;')
        .replaceAll("'", '&#39;');
    }

    function badgeClass(status) {
      return String(status).replaceAll('_', '').toLowerCase();
    }

    function totalPages() {
      if (pageSize === 0 || lastFiltered.length === 0) return 1;
      return Math.ceil(lastFiltered.length / pageSize);
    }

    function renderRows(filterText) {
      const normalized = filterText.trim().toLowerCase();
      lastFiltered = items
        .map((item, index) => ({ item, index }))
        .filter(({ item }) => {
          if (!normalized) return true;
          return [item.relativePath, item.ruleId, item.status, item.severity, item.fileType, item.summary]
            .join(' ').toLowerCase().includes(normalized);
        });

      if (lastFiltered.length === 0) {
        resultsBody.innerHTML = '<tr><td colspan="6">No results match the current filter.</td></tr>';
        detailPanel.innerHTML = '<div class="detail-field">No item selected.</div>';
        updatePagination();
        return;
      }

      if (!lastFiltered.some(entry => entry.index === activeIndex)) {
        activeIndex = lastFiltered[0].index;
        currentPage = 0;
      }

      renderPage();
    }

    function renderPage() {
      const total = totalPages();
      if (currentPage >= total) currentPage = total - 1;
      if (currentPage < 0) currentPage = 0;

      const start = pageSize === 0 ? 0 : currentPage * pageSize;
      const end = pageSize === 0 ? lastFiltered.length : Math.min(start + pageSize, lastFiltered.length);
      const pageItems = lastFiltered.slice(start, end);

      resultsBody.innerHTML = pageItems.map(({ item, index }) => `
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
          renderPage();
          renderDetail(items[activeIndex]);
        });
      }

      renderDetail(items[activeIndex]);
      updatePagination();
    }

    function updatePagination() {
      const total = totalPages();
      const showing = lastFiltered.length;
      const start = pageSize === 0 ? 1 : currentPage * pageSize + 1;
      const end = pageSize === 0 ? showing : Math.min(start + pageSize - 1, showing);
      pageInfo.textContent = showing === 0
        ? 'No results'
        : `${start}\u2013${end} of ${showing.toLocaleString()} items`;
      btnFirst.disabled = currentPage === 0;
      btnPrev.disabled = currentPage === 0;
      btnNext.disabled = currentPage >= total - 1;
      btnLast.disabled = currentPage >= total - 1;
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

    searchBox.addEventListener('input', event => { currentPage = 0; renderRows(event.target.value); });
    btnFirst.addEventListener('click', () => { currentPage = 0; renderPage(); });
    btnPrev.addEventListener('click', () => { currentPage = Math.max(0, currentPage - 1); renderPage(); });
    btnNext.addEventListener('click', () => { currentPage = Math.min(totalPages() - 1, currentPage + 1); renderPage(); });
    btnLast.addEventListener('click', () => { currentPage = totalPages() - 1; renderPage(); });
    pageSizeSelect.addEventListener('change', event => { pageSize = Number(event.target.value); currentPage = 0; renderPage(); updatePagination(); });
    renderRows('');
    """;
}

/// <summary>Builds HTML fragments (KPI cards, performance rows) and formats values for the report.</summary>
internal static class HtmlReportDataConverter
{
    public static string BuildCards(IReadOnlyDictionary<string, int> counts, string eyebrow)
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

    public static string BuildPerformanceRows(IReadOnlyList<ExecutionStageMetric> stages)
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

    public static string FormatDuration(double durationMs)
        => durationMs >= 1000d
            ? $"{durationMs / 1000d:0.00} s"
            : $"{durationMs:0.##} ms";

    public static string FormatThroughput(double itemsPerSecond)
        => itemsPerSecond <= 0
            ? "-"
            : $"{itemsPerSecond:0.##} items/s";
}
