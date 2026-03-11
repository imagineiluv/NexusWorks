using System.Text.Json;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Preferences;

public interface IRecentPathStore
{
    IReadOnlyList<string> List(RecentPathKind kind, int maxCount = 6);

    void Remember(RecentPathKind kind, string path, int maxCount = 6);
}

public sealed class FileSystemRecentPathStore : IRecentPathStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly object _syncRoot = new();
    private readonly string _stateFilePath;

    public FileSystemRecentPathStore(string stateDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectoryPath);
        _stateFilePath = Path.Combine(stateDirectoryPath, "guardian-recent-paths.json");
    }

    public IReadOnlyList<string> List(RecentPathKind kind, int maxCount = 6)
    {
        lock (_syncRoot)
        {
            var state = LoadState();
            return GetBucket(state, kind)
                .Take(Math.Max(1, maxCount))
                .ToArray();
        }
    }

    public void Remember(RecentPathKind kind, string path, int maxCount = 6)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = NormalizePath(path);
        lock (_syncRoot)
        {
            var state = LoadState();
            var bucket = GetBucket(state, kind);
            bucket.RemoveAll(existing => string.Equals(existing, normalizedPath, StringComparison.OrdinalIgnoreCase));
            bucket.Insert(0, normalizedPath);

            var targetCount = Math.Max(1, maxCount);
            if (bucket.Count > targetCount)
            {
                bucket.RemoveRange(targetCount, bucket.Count - targetCount);
            }

            SaveState(state);
        }
    }

    private RecentPathState LoadState()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new RecentPathState();
        }

        using var stream = File.OpenRead(_stateFilePath);
        return JsonSerializer.Deserialize<RecentPathState>(stream, JsonOptions) ?? new RecentPathState();
    }

    private void SaveState(RecentPathState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_stateFilePath, json);
    }

    private static List<string> GetBucket(RecentPathState state, RecentPathKind kind)
        => kind switch
        {
            RecentPathKind.CurrentRoot => state.CurrentRoots,
            RecentPathKind.PatchRoot => state.PatchRoots,
            RecentPathKind.BaselineFile => state.BaselineFiles,
            RecentPathKind.OutputRoot => state.OutputRoots,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private static string NormalizePath(string path)
        => Path.GetFullPath(path.Trim());

    private sealed class RecentPathState
    {
        public List<string> CurrentRoots { get; init; } = [];

        public List<string> PatchRoots { get; init; } = [];

        public List<string> BaselineFiles { get; init; } = [];

        public List<string> OutputRoots { get; init; } = [];
    }
}
