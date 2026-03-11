using System.Collections.ObjectModel;

namespace NexusWorks.Guardian.Models;

public enum GuardianFileType
{
    Auto,
    Jar,
    Xml,
    Yaml,
}

[Flags]
public enum CompareMode
{
    None = 0,
    Hash = 1,
    XmlStructure = 2,
    JarEntry = 4,
    YamlStructure = 8,
}

public enum CompareStatus
{
    Ok,
    Changed,
    Added,
    Removed,
    MissingRequired,
    Error,
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical,
}

public sealed record BaselineRule(
    string RuleId,
    string? RelativePath,
    string? Pattern,
    GuardianFileType FileType,
    bool Required,
    CompareMode CompareMode,
    bool DetailCompare,
    bool Exclude,
    int Priority,
    string? Notes);

public sealed record ResolvedRule(
    string RuleId,
    GuardianFileType FileType,
    CompareMode CompareMode,
    bool Required,
    bool DetailCompare,
    bool Exclude,
    string Source,
    BaselineRule? BaselineRule);

public sealed record FileInventoryEntry(
    string RelativePath,
    string AbsolutePath,
    long Size,
    DateTimeOffset LastWriteTimeUtc,
    string Hash);

public sealed record JarCompareDetail(
    IReadOnlyList<string> AddedEntries,
    IReadOnlyList<string> RemovedEntries,
    IReadOnlyList<string> ChangedEntries,
    bool ManifestChanged,
    int AddedClassCount,
    int RemovedClassCount,
    int ChangedClassCount,
    IReadOnlyList<JarPackageChangeSummary> PackageSummaries)
{
    public int TotalDifferenceCount => AddedEntries.Count + RemovedEntries.Count + ChangedEntries.Count;
    public bool IsEquivalent => TotalDifferenceCount == 0;
}

public sealed record JarPackageChangeSummary(
    string PackageName,
    int AddedClassCount,
    int RemovedClassCount,
    int ChangedClassCount)
{
    public int TotalChangedCount => AddedClassCount + RemovedClassCount + ChangedClassCount;
}

public sealed record XmlChangeEntry(
    string Path,
    string ChangeKind,
    string? CurrentValue,
    string? PatchValue);

public sealed record XmlCompareDetail(
    IReadOnlyList<string> ChangedXPaths,
    int AddedNodes,
    int RemovedNodes,
    int ChangedNodeCount,
    IReadOnlyList<XmlChangeEntry> Changes)
{
    public bool IsEquivalent => AddedNodes == 0 && RemovedNodes == 0 && ChangedNodeCount == 0;
}

public sealed record YamlCompareDetail(
    IReadOnlyList<string> ChangedPaths,
    int AddedKeys,
    int RemovedKeys,
    int ChangedNodeCount)
{
    public bool IsEquivalent => AddedKeys == 0 && RemovedKeys == 0 && ChangedNodeCount == 0;
}

public sealed record ComparisonItemResult(
    string RelativePath,
    string RuleId,
    GuardianFileType FileType,
    CompareMode CompareMode,
    CompareStatus Status,
    Severity Severity,
    string Summary,
    bool CurrentExists,
    bool PatchExists,
    string? CurrentHash,
    string? PatchHash,
    JarCompareDetail? JarDetail,
    XmlCompareDetail? XmlDetail,
    YamlCompareDetail? YamlDetail,
    IReadOnlyList<string> Messages)
{
    public IReadOnlyList<string> Messages { get; init; } = Messages.Count == 0
        ? Array.Empty<string>()
        : new ReadOnlyCollection<string>(Messages.ToList());
}

public sealed record ComparisonExecutionRequest(
    string CurrentRootPath,
    string PatchRootPath,
    string BaselinePath);

public sealed record ComparisonExecutionResult(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    IReadOnlyList<ComparisonItemResult> Items,
    ExecutionPerformanceSummary? Performance = null)
{
    public IReadOnlyDictionary<CompareStatus, int> StatusCounts => Items
        .GroupBy(item => item.Status)
        .ToDictionary(group => group.Key, group => group.Count());

    public double TotalDurationMs => Performance?.TotalDurationMs > 0
        ? Performance.TotalDurationMs
        : Math.Max(0, (CompletedAt - StartedAt).TotalMilliseconds);

    public IReadOnlyList<ExecutionStageMetric> StageMetrics => Performance?.Stages ?? Array.Empty<ExecutionStageMetric>();
}

public sealed record StatusEvaluationContext(
    bool Required,
    bool CurrentExists,
    bool PatchExists,
    bool IsEquivalent,
    bool HasDifferences,
    GuardianFileType FileType,
    bool HasError = false);
