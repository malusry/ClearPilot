namespace ClearPilot.Core.Cleanup;

public sealed record CleanupSafetyDecision(
    string OriginalPath,
    string? CanonicalPath,
    string AllowlistResult,
    string DenylistResult,
    string PathSafetyResult,
    string RevalidationResult,
    string? SkippedReason);
