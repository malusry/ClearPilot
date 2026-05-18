namespace ClearPilot.Core.Cleanup;

public sealed record CleanupRunResult(
    CleanupMode Mode,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool DryRun,
    IReadOnlyList<CleanupItemResult> Items,
    string? LogPath,
    string? LogError = null)
{
    public int DeletedCount => Items.Count(item => item.Action == CleanupItemAction.Deleted);

    public long DeletedBytes => Items
        .Where(item => item.Action == CleanupItemAction.Deleted)
        .Sum(item => item.SizeBytes);

    public int DryRunCount => Items.Count(item => item.Action == CleanupItemAction.DryRun);

    public long DryRunBytes => Items
        .Where(item => item.Action == CleanupItemAction.DryRun)
        .Sum(item => item.SizeBytes);

    public int SkippedCount => Items.Count(item => item.Action == CleanupItemAction.Skipped);

    public int FailedCount => Items.Count(item => item.Action == CleanupItemAction.Failed);
}
