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
}
