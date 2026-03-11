using System.Collections.Concurrent;
using System.Diagnostics;
using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Comparison;
using NexusWorks.Guardian.Infrastructure;
using NexusWorks.Guardian.Inventory;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.RuleResolution;

namespace NexusWorks.Guardian.Orchestration;

public sealed class GuardianComparisonEngine
{
    private readonly IBaselineReader _baselineReader;
    private readonly IBaselineValidator _baselineValidator;
    private readonly IInventoryScanner _inventoryScanner;
    private readonly IRuleResolver _ruleResolver;
    private readonly IFileComparer _fileComparer;

    public GuardianComparisonEngine(
        IBaselineReader baselineReader,
        IBaselineValidator baselineValidator,
        IInventoryScanner inventoryScanner,
        IRuleResolver ruleResolver,
        IFileComparer fileComparer)
    {
        _baselineReader = baselineReader;
        _baselineValidator = baselineValidator;
        _inventoryScanner = inventoryScanner;
        _ruleResolver = ruleResolver;
        _fileComparer = fileComparer;
    }

    public ComparisonExecutionResult Execute(ComparisonExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = DateTimeOffset.UtcNow;
        var totalStopwatch = Stopwatch.StartNew();
        var stageMetrics = new List<ExecutionStageMetric>();

        var baselineStopwatch = Stopwatch.StartNew();
        var rules = _baselineReader.Read(request.BaselinePath);
        _baselineValidator.Validate(rules);
        baselineStopwatch.Stop();
        stageMetrics.Add(CreateStageMetric("Baseline Load", rules.Count, 1, baselineStopwatch.Elapsed));

        var currentScanStopwatch = Stopwatch.StartNew();
        var currentInventory = _inventoryScanner.Scan(request.CurrentRootPath, cancellationToken);
        currentScanStopwatch.Stop();
        stageMetrics.Add(CreateStageMetric(
            "Current Inventory Scan",
            currentInventory.Count,
            GuardianPerformanceTuning.GetWorkerCount(currentInventory.Count),
            currentScanStopwatch.Elapsed));

        var patchScanStopwatch = Stopwatch.StartNew();
        var patchInventory = _inventoryScanner.Scan(request.PatchRootPath, cancellationToken);
        patchScanStopwatch.Stop();
        stageMetrics.Add(CreateStageMetric(
            "Patch Inventory Scan",
            patchInventory.Count,
            GuardianPerformanceTuning.GetWorkerCount(patchInventory.Count),
            patchScanStopwatch.Elapsed));

        var candidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        candidatePaths.UnionWith(currentInventory.Keys);
        candidatePaths.UnionWith(patchInventory.Keys);
        candidatePaths.UnionWith(rules
            .Where(static rule => !string.IsNullOrWhiteSpace(rule.RelativePath))
            .Select(static rule => NormalizedPathUtility.NormalizeRelativePath(rule.RelativePath!)));

        var orderedCandidatePaths = candidatePaths
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matchedRuleIds = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var primaryItems = new ComparisonItemResult?[orderedCandidatePaths.Length];
        var comparedItemCount = 0;
        var compareConcurrency = GuardianPerformanceTuning.GetWorkerCount(orderedCandidatePaths.Length);
        var compareStopwatch = Stopwatch.StartNew();
        Parallel.For(0, orderedCandidatePaths.Length, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = compareConcurrency,
        }, index =>
        {
            var relativePath = orderedCandidatePaths[index];
            var resolvedRule = _ruleResolver.Resolve(relativePath, rules);
            if (resolvedRule.Exclude)
            {
                return;
            }

            currentInventory.TryGetValue(relativePath, out var current);
            patchInventory.TryGetValue(relativePath, out var patch);
            if (resolvedRule.BaselineRule is not null)
            {
                matchedRuleIds.TryAdd(resolvedRule.BaselineRule.RuleId, 0);
            }

            primaryItems[index] = _fileComparer.Compare(relativePath, resolvedRule, current, patch);
            Interlocked.Increment(ref comparedItemCount);
        });
        compareStopwatch.Stop();
        stageMetrics.Add(CreateStageMetric("Candidate Compare", comparedItemCount, compareConcurrency, compareStopwatch.Elapsed));

        var items = primaryItems
            .Where(static item => item is not null)
            .Select(static item => item!)
            .ToList();

        var unmatchedRequiredRules = rules
            .Where(static rule => rule.Required
                && !rule.Exclude
                && string.IsNullOrWhiteSpace(rule.RelativePath)
                && !string.IsNullOrWhiteSpace(rule.Pattern))
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase)
            .Where(rule => !matchedRuleIds.ContainsKey(rule.RuleId))
            .ToArray();

        if (unmatchedRequiredRules.Length > 0)
        {
            var backfillItems = new ComparisonItemResult[unmatchedRequiredRules.Length];
            var backfillConcurrency = GuardianPerformanceTuning.GetWorkerCount(unmatchedRequiredRules.Length);
            var backfillStopwatch = Stopwatch.StartNew();
            Parallel.For(0, unmatchedRequiredRules.Length, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = backfillConcurrency,
            }, index =>
            {
                var rule = unmatchedRequiredRules[index];
                var syntheticPath = NormalizedPathUtility.NormalizeRelativePath(rule.Pattern!);
                var resolvedRule = ResolvedRuleFactory.FromBaselineRule(rule, syntheticPath, "pattern-unmatched");
                backfillItems[index] = _fileComparer.Compare(syntheticPath, resolvedRule, current: null, patch: null);
            });
            backfillStopwatch.Stop();
            stageMetrics.Add(CreateStageMetric("Missing Required Backfill", backfillItems.Length, backfillConcurrency, backfillStopwatch.Elapsed));
            items.AddRange(backfillItems);
        }

        totalStopwatch.Stop();
        var completedAt = DateTimeOffset.UtcNow;
        var performance = new ExecutionPerformanceSummary(totalStopwatch.Elapsed.TotalMilliseconds, stageMetrics.ToArray());
        return new ComparisonExecutionResult(startedAt, completedAt, items, performance);
    }

    private static ExecutionStageMetric CreateStageMetric(string stageName, int itemCount, int concurrency, TimeSpan elapsed)
        => new(stageName, itemCount, elapsed.TotalMilliseconds, Math.Max(1, concurrency));
}
