using ClosedXML.Excel;
using NexusWorks.Guardian.Infrastructure;
using NexusWorks.Guardian.Models;
using static NexusWorks.Guardian.GuardianConstants;

namespace NexusWorks.Guardian.Baseline;

public interface IBaselineReader
{
    BaselineWorkbook Read(string baselinePath, CancellationToken cancellationToken = default);
}

public interface IBaselineValidator
{
    void Validate(IReadOnlyList<BaselineRule> rules, CancellationToken cancellationToken = default);
}

public interface IBaselinePreviewService
{
    BaselinePreviewSummary Load(string baselinePath, CancellationToken cancellationToken = default);
}

public sealed class BaselineValidationException : Exception
{
    public BaselineValidationException(string message) : base(message)
    {
    }
}

public sealed class ClosedXmlBaselineReader : IBaselineReader
{
    private static readonly string[] RequiredRulesColumns =
    [
        BaselineColumn.RuleId,
        BaselineColumn.FileType,
        BaselineColumn.Required,
        BaselineColumn.CompareMode,
    ];

    public BaselineWorkbook Read(string baselinePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baselinePath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(baselinePath))
        {
            throw new FileNotFoundException("Baseline file was not found.", baselinePath);
        }

        using var workbook = new XLWorkbook(baselinePath);

        var rules = ReadRulesSheet(workbook);
        var settings = ReadSettingsSheet(workbook);
        var excludes = ReadExcludesSheet(workbook);
        var severityOverrides = ReadSeverityMapSheet(workbook);

