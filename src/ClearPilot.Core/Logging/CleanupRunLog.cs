using ClearPilot.Core.Cleanup;

namespace ClearPilot.Core.Logging;

public sealed record CleanupRunLog(
    string RunId,
    CleanupMode Mode,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool DryRun,
    int DeletedCount,
    long DeletedBytes,
    int DryRunCount,
    long DryRunBytes,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<CleanupItemResult> Items)
{
    public static CleanupRunLog FromResult(CleanupRunResult result)
    {
        return new CleanupRunLog(
            result.RunId,
            result.Mode,
            result.StartedAt,
            result.CompletedAt,
            result.DryRun,
            result.DeletedCount,
            result.DeletedBytes,
            result.DryRunCount,
            result.DryRunBytes,
            result.SkippedCount,
            result.FailedCount,
            result.Items);
    }
}
