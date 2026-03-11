namespace NexusWorks.Guardian.Models;

public sealed record BaselinePreviewSummary(
    int TotalRuleCount,
    int RequiredRuleCount,
    int ExcludedRuleCount,
    IReadOnlyDictionary<string, int> FileTypeCounts);
