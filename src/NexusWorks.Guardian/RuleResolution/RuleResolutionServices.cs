using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NexusWorks.Guardian.Infrastructure;
using NexusWorks.Guardian.Models;
using static NexusWorks.Guardian.GuardianConstants;

namespace NexusWorks.Guardian.RuleResolution;

public interface IRuleResolver
{
    ResolvedRule Resolve(string relativePath, IReadOnlyList<BaselineRule> rules);
}

public sealed class BaselineRuleResolver : IRuleResolver
{
    private readonly ConditionalWeakTable<IReadOnlyList<BaselineRule>, PreparedRuleSet> _preparedRuleSets = new();

    public ResolvedRule Resolve(string relativePath, IReadOnlyList<BaselineRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(rules);

        // Engine candidate paths are pre-normalized; skip redundant work when possible
        var normalizedPath = IsAlreadyNormalized(relativePath)
            ? relativePath
            : NormalizedPathUtility.NormalizeRelativePath(relativePath);
        var preparedRules = _preparedRuleSets.GetValue(rules, CreatePreparedRuleSet);

        if (preparedRules.ExactRules.TryGetValue(normalizedPath, out var exactMatch))
        {
            return ResolvedRuleFactory.FromBaselineRule(exactMatch, normalizedPath, RuleSource.Exact);
        }

        foreach (var patternRule in preparedRules.PatternRules)
        {
            if (patternRule.Regex.IsMatch(normalizedPath))
            {
                return ResolvedRuleFactory.FromBaselineRule(patternRule.Rule, normalizedPath, RuleSource.Pattern);
            }
        }

        return ResolvedRuleFactory.CreateDefault(normalizedPath);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAlreadyNormalized(string path)
        => path.Length > 0
           && !path.Contains('\\')
           && path[0] != '/'
           && !path.StartsWith("./", StringComparison.Ordinal);

    private static PreparedRuleSet CreatePreparedRuleSet(IReadOnlyList<BaselineRule> rules)
    {
        var exactRules = new Dictionary<string, BaselineRule>(StringComparer.OrdinalIgnoreCase);
        var patternRules = new List<PreparedPatternRule>();

        foreach (var rule in rules
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(rule.RelativePath))
            {
                exactRules.TryAdd(NormalizedPathUtility.NormalizeRelativePath(rule.RelativePath!), rule);
            }

            if (!string.IsNullOrWhiteSpace(rule.Pattern))
            {
                patternRules.Add(new PreparedPatternRule(rule, BuildPatternRegex(rule.Pattern!)));
            }
        }

        return new PreparedRuleSet(exactRules.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase), patternRules);
    }

    private static Regex BuildPatternRegex(string pattern)
    {
        var normalizedPattern = pattern.Replace('\\', '/').Trim();
        var regexPattern = Regex.Escape(normalizedPattern)
            .Replace(@"\*\*", "___DOUBLE_WILDCARD___")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", "[^/]")
            .Replace("___DOUBLE_WILDCARD___", ".*");

        return new Regex($"^{regexPattern}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private sealed record PreparedPatternRule(BaselineRule Rule, Regex Regex);

    private sealed record PreparedRuleSet(
        FrozenDictionary<string, BaselineRule> ExactRules,
        IReadOnlyList<PreparedPatternRule> PatternRules);
}

internal static class ResolvedRuleFactory
{
    /// <summary>Maps each structural file type to its corresponding structural CompareMode.</summary>
    private static readonly Dictionary<GuardianFileType, CompareMode> StructuralModeMap = new()
    {
        [GuardianFileType.Jar] = CompareMode.JarEntry,
        [GuardianFileType.Xml] = CompareMode.XmlStructure,
        [GuardianFileType.Yaml] = CompareMode.YamlStructure,
    };

    public static ResolvedRule FromBaselineRule(BaselineRule rule, string relativePath, string source)
    {
        var effectiveFileType = rule.FileType == GuardianFileType.Auto
            ? NormalizedPathUtility.InferFileType(relativePath)
            : rule.FileType;

        var effectiveCompareMode = EnsureCompareMode(rule.CompareMode, effectiveFileType, rule.DetailCompare);

        return new ResolvedRule(
            rule.RuleId,
            effectiveFileType,
            effectiveCompareMode,
            rule.Required,
            rule.DetailCompare,
            rule.Exclude,
            source,
            rule);
    }

    public static ResolvedRule CreateDefault(string relativePath)
    {
        var fileType = NormalizedPathUtility.InferFileType(relativePath);
        var compareMode = StructuralModeMap.TryGetValue(fileType, out var structuralMode)
            ? CompareMode.Hash | structuralMode
            : CompareMode.Hash;

        return new ResolvedRule(
            AutoRuleId,
            fileType,
            compareMode,
            Required: false,
            DetailCompare: false,
            Exclude: false,
            Source: fileType == GuardianFileType.Auto ? RuleSource.SystemDefault : RuleSource.AutoExtension,
            BaselineRule: null);
    }

    private static CompareMode EnsureCompareMode(CompareMode mode, GuardianFileType fileType, bool detailCompare)
    {
        var effectiveMode = mode;
        if (effectiveMode == CompareMode.None)
        {
            effectiveMode = CompareMode.Hash;
        }

        if (StructuralModeMap.TryGetValue(fileType, out var structuralMode))
        {
            if (detailCompare)
            {
                effectiveMode |= structuralMode;
            }

            if (effectiveMode.HasFlag(structuralMode))
            {
                effectiveMode |= CompareMode.Hash;
            }
        }

        return effectiveMode;
    }
}
