using System.Collections.Concurrent;
using NexusWorks.Guardian.Comparison;
using NexusWorks.Guardian.Infrastructure;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Inventory;

public interface IInventoryScanner
{
    IReadOnlyDictionary<string, FileInventoryEntry> Scan(string rootPath, CancellationToken cancellationToken = default);
}

public sealed class FileSystemInventoryScanner : IInventoryScanner
{
    private readonly IHashProvider _hashProvider;

    public FileSystemInventoryScanner(IHashProvider hashProvider)
    {
        _hashProvider = hashProvider;
    }

    public IReadOnlyDictionary<string, FileInventoryEntry> Scan(string rootPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Root path '{rootPath}' does not exist.");
        }

        var filePaths = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories).ToArray();
        if (filePaths.Length == 0)
        {
            return new Dictionary<string, FileInventoryEntry>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new ConcurrentDictionary<string, FileInventoryEntry>(StringComparer.OrdinalIgnoreCase);
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = GuardianPerformanceTuning.GetWorkerCount(filePaths.Length),
        };

        Parallel.ForEach(filePaths, parallelOptions, filePath =>
        {
            var relativePath = NormalizedPathUtility.NormalizeRelativePath(Path.GetRelativePath(rootPath, filePath));
            var fileInfo = new FileInfo(filePath);
            result[relativePath] = new FileInventoryEntry(
                relativePath,
                filePath,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc,
                _hashProvider.Compute(filePath));
        });

        return new Dictionary<string, FileInventoryEntry>(result, StringComparer.OrdinalIgnoreCase);
    }
}
