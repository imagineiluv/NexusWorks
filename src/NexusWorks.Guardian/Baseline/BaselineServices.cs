using ClosedXML.Excel;
using NexusWorks.Guardian.Infrastructure;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Baseline;

public interface IBaselineReader
{
    IReadOnlyList<BaselineRule> Read(string baselinePath);
}

public interface IBaselineValidator
{
    void Validate(IReadOnlyList<BaselineRule> rules);
}

public interface IBaselinePreviewService
{
    BaselinePreviewSummary Load(string baselinePath);
}

public sealed class BaselineValidationException : Exception
{
    public BaselineValidationException(string message) : base(message)
    {
    }
}

public sealed class ClosedXmlBaselineReader : IBaselineReader
{
    private static readonly string[] RequiredColumns =
    [
        "rule_id",
        "file_type",
        "required",
        "compare_mode",
    ];

    public IReadOnlyList<BaselineRule> Read(string baselinePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baselinePath);
        if (!File.Exists(baselinePath))
        {
            throw new FileNotFoundException("Baseline file was not found.", baselinePath);
        }

        using var workbook = new XLWorkbook(baselinePath);
        var worksheet = workbook.Worksheets.FirstOrDefault(static ws => string.Equals(ws.Name, "RULES", StringComparison.OrdinalIgnoreCase));
        if (worksheet is null)
        {
            throw new BaselineValidationException("The baseline workbook must contain a RULES sheet.");
        }

        var headerRow = worksheet.FirstRowUsed();
        if (headerRow is null)
        {
            return Array.Empty<BaselineRule>();
        }

        var headerMap = headerRow.CellsUsed()
            .Select(cell => new { Index = cell.Address.ColumnNumber, Header = cell.GetString().Trim().ToLowerInvariant() })
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Header))
            .ToDictionary(static entry => entry.Header, static entry => entry.Index, StringComparer.OrdinalIgnoreCase);

        foreach (var requiredColumn in RequiredColumns)
        {
            if (!headerMap.ContainsKey(requiredColumn))
            {
                throw new BaselineValidationException($"Missing required baseline column '{requiredColumn}'.");
            }
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var rules = new List<BaselineRule>();

        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (RowIsEmpty(row))
            {
                continue;
            }

            var rule = new BaselineRule(
                ReadRequiredText(row, headerMap, "rule_id"),
                NormalizeRelativePath(row, headerMap, "relative_path"),
                NormalizePattern(row, headerMap, "pattern"),
                ParseFileType(ReadOptionalText(row, headerMap, "file_type") ?? "AUTO"),
                ParseBoolean(ReadRequiredText(row, headerMap, "required"), false),
                ParseCompareMode(ReadRequiredText(row, headerMap, "compare_mode")),
                ParseBoolean(ReadOptionalText(row, headerMap, "detail_compare"), false),
                ParseBoolean(ReadOptionalText(row, headerMap, "exclude"), false),
                ParsePriority(ReadOptionalText(row, headerMap, "priority")),
                ReadOptionalText(row, headerMap, "notes"));

            rules.Add(rule);
        }

        return rules;
    }

    private static bool RowIsEmpty(IXLRow row)
        => row.CellsUsed().All(static cell => string.IsNullOrWhiteSpace(cell.GetString()));

    private static string ReadRequiredText(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string column)
    {
        var value = ReadOptionalText(row, headerMap, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BaselineValidationException($"Column '{column}' is required at row {row.RowNumber()}.");
        }

        return value;
    }

    private static string? ReadOptionalText(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string column)
    {
        if (!headerMap.TryGetValue(column, out var columnNumber))
        {
            return null;
        }

        var value = row.Cell(columnNumber).GetString().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? NormalizeRelativePath(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string column)
        => NormalizedPathUtility.NormalizeOptionalPath(ReadOptionalText(row, headerMap, column));

    private static string? NormalizePattern(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string column)
        => ReadOptionalText(row, headerMap, column)?.Replace('\\', '/').Trim();

    private static GuardianFileType ParseFileType(string raw)
        => raw.Trim().ToUpperInvariant() switch
        {
            "AUTO" => GuardianFileType.Auto,
            "JAR" => GuardianFileType.Jar,
            "XML" => GuardianFileType.Xml,
            "YAML" => GuardianFileType.Yaml,
            _ => throw new BaselineValidationException($"Unsupported file_type '{raw}'."),
        };

    private static CompareMode ParseCompareMode(string raw)
    {
        var mode = CompareMode.None;
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            mode |= token.Trim().ToLowerInvariant() switch
            {
                "hash" => CompareMode.Hash,
                "xml-structure" or "xml_structure" => CompareMode.XmlStructure,
                "yaml-structure" or "yaml_structure" => CompareMode.YamlStructure,
                "jar-entry" or "jar-entries" or "jar_entry" => CompareMode.JarEntry,
                "auto" => CompareMode.None,
                _ => throw new BaselineValidationException($"Unsupported compare_mode token '{token}'."),
            };
        }

        return mode;
    }

    private static bool ParseBoolean(string? raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return raw.Trim().ToUpperInvariant() switch
        {
            "Y" or "YES" or "TRUE" or "1" => true,
            "N" or "NO" or "FALSE" or "0" => false,
            _ => throw new BaselineValidationException($"Unsupported boolean value '{raw}'."),
        };
    }

    private static int ParsePriority(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? 1_000 : int.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class BaselineValidator : IBaselineValidator
{
    public void Validate(IReadOnlyList<BaselineRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var duplicateRuleId = rules
            .GroupBy(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateRuleId is not null)
        {
            throw new BaselineValidationException($"Duplicate rule_id '{duplicateRuleId.Key}' was found.");
        }

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleId))
            {
                throw new BaselineValidationException("rule_id must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(rule.RelativePath) && string.IsNullOrWhiteSpace(rule.Pattern))
            {
                throw new BaselineValidationException($"Rule '{rule.RuleId}' must define relative_path or pattern.");
            }
        }
    }
}

public sealed class BaselinePreviewService : IBaselinePreviewService
{
    private readonly IBaselineReader _baselineReader;
    private readonly IBaselineValidator _baselineValidator;

    public BaselinePreviewService(IBaselineReader baselineReader, IBaselineValidator baselineValidator)
    {
        _baselineReader = baselineReader;
        _baselineValidator = baselineValidator;
    }

    public BaselinePreviewSummary Load(string baselinePath)
    {
        var rules = _baselineReader.Read(baselinePath);
        _baselineValidator.Validate(rules);

        return new BaselinePreviewSummary(
            rules.Count,
            rules.Count(static rule => rule.Required),
            rules.Count(static rule => rule.Exclude),
            rules.GroupBy(rule => rule.FileType.ToString(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase));
    }
}
