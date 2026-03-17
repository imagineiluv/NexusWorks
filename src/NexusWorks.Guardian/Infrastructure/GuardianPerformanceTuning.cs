namespace NexusWorks.Guardian.Infrastructure;

public interface IPerformanceTuningStrategy
{
    int GetWorkerCount(int itemCount);
    int GetWorkerCount(int itemCount, int? maxConcurrencyOverride);
}

public sealed class DefaultPerformanceTuningStrategy : IPerformanceTuningStrategy
{
    private const int MaxWorkerCount = 8;

    public static readonly DefaultPerformanceTuningStrategy Instance = new();

    public int GetWorkerCount(int itemCount)
    {
        if (itemCount <= 1)
        {
            return 1;
        }

        var processorBoundCount = Math.Min(Environment.ProcessorCount, MaxWorkerCount);
        return Math.Min(itemCount, Math.Max(2, processorBoundCount));
    }

    public int GetWorkerCount(int itemCount, int? maxConcurrencyOverride)
    {
        var baseCount = GetWorkerCount(itemCount);
        if (maxConcurrencyOverride is > 0)
        {
            return Math.Min(baseCount, maxConcurrencyOverride.Value);
        }

        return baseCount;
    }
}

/// <summary>
/// Static facade preserving backward compatibility for existing callers.
/// Delegates to <see cref="DefaultPerformanceTuningStrategy"/>.
/// </summary>
internal static class GuardianPerformanceTuning
{
    /// <summary>
    /// Returns the default processor-bound worker count for streaming operations
    /// where the total item count is not known ahead of time.
    /// </summary>
    public static int GetWorkerCount()
        => Math.Min(Environment.ProcessorCount, 8);

    public static int GetWorkerCount(int itemCount)
        => DefaultPerformanceTuningStrategy.Instance.GetWorkerCount(itemCount);

    public static int GetWorkerCount(int itemCount, int? maxConcurrencyOverride)
        => DefaultPerformanceTuningStrategy.Instance.GetWorkerCount(itemCount, maxConcurrencyOverride);
}
