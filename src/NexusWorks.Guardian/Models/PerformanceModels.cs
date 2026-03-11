namespace NexusWorks.Guardian.Models;

public sealed record ExecutionStageMetric(
    string StageName,
    int ItemCount,
    double DurationMs,
    int Concurrency)
{
    public double ItemsPerSecond => ItemCount == 0 || DurationMs <= 0
        ? 0
        : ItemCount / DurationMs * 1000d;
}

public sealed record ExecutionPerformanceSummary(
    double TotalDurationMs,
    IReadOnlyList<ExecutionStageMetric>? Stages = null)
{
    public static ExecutionPerformanceSummary Empty { get; } = new(0, Array.Empty<ExecutionStageMetric>());

    public IReadOnlyList<ExecutionStageMetric> Stages { get; init; } = Stages ?? Array.Empty<ExecutionStageMetric>();

    public int PeakConcurrency => Stages.Count == 0
        ? 1
        : Stages.Max(stage => Math.Max(1, stage.Concurrency));
}
