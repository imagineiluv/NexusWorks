using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NexusWorks.Guardian.Infrastructure;
using NexusWorks.Guardian.Models;

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

        var normalizedPath = NormalizedPathUtility.NormalizeRelativePath(relativePath);
        var preparedRules = _preparedRuleSets.GetValue(rules, CreatePreparedRuleSet);

        var exactMatch = preparedRules.ExactRules.GetValueOrDefault(normalizedPath);
        if (exactMatch is not null)
        {
            return ResolvedRuleFactory.FromBaselineRule(exactMatch, normalizedPath, "exact");
        }

        foreach (var patternRule in preparedRules.PatternRules)
        {
            if (patternRule.Regex.IsMatch(normalizedPath))
            {
                return ResolvedRuleFactory.FromBaselineRule(patternRule.Rule, normalizedPath, "pattern");
            }
        }

        return ResolvedRuleFactory.CreateDefault(normalizedPath);
    }

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

        return new PreparedRuleSet(exactRules, patternRules);
    }

    private static Regex BuildPatternRegex(string pattern)
    {
        var normalizedPattern = pattern.Replace('\\', '/').Trim();
        var regexPattern = Regex.Escape(normalizedPattern)
            .Replace(@"\*\*", "___DOUBLE_WILDCARD___")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", "[^/]")
            .Replace("___DOUBLE_WILDCARD___", ".*");

        return new Regex($"^{regexPattern}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private sealed record PreparedPatternRule(BaselineRule Rule, Regex Regex);

    private sealed record PreparedRuleSet(
        IReadOnlyDictionary<string, BaselineRule> ExactRules,
        IReadOnlyList<PreparedPatternRule> PatternRules);
}

internal static class ResolvedRuleFactory
{
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
        var compareMode = fileType switch
        {
            GuardianFileType.Jar => CompareMode.Hash | CompareMode.JarEntry,
            GuardianFileType.Xml => CompareMode.Hash | CompareMode.XmlStructure,
            GuardianFileType.Yaml => CompareMode.Hash | CompareMode.YamlStructure,
            _ => CompareMode.Hash,
        };

        return new ResolvedRule(
            "AUTO",
            fileType,
            compareMode,
            Required: false,
            DetailCompare: false,
            Exclude: false,
            Source: fileType == GuardianFileType.Auto ? "system-default" : "auto-extension",
            BaselineRule: null);
    }

    private static CompareMode EnsureCompareMode(CompareMode mode, GuardianFileType fileType, bool detailCompare)
    {
        var effectiveMode = mode;
        if (effectiveMode == CompareMode.None)
        {
            effectiveMode = CompareMode.Hash;
        }

        if (fileType == GuardianFileType.Jar && detailCompare)
        {
            effectiveMode |= CompareMode.JarEntry;
        }

        if (fileType == GuardianFileType.Xml && detailCompare)
        {
            effectiveMode |= CompareMode.XmlStructure;
        }

        if (fileType == GuardianFileType.Yaml && detailCompare)
        {
            effectiveMode |= CompareMode.YamlStructure;
        }

        if (fileType == GuardianFileType.Jar && effectiveMode.HasFlag(CompareMode.JarEntry))
        {
            effectiveMode |= CompareMode.Hash;
        }

        if (fileType == GuardianFileType.Xml && effectiveMode.HasFlag(CompareMode.XmlStructure))
        {
            effectiveMode |= CompareMode.Hash;
        }

        if (fileType == GuardianFileType.Yaml && effectiveMode.HasFlag(CompareMode.YamlStructure))
        {
            effectiveMode |= CompareMode.Hash;
        }

        return effectiveMode;
    }
}
