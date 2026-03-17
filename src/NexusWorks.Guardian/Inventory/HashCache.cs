using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using NexusWorks.Guardian.Comparison;

namespace NexusWorks.Guardian.Inventory;

/// <summary>
/// Wraps an <see cref="IHashProvider"/> to cache file hashes based on
/// last-write-time and file size. When a file's timestamp and size match
/// the cached entry, the stored hash is returned without re-reading the file.
/// </summary>
public sealed class CachingHashProvider : IHashProvider
{
    private readonly IHashProvider _inner;
    private readonly ConcurrentDictionary<string, HashCacheEntry> _cache;

    public CachingHashProvider(IHashProvider inner)
        : this(inner, new ConcurrentDictionary<string, HashCacheEntry>(StringComparer.OrdinalIgnoreCase))
    {
    }

    public CachingHashProvider(IHashProvider inner, ConcurrentDictionary<string, HashCacheEntry> existingCache)
    {
        _inner = inner;
        _cache = existingCache;
    }

    private int _cacheHits;
    private int _cacheMisses;

    public int CacheHits => _cacheHits;
    public int CacheMisses => _cacheMisses;

    public string Compute(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            return _inner.Compute(filePath);
        }

        var key = filePath;
        if (_cache.TryGetValue(key, out var cached)
            && cached.Size == fileInfo.Length
            && cached.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc)
        {
            Interlocked.Increment(ref _cacheHits);
            return cached.Hash;
        }

        var hash = _inner.Compute(filePath);
        _cache[key] = new HashCacheEntry(hash, fileInfo.Length, fileInfo.LastWriteTimeUtc);
        Interlocked.Increment(ref _cacheMisses);
        return hash;
    }

    /// <summary>Persists the current cache to a JSON file for reuse in future runs.</summary>
    public void SaveTo(string cachePath)
    {
        var snapshot = new Dictionary<string, HashCacheEntry>(_cache, StringComparer.OrdinalIgnoreCase);
        var json = JsonSerializer.Serialize(snapshot, HashCacheJsonContext.Default.DictionaryStringHashCacheEntry);
        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(cachePath, json);
    }

    /// <summary>Loads a previously saved cache from a JSON file.</summary>
    public static ConcurrentDictionary<string, HashCacheEntry> LoadFrom(string cachePath)
    {
        if (!File.Exists(cachePath))
        {
            return new ConcurrentDictionary<string, HashCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(cachePath);
        var entries = JsonSerializer.Deserialize(json, HashCacheJsonContext.Default.DictionaryStringHashCacheEntry);
        return entries is not null
            ? new ConcurrentDictionary<string, HashCacheEntry>(entries, StringComparer.OrdinalIgnoreCase)
            : new ConcurrentDictionary<string, HashCacheEntry>(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record HashCacheEntry(
    string Hash,
    long Size,
    DateTimeOffset LastWriteTimeUtc);

[JsonSerializable(typeof(Dictionary<string, HashCacheEntry>))]
internal sealed partial class HashCacheJsonContext : JsonSerializerContext;
