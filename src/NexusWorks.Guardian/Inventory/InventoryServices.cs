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

        // Stream file enumeration directly into Parallel.ForEach to avoid
        // materializing the full file list into an array. The partitioner
        // pulls items on demand, keeping memory proportional to the degree
        // of parallelism instead of total file count.
        var fileEnumerable = Directory.EnumerateFiles(rootPath, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
        });

        var result = new ConcurrentDictionary<string, FileInventoryEntry>(StringComparer.OrdinalIgnoreCase);
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = GuardianPerformanceTuning.GetWorkerCount(),
        };

        Parallel.ForEach(fileEnumerable, parallelOptions, filePath =>
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
