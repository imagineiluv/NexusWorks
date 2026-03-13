namespace NexusWorks.Guardian.Infrastructure;

/// <summary>
/// Abstraction for Guardian comparison pipeline logging.
/// Implementations can route messages to console, file, or any sink.
/// </summary>
public interface IGuardianLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
    void StageStart(string stageName);
    void StageEnd(string stageName, int itemCount, double durationMs);
    void ItemError(string relativePath, string ruleId, Exception exception);
}

/// <summary>
/// Logger that discards all messages. Used when no logging is configured.
/// </summary>
public sealed class NullGuardianLogger : IGuardianLogger
{
    public static readonly NullGuardianLogger Instance = new();

    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? exception = null) { }
    public void StageStart(string stageName) { }
    public void StageEnd(string stageName, int itemCount, double durationMs) { }
    public void ItemError(string relativePath, string ruleId, Exception exception) { }
}

/// <summary>
/// Logger that writes messages to the console (stderr for warnings/errors).
/// </summary>
public sealed class ConsoleGuardianLogger : IGuardianLogger
{
    public void Info(string message)
        => Console.WriteLine($"[INFO] {message}");

    public void Warn(string message)
        => Console.Error.WriteLine($"[WARN] {message}");

    public void Error(string message, Exception? exception = null)
    {
        Console.Error.WriteLine($"[ERROR] {message}");
        if (exception is not null)
        {
            Console.Error.WriteLine($"  {exception.GetType().Name}: {exception.Message}");
        }
    }

    public void StageStart(string stageName)
        => Console.WriteLine($"[STAGE] {stageName} started...");

    public void StageEnd(string stageName, int itemCount, double durationMs)
        => Console.WriteLine($"[STAGE] {stageName} completed: {itemCount} items in {durationMs:0.##}ms");

    public void ItemError(string relativePath, string ruleId, Exception exception)
        => Console.Error.WriteLine($"[ITEM-ERROR] {relativePath} (rule={ruleId}): {exception.GetType().Name}: {exception.Message}");
}

/// <summary>
/// Logger that collects messages in memory for later inspection or serialization.
/// Thread-safe for use with parallel comparison operations.
/// </summary>
public sealed class BufferedGuardianLogger : IGuardianLogger
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    public IReadOnlyList<LogEntry> Errors => _entries.Where(static e => e.Level is LogLevel.Error or LogLevel.ItemError).ToArray();

    public void Info(string message)
        => _entries.Enqueue(new LogEntry(LogLevel.Info, message, null, null, null));

    public void Warn(string message)
        => _entries.Enqueue(new LogEntry(LogLevel.Warn, message, null, null, null));

    public void Error(string message, Exception? exception = null)
        => _entries.Enqueue(new LogEntry(LogLevel.Error, message, null, null, exception));

    public void StageStart(string stageName)
        => _entries.Enqueue(new LogEntry(LogLevel.Info, $"Stage started: {stageName}", null, null, null));

    public void StageEnd(string stageName, int itemCount, double durationMs)
        => _entries.Enqueue(new LogEntry(LogLevel.Info, $"Stage completed: {stageName} ({itemCount} items, {durationMs:0.##}ms)", null, null, null));

    public void ItemError(string relativePath, string ruleId, Exception exception)
        => _entries.Enqueue(new LogEntry(LogLevel.ItemError, $"{relativePath} (rule={ruleId}): {exception.Message}", relativePath, ruleId, exception));

    public enum LogLevel { Info, Warn, Error, ItemError }

    public sealed record LogEntry(
        LogLevel Level,
        string Message,
        string? RelativePath,
        string? RuleId,
        Exception? Exception);
}
