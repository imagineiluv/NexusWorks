using ClosedXML.Excel;
using NexusWorks.Guardian.Models;
using System.IO.Compression;

namespace NexusWorks.Guardian.Tests.TestSupport;

internal sealed class TestArtifactFactory : IDisposable
{
    public string RootPath { get; } = Path.Combine(Path.GetTempPath(), "guardian-tests", Guid.NewGuid().ToString("N"));

    public TestArtifactFactory()
    {
        Directory.CreateDirectory(RootPath);
    }

    public string CreateDirectory(string relativePath)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public string WriteTextFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public string WriteJar(string relativePath, IReadOnlyDictionary<string, string> entries)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var stream = File.Create(fullPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        foreach (var entry in entries)
        {
            var zipEntry = archive.CreateEntry(entry.Key);
            using var writer = new StreamWriter(zipEntry.Open());
            writer.Write(entry.Value);
        }

        return fullPath;
    }

    public string WriteBaselineWorkbook(string relativePath, IReadOnlyList<BaselineRule> rules)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("RULES");
        worksheet.Cell(1, 1).Value = "rule_id";
        worksheet.Cell(1, 2).Value = "relative_path";
        worksheet.Cell(1, 3).Value = "pattern";
        worksheet.Cell(1, 4).Value = "file_type";
        worksheet.Cell(1, 5).Value = "required";
        worksheet.Cell(1, 6).Value = "compare_mode";
        worksheet.Cell(1, 7).Value = "detail_compare";
        worksheet.Cell(1, 8).Value = "exclude";
        worksheet.Cell(1, 9).Value = "priority";
        worksheet.Cell(1, 10).Value = "notes";

        for (var index = 0; index < rules.Count; index++)
        {
            var row = index + 2;
            var rule = rules[index];
            worksheet.Cell(row, 1).Value = rule.RuleId;
            worksheet.Cell(row, 2).Value = rule.RelativePath ?? string.Empty;
            worksheet.Cell(row, 3).Value = rule.Pattern ?? string.Empty;
            worksheet.Cell(row, 4).Value = rule.FileType.ToString().ToUpperInvariant();
            worksheet.Cell(row, 5).Value = rule.Required ? "Y" : "N";
            worksheet.Cell(row, 6).Value = ToCompareModeText(rule.CompareMode);
            worksheet.Cell(row, 7).Value = rule.DetailCompare ? "Y" : "N";
            worksheet.Cell(row, 8).Value = rule.Exclude ? "Y" : "N";
            worksheet.Cell(row, 9).Value = rule.Priority;
            worksheet.Cell(row, 10).Value = rule.Notes ?? string.Empty;
        }

        workbook.SaveAs(fullPath);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for temp test data.
        }
    }

    private static string ToCompareModeText(CompareMode mode)
    {
        var parts = new List<string>();
        if (mode.HasFlag(CompareMode.Hash))
        {
            parts.Add("hash");
        }

        if (mode.HasFlag(CompareMode.XmlStructure))
        {
            parts.Add("xml-structure");
        }

        if (mode.HasFlag(CompareMode.YamlStructure))
        {
            parts.Add("yaml-structure");
        }

        if (mode.HasFlag(CompareMode.JarEntry))
        {
            parts.Add("jar-entry");
        }

        return string.Join(", ", parts);
    }
}
