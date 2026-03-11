using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Infrastructure;

internal static class NormalizedPathUtility
{
    public static string NormalizeRelativePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = path.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.TrimStart('/');
    }

    public static string? NormalizeOptionalPath(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : NormalizeRelativePath(path);

    public static GuardianFileType InferFileType(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return extension.ToLowerInvariant() switch
        {
            ".jar" => GuardianFileType.Jar,
            ".xml" => GuardianFileType.Xml,
            ".yaml" or ".yml" => GuardianFileType.Yaml,
            _ => GuardianFileType.Auto,
        };
    }
}
