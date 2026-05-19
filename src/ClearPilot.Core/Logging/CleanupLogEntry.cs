using ClearPilot.Core.Cleanup;

namespace ClearPilot.Core.Logging;

public sealed record CleanupLogEntry(
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
    string Path);
