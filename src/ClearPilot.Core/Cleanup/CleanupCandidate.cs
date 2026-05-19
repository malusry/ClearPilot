namespace ClearPilot.Core.Cleanup;

public sealed record CleanupCandidate(
    string RuleId,
    string Category,
    string LauncherName,
    string Path,
    long EstimatedBytes,
    int FileCount,
    RiskLevel RiskLevel,
    string Explanation,
    RecommendationLevel Recommendation,
    CleanupDecision CleanupDecision,
    string CleanupDecisionReason,
    string AdviceKey,
    string PossibleImpact,
    string RecommendedAction,
    string SafetyNote);
