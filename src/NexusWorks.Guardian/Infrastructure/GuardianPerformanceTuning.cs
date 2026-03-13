namespace NexusWorks.Guardian.Infrastructure;

internal static class GuardianPerformanceTuning
{
    private const int MaxWorkerCount = 8;

    public static int GetWorkerCount(int itemCount)
    {
        if (itemCount <= 1)
        {
            return 1;
        }

        var processorBoundCount = Math.Min(Environment.ProcessorCount, MaxWorkerCount);
        return Math.Min(itemCount, Math.Max(2, processorBoundCount));
    }

    public static int GetWorkerCount(int itemCount, int? maxConcurrencyOverride)
    {
        var baseCount = GetWorkerCount(itemCount);
        if (maxConcurrencyOverride is > 0)
        {
            return Math.Min(baseCount, maxConcurrencyOverride.Value);
        }

        return baseCount;
    }
}