        return new BaselineWorkbook(rules, settings, excludes, severityOverrides);
    }

    // ── RULES sheet ──

    private static IReadOnlyList<BaselineRule> ReadRulesSheet(XLWorkbook workbook)
    {
        var worksheet = FindSheet(workbook, RulesSheetName);
        if (worksheet is null)
        {
            throw new BaselineValidationException($"The baseline workbook must contain a {RulesSheetName} sheet.");
        }

        var headerRow = worksheet.FirstRowUsed();
        if (headerRow is null)
        {
            return Array.Empty<BaselineRule>();
        }

        var headerMap = BuildHeaderMap(headerRow);

        foreach (var requiredColumn in RequiredRulesColumns)
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
                ReadRequiredText(row, headerMap, BaselineColumn.RuleId),
                NormalizeRelativePath(row, headerMap, BaselineColumn.RelativePath),
                NormalizePattern(row, headerMap, BaselineColumn.Pattern),
                ParseFileType(ReadOptionalText(row, headerMap, BaselineColumn.FileType) ?? "AUTO"),
                ParseBoolean(ReadRequiredText(row, headerMap, BaselineColumn.Required), false),
                ParseCompareMode(ReadRequiredText(row, headerMap, BaselineColumn.CompareMode)),
                ParseBoolean(ReadOptionalText(row, headerMap, BaselineColumn.DetailCompare), false),
                ParseBoolean(ReadOptionalText(row, headerMap, BaselineColumn.Exclude), false),
                ParsePriority(ReadOptionalText(row, headerMap, BaselineColumn.Priority)),
                ReadOptionalText(row, headerMap, BaselineColumn.Notes));

            rules.Add(rule);
        }

        return rules;
    }

    // ── SETTINGS sheet (optional, key-value pairs) ──

    private static BaselineSettings ReadSettingsSheet(XLWorkbook workbook)
    {
        var worksheet = FindSheet(workbook, SettingsSheetName);
        if (worksheet is null)
        {
            return BaselineSettings.Default;
        }

        var headerRow = worksheet.FirstRowUsed();
        if (headerRow is null)
        {
            return BaselineSettings.Default;
        }

        var headerMap = BuildHeaderMap(headerRow);
        if (!headerMap.ContainsKey("key") || !headerMap.ContainsKey("value"))
        {
            return BaselineSettings.Default;
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (RowIsEmpty(row))
            {
                continue;
            }

            var key = ReadOptionalText(row, headerMap, "key");
            var value = ReadOptionalText(row, headerMap, "value");
            if (!string.IsNullOrWhiteSpace(key) && value is not null)
            {
                settings[key] = value;
            }
        }

        return new BaselineSettings(
            settings.GetValueOrDefault(SettingsKey.ReportTitle),
            settings.GetValueOrDefault(SettingsKey.ProjectName),
            settings.GetValueOrDefault(SettingsKey.DefaultHashAlgorithm));
    }

    // ── EXCLUDES sheet (optional, pattern list) ──

    private static IReadOnlyList<ExcludePattern> ReadExcludesSheet(XLWorkbook workbook)
    {
        var worksheet = FindSheet(workbook, ExcludesSheetName);
        if (worksheet is null)
        {
            return Array.Empty<ExcludePattern>();
        }

        var headerRow = worksheet.FirstRowUsed();
        if (headerRow is null)
        {
            return Array.Empty<ExcludePattern>();
        }

        var headerMap = BuildHeaderMap(headerRow);
        if (!headerMap.ContainsKey(ExcludesColumn.Pattern))
        {
            return Array.Empty<ExcludePattern>();
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var excludes = new List<ExcludePattern>();

        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (RowIsEmpty(row))
            {
                continue;
            }

            var pattern = ReadOptionalText(row, headerMap, ExcludesColumn.Pattern);
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            excludes.Add(new ExcludePattern(
                pattern.Replace('\\', '/').Trim(),
                ReadOptionalText(row, headerMap, ExcludesColumn.Notes)));
        }

        return excludes;
    }

    // ── SEVERITY_MAP sheet (optional, file_type × status → severity) ──

    private static IReadOnlyList<SeverityOverride> ReadSeverityMapSheet(XLWorkbook workbook)
    {
        var worksheet = FindSheet(workbook, SeverityMapSheetName);
        if (worksheet is null)
        {
            return Array.Empty<SeverityOverride>();
        }

        var headerRow = worksheet.FirstRowUsed();
        if (headerRow is null)
        {
            return Array.Empty<SeverityOverride>();
        }

        var headerMap = BuildHeaderMap(headerRow);
        if (!headerMap.ContainsKey(SeverityMapColumn.FileType)
            || !headerMap.ContainsKey(SeverityMapColumn.Status)
            || !headerMap.ContainsKey(SeverityMapColumn.Severity))
        {
            return Array.Empty<SeverityOverride>();
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var overrides = new List<SeverityOverride>();

        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (RowIsEmpty(row))
            {
                continue;
            }

            var fileTypeRaw = ReadOptionalText(row, headerMap, SeverityMapColumn.FileType);
            var statusRaw = ReadOptionalText(row, headerMap, SeverityMapColumn.Status);
            var severityRaw = ReadOptionalText(row, headerMap, SeverityMapColumn.Severity);
            if (fileTypeRaw is null || statusRaw is null || severityRaw is null)
            {
                continue;
            }

            overrides.Add(new SeverityOverride(
                ParseFileType(fileTypeRaw),
                ParseCompareStatus(statusRaw),
                ParseSeverity(severityRaw)));
        }

        return overrides;
    }

    // ── Shared helpers ──

    private static IXLWorksheet? FindSheet(XLWorkbook workbook, string name)
        => workbook.Worksheets.FirstOrDefault(ws => string.Equals(ws.Name, name, StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
        => headerRow.CellsUsed()
            .Select(cell => new { Index = cell.Address.ColumnNumber, Header = cell.GetString().Trim().ToLowerInvariant() })
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Header))
            .ToDictionary(static entry => entry.Header, static entry => entry.Index, StringComparer.OrdinalIgnoreCase);

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

    private static CompareStatus ParseCompareStatus(string raw)
        => raw.Trim().ToUpperInvariant() switch
        {
            "OK" => CompareStatus.Ok,
            "CHANGED" => CompareStatus.Changed,
            "ADDED" => CompareStatus.Added,
            "REMOVED" => CompareStatus.Removed,
            "MISSING_REQUIRED" or "MISSINGREQUIRED" => CompareStatus.MissingRequired,
            "ERROR" => CompareStatus.Error,
            _ => throw new BaselineValidationException($"Unsupported status '{raw}'."),
        };

    private static Severity ParseSeverity(string raw)
        => raw.Trim().ToUpperInvariant() switch
        {
            "LOW" => Severity.Low,
            "MEDIUM" => Severity.Medium,
            "HIGH" => Severity.High,
            "CRITICAL" => Severity.Critical,
            _ => throw new BaselineValidationException($"Unsupported severity '{raw}'."),
        };

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
        => string.IsNullOrWhiteSpace(raw) ? DefaultPriority : int.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class BaselineValidator : IBaselineValidator
{
    public void Validate(IReadOnlyList<BaselineRule> rules, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        cancellationToken.ThrowIfCancellationRequested();

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

            ValidateFileTypeCompareModeCompatibility(rule);
        }
    }

    private static void ValidateFileTypeCompareModeCompatibility(BaselineRule rule)
    {
        if (rule.FileType == GuardianFileType.Auto || rule.CompareMode == CompareMode.None || rule.CompareMode == CompareMode.Hash)
        {
            return;
        }

        var hasJarEntry = rule.CompareMode.HasFlag(CompareMode.JarEntry);
        var hasXmlStructure = rule.CompareMode.HasFlag(CompareMode.XmlStructure);
        var hasYamlStructure = rule.CompareMode.HasFlag(CompareMode.YamlStructure);

        if (hasJarEntry && rule.FileType != GuardianFileType.Jar)
        {
            throw new BaselineValidationException(
                $"Rule '{rule.RuleId}': compare_mode 'JarEntry' requires file_type 'Jar', but found '{rule.FileType}'.");
        }

        if (hasXmlStructure && rule.FileType != GuardianFileType.Xml)
        {
            throw new BaselineValidationException(
                $"Rule '{rule.RuleId}': compare_mode 'XmlStructure' requires file_type 'Xml', but found '{rule.FileType}'.");
        }

        if (hasYamlStructure && rule.FileType != GuardianFileType.Yaml)
        {
            throw new BaselineValidationException(
                $"Rule '{rule.RuleId}': compare_mode 'YamlStructure' requires file_type 'Yaml', but found '{rule.FileType}'.");
        }

        // Mutually exclusive structural modes
        var structuralModeCount = (hasJarEntry ? 1 : 0) + (hasXmlStructure ? 1 : 0) + (hasYamlStructure ? 1 : 0);
        if (structuralModeCount > 1)
        {
            throw new BaselineValidationException(
                $"Rule '{rule.RuleId}': only one structural compare_mode (JarEntry, XmlStructure, YamlStructure) can be used at a time.");
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

    public BaselinePreviewSummary Load(string baselinePath, CancellationToken cancellationToken = default)
    {
        var workbook = _baselineReader.Read(baselinePath, cancellationToken);
        _baselineValidator.Validate(workbook.Rules, cancellationToken);

        return new BaselinePreviewSummary(
            workbook.Rules.Count,
            workbook.Rules.Count(static rule => rule.Required),
            workbook.Rules.Count(static rule => rule.Exclude),
            workbook.Rules.GroupBy(rule => rule.FileType.ToString(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase));
    }
}
