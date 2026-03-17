namespace NexusWorks.Guardian;

/// <summary>
/// Centralizes domain-specific magic strings and values used across the Guardian pipeline.
/// </summary>
public static class GuardianConstants
{
    // ── Rule Sources ──

    public static class RuleSource
    {
        public const string Exact = "exact";
        public const string Pattern = "pattern";
        public const string SystemDefault = "system-default";
        public const string AutoExtension = "auto-extension";
        public const string PatternUnmatched = "pattern-unmatched";
    }

    /// <summary>Rule ID assigned to files with no matching baseline rule.</summary>
    public const string AutoRuleId = "AUTO";

    // ── Baseline Worksheet ──

    /// <summary>Name of the Excel worksheet containing baseline rules.</summary>
    public const string RulesSheetName = "RULES";

    // ── Baseline Worksheets ──

    /// <summary>Name of the optional settings worksheet.</summary>
    public const string SettingsSheetName = "SETTINGS";

    /// <summary>Name of the optional global excludes worksheet.</summary>
    public const string ExcludesSheetName = "EXCLUDES";

    /// <summary>Name of the optional severity override worksheet.</summary>
    public const string SeverityMapSheetName = "SEVERITY_MAP";

    // ── Baseline Column Names ──

    public static class BaselineColumn
    {
        public const string RuleId = "rule_id";
        public const string RelativePath = "relative_path";
        public const string Pattern = "pattern";
        public const string FileType = "file_type";
        public const string Required = "required";
        public const string CompareMode = "compare_mode";
        public const string DetailCompare = "detail_compare";
        public const string Exclude = "exclude";
        public const string Priority = "priority";
        public const string Notes = "notes";
    }

    public static class SettingsKey
    {
        public const string ReportTitle = "report_title";
        public const string ProjectName = "project_name";
        public const string DefaultHashAlgorithm = "default_hash_algorithm";
    }

    public static class ExcludesColumn
    {
        public const string Pattern = "pattern";
        public const string Notes = "notes";
    }

    public static class SeverityMapColumn
    {
        public const string FileType = "file_type";
        public const string Status = "status";
        public const string Severity = "severity";
    }

    // ── File Extensions ──

    public static class FileExtension
    {
        public const string Jar = ".jar";
        public const string Xml = ".xml";
        public const string Yaml = ".yaml";
        public const string Yml = ".yml";
        public const string Class = ".class";
    }

    // ── JAR Archive Paths ──

    public static class JarPath
    {
        public const string ManifestPath = "META-INF/MANIFEST.MF";
        public const string BootInfClassesPrefix = "BOOT-INF/classes/";
        public const string WebInfClassesPrefix = "WEB-INF/classes/";
        public const string RootPackageName = "(root)";
    }

    /// <summary>Default priority for rules that do not specify one.</summary>
    public const int DefaultPriority = 1_000;
}
