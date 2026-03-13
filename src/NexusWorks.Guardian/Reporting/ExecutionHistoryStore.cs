using System.Text.Json;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Reporting;

public interface IExecutionHistoryStore
{
    IReadOnlyList<ExecutionHistoryEntry> ListRecent(string outputRootPath, int maxCount = 10);
    ExecutionReport? Load(string jsonResultPath);
    bool Delete(string outputDirectory);
}

public sealed class FileSystemExecutionHistoryStore : IExecutionHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = ReportingJson.CreateOptions(writeIndented: false);

    public IReadOnlyList<ExecutionHistoryEntry> ListRecent(string outputRootPath, int maxCount = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRootPath);

        var guardianRoot = Path.Combine(outputRootPath, "guardian");
        if (!Directory.Exists(guardianRoot))
        {
            return Array.Empty<ExecutionHistoryEntry>();
        }

        return Directory
            .EnumerateFiles(guardianRoot, "results.json", SearchOption.AllDirectories)
            .Select(Load)
            .Where(static report => report is not null)
            .Select(static report => ToEntry(report!))
            .OrderByDescending(static entry => entry.CompletedAt)
            .ThenByDescending(static entry => entry.OutputDirectory, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToArray();
    }

    public ExecutionReport? Load(string jsonResultPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonResultPath);
        if (!File.Exists(jsonResultPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(jsonResultPath);
            return JsonSerializer.Deserialize<ExecutionReport>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // Corrupted or incompatible JSON — safe to skip this entry
            return null;
        }
        catch (IOException)
        {
            // File locked or inaccessible — safe to skip this entry
            return null;
        }
    }

    public bool Delete(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var fullPath = Path.GetFullPath(outputDirectory);
        if (!Directory.Exists(fullPath))
        {
            return false;
        }

        var directoryInfo = new DirectoryInfo(fullPath);
        if (!string.Equals(directoryInfo.Parent?.Name, "guardian", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Guardian execution directories can be deleted from history.");
        }

        Directory.Delete(fullPath, recursive: true);
        return true;
    }

    private static ExecutionHistoryEntry ToEntry(ExecutionReport report)
        => new(
            report.Summary.ExecutionId,
            report.ReportTitle,
            report.Summary.CompletedAt,
            report.Artifacts.OutputDirectory,
            report.Artifacts.HtmlReportPath,
            report.Artifacts.ExcelReportPath,
            report.Artifacts.JsonResultPath,
            report.Artifacts.LogPath,
            report.Summary.StatusCounts,
            report.Summary.SeverityCounts);
}
