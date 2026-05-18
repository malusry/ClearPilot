namespace ClearPilot.Core.Cleanup;

public sealed record CleanupCandidate(
    string RuleId,
    string Category,
    string Path,
    long EstimatedBytes,
    int FileCount,
    RiskLevel RiskLevel,
    string Explanation);
