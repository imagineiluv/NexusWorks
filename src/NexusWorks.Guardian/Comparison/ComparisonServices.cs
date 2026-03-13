using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using NexusWorks.Guardian.Evaluation;
using NexusWorks.Guardian.Infrastructure;
using NexusWorks.Guardian.Models;
using YamlDotNet.Serialization;

namespace NexusWorks.Guardian.Comparison;

public interface IHashProvider
{
    string Compute(string filePath);
}

public interface IJarComparer
{
    JarCompareDetail Compare(string currentPath, string patchPath);
}

public interface IXmlComparer
{
    XmlCompareDetail Compare(string currentPath, string patchPath);
}

public interface IYamlComparer
{
    YamlCompareDetail Compare(string currentPath, string patchPath);
}

public interface IFileComparer
{
    ComparisonItemResult Compare(string relativePath, ResolvedRule rule, FileInventoryEntry? current, FileInventoryEntry? patch);
}

public sealed class Sha256HashProvider : IHashProvider
{
    public string Compute(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed class JarComparer : IJarComparer
{
    public JarCompareDetail Compare(string currentPath, string patchPath)
    {
        var currentEntries = ReadEntries(currentPath);
        var patchEntries = ReadEntries(patchPath);

        var added = new List<string>();
        var removed = new List<string>();
        var changed = new List<string>();

        var allPaths = new HashSet<string>(currentEntries.Keys, StringComparer.OrdinalIgnoreCase);
        allPaths.UnionWith(patchEntries.Keys);

        foreach (var entryPath in allPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var hasCurrent = currentEntries.TryGetValue(entryPath, out var currentHash);
            var hasPatch = patchEntries.TryGetValue(entryPath, out var patchHash);

            if (hasCurrent && !hasPatch)
            {
                removed.Add(entryPath);
                continue;
            }

            if (!hasCurrent && hasPatch)
            {
                added.Add(entryPath);
                continue;
            }

            if (!string.Equals(currentHash, patchHash, StringComparison.OrdinalIgnoreCase))
            {
                changed.Add(entryPath);
            }
        }

        var manifestChanged = changed.Contains("META-INF/MANIFEST.MF", StringComparer.OrdinalIgnoreCase)
            || added.Contains("META-INF/MANIFEST.MF", StringComparer.OrdinalIgnoreCase)
            || removed.Contains("META-INF/MANIFEST.MF", StringComparer.OrdinalIgnoreCase);

        var addedClasses = added.Where(IsClassEntry).ToArray();
        var removedClasses = removed.Where(IsClassEntry).ToArray();
        var changedClasses = changed.Where(IsClassEntry).ToArray();
        var packageSummaries = BuildPackageSummaries(addedClasses, removedClasses, changedClasses);

        return new JarCompareDetail(
            added,
            removed,
            changed,
            manifestChanged,
            addedClasses.Length,
            removedClasses.Length,
            changedClasses.Length,
            packageSummaries);
    }

    /// <summary>Maximum number of entries allowed in a single JAR/ZIP archive.</summary>
    private const int MaxZipEntryCount = 100_000;

    /// <summary>Maximum uncompressed size per entry (256 MB).</summary>
    private const long MaxUncompressedEntrySize = 256 * 1024 * 1024;

    private static Dictionary<string, string> ReadEntries(string jarPath)
    {
        using var fileStream = File.OpenRead(jarPath);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);

        if (archive.Entries.Count > MaxZipEntryCount)
        {
            throw new InvalidOperationException(
                $"JAR archive '{Path.GetFileName(jarPath)}' contains {archive.Entries.Count} entries, exceeding the safety limit of {MaxZipEntryCount}.");
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries.Where(static entry => !string.IsNullOrEmpty(entry.Name)))
        {
            if (entry.Length > MaxUncompressedEntrySize)
            {
                throw new InvalidOperationException(
                    $"JAR entry '{entry.FullName}' in '{Path.GetFileName(jarPath)}' has uncompressed size {entry.Length:N0} bytes, exceeding the safety limit of {MaxUncompressedEntrySize:N0} bytes.");
            }

            using var entryStream = entry.Open();
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(entryStream);
            result[entry.FullName.Replace('\\', '/')] = Convert.ToHexString(hash).ToLowerInvariant();
        }

        return result;
    }

    private static bool IsClassEntry(string entryPath)
        => entryPath.EndsWith(".class", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<JarPackageChangeSummary> BuildPackageSummaries(
        IReadOnlyList<string> addedClasses,
        IReadOnlyList<string> removedClasses,
        IReadOnlyList<string> changedClasses)
    {
        var packageMap = new Dictionary<string, (int Added, int Removed, int Changed)>(StringComparer.OrdinalIgnoreCase);

        void AddRange(IEnumerable<string> entries, Action<string> update)
        {
            foreach (var entry in entries)
            {
                update(GetPackageName(entry));
            }
        }

        AddRange(addedClasses, package =>
        {
            packageMap.TryGetValue(package, out var counts);
            packageMap[package] = counts with { Added = counts.Added + 1 };
        });

        AddRange(removedClasses, package =>
        {
            packageMap.TryGetValue(package, out var counts);
            packageMap[package] = counts with { Removed = counts.Removed + 1 };
        });

        AddRange(changedClasses, package =>
        {
            packageMap.TryGetValue(package, out var counts);
            packageMap[package] = counts with { Changed = counts.Changed + 1 };
        });

        return packageMap
            .Select(pair => new JarPackageChangeSummary(pair.Key, pair.Value.Added, pair.Value.Removed, pair.Value.Changed))
            .OrderByDescending(static summary => summary.TotalChangedCount)
            .ThenBy(static summary => summary.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetPackageName(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/');
        normalized = normalized switch
        {
            var path when path.StartsWith("BOOT-INF/classes/", StringComparison.OrdinalIgnoreCase) => path["BOOT-INF/classes/".Length..],
            var path when path.StartsWith("WEB-INF/classes/", StringComparison.OrdinalIgnoreCase) => path["WEB-INF/classes/".Length..],
            _ => normalized,
        };

        var directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(directory))
        {
            return "(root)";
        }

        return directory.Replace('/', '.');
    }
}

public sealed class XmlComparer : IXmlComparer
{
    public XmlCompareDetail Compare(string currentPath, string patchPath)
    {
        var currentDocument = XDocument.Load(currentPath, LoadOptions.None);
        var patchDocument = XDocument.Load(patchPath, LoadOptions.None);

        if (currentDocument.Root is null || patchDocument.Root is null)
        {
            throw new InvalidOperationException("Both XML files must contain a root element.");
        }

        var currentNode = Normalize(currentDocument.Root);
        var patchNode = Normalize(patchDocument.Root);

        var changedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var counters = new XmlDifferenceCounters();
        CompareNodes(currentNode, patchNode, $"/{currentNode.Name}[1]", changedPaths, counters);

        return new XmlCompareDetail(
            changedPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
            counters.AddedNodes,
            counters.RemovedNodes,
            counters.ChangedNodeCount,
            counters.Changes.ToArray());
    }

    private static NormalizedXmlNode Normalize(XElement element)
    {
        var attributes = element.Attributes()
            .OrderBy(attribute => attribute.Name.ToString(), StringComparer.Ordinal)
            .ToDictionary(attribute => attribute.Name.ToString(), attribute => attribute.Value.Trim(), StringComparer.Ordinal);

        var children = element.Elements().Select(Normalize).ToArray();
        var textValue = string.Concat(element.Nodes().OfType<XText>().Select(text => text.Value)).Trim();

        return new NormalizedXmlNode(
            element.Name.ToString(),
            attributes,
            string.IsNullOrWhiteSpace(textValue) ? null : textValue,
            children);
    }

    private static void CompareNodes(
        NormalizedXmlNode current,
        NormalizedXmlNode patch,
        string path,
        ISet<string> changedPaths,
        XmlDifferenceCounters counters)
    {
        if (!string.Equals(current.Name, patch.Name, StringComparison.Ordinal))
        {
            RegisterChangedPath(changedPaths, path);
            counters.RecordChange(path, "node-name", current.Name, patch.Name);
            return;
        }

        if (!DictionaryEquals(current.Attributes, patch.Attributes))
        {
            RegisterChangedPath(changedPaths, $"{path}/@attributes");
            counters.RecordChange(
                $"{path}/@attributes",
                "attributes",
                FormatAttributes(current.Attributes),
                FormatAttributes(patch.Attributes));
        }

        if (!string.Equals(current.TextValue, patch.TextValue, StringComparison.Ordinal))
        {
            RegisterChangedPath(changedPaths, $"{path}/text()");
            counters.RecordChange($"{path}/text()", "text", current.TextValue, patch.TextValue);
        }

        var childCount = Math.Max(current.Children.Count, patch.Children.Count);
        var nameCounters = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < childCount; index++)
        {
            var currentChild = index < current.Children.Count ? current.Children[index] : null;
            var patchChild = index < patch.Children.Count ? patch.Children[index] : null;
            var childName = currentChild?.Name ?? patchChild?.Name ?? "node";
            var occurrence = nameCounters.TryGetValue(childName, out var existing) ? existing + 1 : 1;
            nameCounters[childName] = occurrence;
            var childPath = $"{path}/{childName}[{occurrence}]";

            if (currentChild is null)
            {
                RegisterChangedPath(changedPaths, childPath);
                counters.RecordAdded(childPath, DescribeNode(patchChild));
                continue;
            }

            if (patchChild is null)
            {
                RegisterChangedPath(changedPaths, childPath);
                counters.RecordRemoved(childPath, DescribeNode(currentChild));
                continue;
            }

            CompareNodes(currentChild, patchChild, childPath, changedPaths, counters);
        }
    }

    private static bool DictionaryEquals(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue))
            {
                return false;
            }

            if (!string.Equals(pair.Value, rightValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void RegisterChangedPath(ISet<string> changedPaths, string path)
        => changedPaths.Add(path);

    private static string FormatAttributes(IReadOnlyDictionary<string, string> attributes)
        => string.Join(", ", attributes.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(static pair => $"{pair.Key}={pair.Value}"));

    private static string DescribeNode(NormalizedXmlNode? node)
    {
        if (node is null)
        {
            return "(null)";
        }

        if (!string.IsNullOrWhiteSpace(node.TextValue))
        {
            return $"{node.Name}:{node.TextValue}";
        }

        if (node.Attributes.Count > 0)
        {
            return $"{node.Name} [{FormatAttributes(node.Attributes)}]";
        }

        return node.Name;
    }

    private sealed record NormalizedXmlNode(
        string Name,
        IReadOnlyDictionary<string, string> Attributes,
        string? TextValue,
        IReadOnlyList<NormalizedXmlNode> Children);

    private sealed class XmlDifferenceCounters
    {
        public int AddedNodes { get; set; }
        public int RemovedNodes { get; set; }
        public int ChangedNodeCount { get; set; }
        public List<XmlChangeEntry> Changes { get; } = [];

        public void RecordAdded(string path, string? patchValue)
        {
            AddedNodes++;
            Changes.Add(new XmlChangeEntry(path, "added", null, patchValue));
        }

        public void RecordRemoved(string path, string? currentValue)
        {
            RemovedNodes++;
            Changes.Add(new XmlChangeEntry(path, "removed", currentValue, null));
        }

        public void RecordChange(string path, string kind, string? currentValue, string? patchValue)
        {
            ChangedNodeCount++;
            Changes.Add(new XmlChangeEntry(path, kind, currentValue, patchValue));
        }
    }
}

public sealed class YamlComparer : IYamlComparer
{
    private readonly ThreadLocal<IDeserializer> _deserializer = new(static () =>
        new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build());

    public YamlCompareDetail Compare(string currentPath, string patchPath)
    {
        var deserializer = _deserializer.Value ?? new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        using var currentReader = File.OpenText(currentPath);
        using var patchReader = File.OpenText(patchPath);

        var currentNode = Normalize(deserializer.Deserialize<object?>(currentReader));
        var patchNode = Normalize(deserializer.Deserialize<object?>(patchReader));

        var changedPaths = new HashSet<string>(StringComparer.Ordinal);
        var counters = new YamlDifferenceCounters();
        CompareNodes(currentNode, patchNode, string.Empty, changedPaths, counters);

        return new YamlCompareDetail(
            changedPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            counters.AddedKeys,
            counters.RemovedKeys,
            counters.ChangedNodeCount);
    }

    private static NormalizedYamlNode Normalize(object? value)
        => value switch
        {
            null => new ScalarYamlNode("null", null),
            string text => new ScalarYamlNode("string", text),
            bool boolean => new ScalarYamlNode("bool", boolean ? "true" : "false"),
            byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal => new ScalarYamlNode("number", Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)),
            DateTime dateTime => new ScalarYamlNode("datetime", dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
            DateTimeOffset dateTimeOffset => new ScalarYamlNode("datetimeoffset", dateTimeOffset.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
            IDictionary<object, object> mapping => new MappingYamlNode(
                mapping
                    .OrderBy(entry => NormalizeKey(entry.Key), StringComparer.Ordinal)
                    .ToDictionary(
                        entry => NormalizeKey(entry.Key),
                        entry => Normalize(entry.Value),
                        StringComparer.Ordinal)),
            IEnumerable<object> sequence => new SequenceYamlNode(sequence.Select(Normalize).ToArray()),
            _ => new ScalarYamlNode(value.GetType().Name, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)),
        };

    private static string NormalizeKey(object? key)
        => key switch
        {
            null => "null",
            IDictionary<object, object> or IList<object> => throw new NotSupportedException("Complex YAML mapping keys are not supported."),
            string text => text,
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => key.ToString() ?? string.Empty,
        };

    private static void CompareNodes(
        NormalizedYamlNode current,
        NormalizedYamlNode patch,
        string path,
        ISet<string> changedPaths,
        YamlDifferenceCounters counters)
    {
        if (current.GetType() != patch.GetType())
        {
            changedPaths.Add(NormalizeReportedPath(path));
            counters.ChangedNodeCount++;
            return;
        }

        switch (current)
        {
            case ScalarYamlNode leftScalar when patch is ScalarYamlNode rightScalar:
                if (!string.Equals(leftScalar.TypeName, rightScalar.TypeName, StringComparison.Ordinal)
                    || !string.Equals(leftScalar.Value, rightScalar.Value, StringComparison.Ordinal))
                {
                    changedPaths.Add(NormalizeReportedPath(path));
                    counters.ChangedNodeCount++;
                }

                return;

            case MappingYamlNode leftMap when patch is MappingYamlNode rightMap:
            {
                var keys = new HashSet<string>(leftMap.Properties.Keys, StringComparer.Ordinal);
                keys.UnionWith(rightMap.Properties.Keys);

                foreach (var key in keys.OrderBy(static key => key, StringComparer.Ordinal))
                {
                    var childPath = AppendPropertyPath(path, key);
                    var hasCurrent = leftMap.Properties.TryGetValue(key, out var currentValue);
                    var hasPatch = rightMap.Properties.TryGetValue(key, out var patchValue);

                    if (!hasCurrent)
                    {
                        changedPaths.Add(childPath);
                        counters.AddedKeys++;
                        continue;
                    }

                    if (!hasPatch)
                    {
                        changedPaths.Add(childPath);
                        counters.RemovedKeys++;
                        continue;
                    }

                    CompareNodes(currentValue!, patchValue!, childPath, changedPaths, counters);
                }

                return;
            }

            case SequenceYamlNode leftSequence when patch is SequenceYamlNode rightSequence:
            {
                var count = Math.Max(leftSequence.Items.Count, rightSequence.Items.Count);
                for (var index = 0; index < count; index++)
                {
                    var childPath = string.IsNullOrEmpty(path) ? $"root[{index}]" : $"{path}[{index}]";
                    var hasCurrent = index < leftSequence.Items.Count;
                    var hasPatch = index < rightSequence.Items.Count;

                    if (!hasCurrent)
                    {
                        changedPaths.Add(childPath);
                        counters.AddedKeys++;
                        continue;
                    }

                    if (!hasPatch)
                    {
                        changedPaths.Add(childPath);
                        counters.RemovedKeys++;
                        continue;
                    }

                    CompareNodes(leftSequence.Items[index], rightSequence.Items[index], childPath, changedPaths, counters);
                }

                return;
            }
        }
    }

    private static string AppendPropertyPath(string parentPath, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.IsNullOrEmpty(parentPath) ? "root[\"\"]" : $"{parentPath}[\"\"]";
        }

        static bool IsIdentifier(string segment)
        {
            if (!char.IsLetter(segment[0]) && segment[0] != '_')
            {
                return false;
            }

            for (var i = 1; i < segment.Length; i++)
            {
                var c = segment[i];
                if (!char.IsLetterOrDigit(c) && c is not '_' and not '-')
                {
                    return false;
                }
            }

            return true;
        }

        var escapedKey = key.Replace("\"", "\\\"", StringComparison.Ordinal);
        if (IsIdentifier(key))
        {
            return string.IsNullOrEmpty(parentPath) ? key : $"{parentPath}.{key}";
        }

        return string.IsNullOrEmpty(parentPath)
            ? $"root[\"{escapedKey}\"]"
            : $"{parentPath}[\"{escapedKey}\"]";
    }

    private static string NormalizeReportedPath(string path)
        => string.IsNullOrEmpty(path) ? "root" : path;

    private abstract record NormalizedYamlNode;

    private sealed record ScalarYamlNode(string TypeName, string? Value) : NormalizedYamlNode;

    private sealed record SequenceYamlNode(IReadOnlyList<NormalizedYamlNode> Items) : NormalizedYamlNode;

    private sealed record MappingYamlNode(IReadOnlyDictionary<string, NormalizedYamlNode> Properties) : NormalizedYamlNode;

    private sealed class YamlDifferenceCounters
    {
        public int AddedKeys { get; set; }
        public int RemovedKeys { get; set; }
        public int ChangedNodeCount { get; set; }
    }
}

public sealed class GuardianFileComparer : IFileComparer
{
    private readonly IJarComparer _jarComparer;
    private readonly IXmlComparer _xmlComparer;
    private readonly IYamlComparer _yamlComparer;
    private readonly IStatusEvaluator _statusEvaluator;
    private readonly IGuardianLogger _logger;

    public GuardianFileComparer(IJarComparer jarComparer, IXmlComparer xmlComparer, IYamlComparer yamlComparer, IStatusEvaluator statusEvaluator)
        : this(jarComparer, xmlComparer, yamlComparer, statusEvaluator, NullGuardianLogger.Instance)
    {
    }

    public GuardianFileComparer(IJarComparer jarComparer, IXmlComparer xmlComparer, IYamlComparer yamlComparer, IStatusEvaluator statusEvaluator, IGuardianLogger logger)
    {
        _jarComparer = jarComparer;
        _xmlComparer = xmlComparer;
        _yamlComparer = yamlComparer;
        _statusEvaluator = statusEvaluator;
        _logger = logger;
    }

    public ComparisonItemResult Compare(string relativePath, ResolvedRule rule, FileInventoryEntry? current, FileInventoryEntry? patch)
    {
        try
        {
            if (current is null || patch is null)
            {
                var missingContext = new StatusEvaluationContext(
                    rule.Required,
                    current is not null,
                    patch is not null,
                    IsEquivalent: false,
                    HasDifferences: current is not null || patch is not null,
                    rule.FileType);
                var missingOutcome = _statusEvaluator.Evaluate(missingContext);

                return CreateResult(relativePath, rule, current, patch, missingOutcome.Status, missingOutcome.Severity, missingOutcome.Summary, null, null, null, Array.Empty<string>());
            }

            var messages = new List<string>();
            JarCompareDetail? jarDetail = null;
            XmlCompareDetail? xmlDetail = null;
            YamlCompareDetail? yamlDetail = null;

            var hashesMatch = string.Equals(current.Hash, patch.Hash, StringComparison.OrdinalIgnoreCase);
            var isEquivalent = hashesMatch;
            var hasDifferences = !hashesMatch;

            if (!hashesMatch && rule.CompareMode.HasFlag(CompareMode.XmlStructure) && rule.FileType == GuardianFileType.Xml)
            {
                xmlDetail = _xmlComparer.Compare(current.AbsolutePath, patch.AbsolutePath);
                isEquivalent = xmlDetail.IsEquivalent;
                hasDifferences = !xmlDetail.IsEquivalent;
                messages.Add(xmlDetail.IsEquivalent
                    ? "Raw hash differs, but normalized XML is equivalent."
                    : $"XML structure changed at {xmlDetail.ChangedXPaths.Count} path(s).");
            }
            else if (!hashesMatch && rule.CompareMode.HasFlag(CompareMode.YamlStructure) && rule.FileType == GuardianFileType.Yaml)
            {
                yamlDetail = _yamlComparer.Compare(current.AbsolutePath, patch.AbsolutePath);
                isEquivalent = yamlDetail.IsEquivalent;
                hasDifferences = !yamlDetail.IsEquivalent;
                messages.Add(yamlDetail.IsEquivalent
                    ? "Raw hash differs, but normalized YAML is equivalent."
                    : $"YAML structure changed at {yamlDetail.ChangedPaths.Count} path(s).");
            }
            else if (!hashesMatch && rule.CompareMode.HasFlag(CompareMode.JarEntry) && rule.FileType == GuardianFileType.Jar)
            {
                jarDetail = _jarComparer.Compare(current.AbsolutePath, patch.AbsolutePath);
                isEquivalent = jarDetail.IsEquivalent;
                hasDifferences = !jarDetail.IsEquivalent;
                messages.Add(jarDetail.IsEquivalent
                    ? "Raw hash differs, but JAR entries are equivalent."
                    : $"JAR entries changed: +{jarDetail.AddedEntries.Count} / -{jarDetail.RemovedEntries.Count} / Δ{jarDetail.ChangedEntries.Count}.");
            }
            else if (hashesMatch)
            {
                messages.Add("Hashes match.");
            }
            else
            {
                messages.Add("Raw hash differs.");
            }

            var outcome = _statusEvaluator.Evaluate(new StatusEvaluationContext(
                rule.Required,
                CurrentExists: true,
                PatchExists: true,
                isEquivalent,
                hasDifferences,
                rule.FileType));

            var summary = messages.Count > 0 ? messages[0] : outcome.Summary;
            return CreateResult(relativePath, rule, current, patch, outcome.Status, outcome.Severity, summary, jarDetail, xmlDetail, yamlDetail, messages);
        }
        catch (Exception ex)
        {
            var outcome = _statusEvaluator.Evaluate(new StatusEvaluationContext(
                rule.Required,
                current is not null,
                patch is not null,
                IsEquivalent: false,
                HasDifferences: false,
                rule.FileType,
                HasError: true));

            _logger.ItemError(relativePath, rule.RuleId, ex);
            var errorDetail = $"{ex.GetType().Name}: {ex.Message}";
            return CreateResult(relativePath, rule, current, patch, outcome.Status, outcome.Severity, errorDetail, null, null, null, new[] { errorDetail, $"StackTrace: {ex.StackTrace}" });
        }
    }

    private static ComparisonItemResult CreateResult(
        string relativePath,
        ResolvedRule rule,
        FileInventoryEntry? current,
        FileInventoryEntry? patch,
        CompareStatus status,
        Severity severity,
        string summary,
        JarCompareDetail? jarDetail,
        XmlCompareDetail? xmlDetail,
        YamlCompareDetail? yamlDetail,
        IReadOnlyList<string> messages)
        => new(
            relativePath,
            rule.RuleId,
            rule.FileType,
            rule.CompareMode,
            status,
            severity,
            summary,
            current is not null,
            patch is not null,
            current?.Hash,
            patch?.Hash,
            jarDetail,
            xmlDetail,
            yamlDetail,
            messages);
}
