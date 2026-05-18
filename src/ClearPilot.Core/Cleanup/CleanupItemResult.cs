namespace ClearPilot.Core.Cleanup;

public sealed record CleanupItemResult(
    string RuleId,
    string Path,
    long SizeBytes,
    CleanupItemAction Action,
    string? Message = null);
