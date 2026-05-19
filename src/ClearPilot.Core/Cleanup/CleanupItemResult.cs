namespace ClearPilot.Core.Cleanup;

public sealed record CleanupItemResult(
    string RuleId,
    string Category,
    string LauncherName,
    string ProcessGuardResult,
    string Path,
    long SizeBytes,
    CleanupItemAction Action,
    RiskLevel? RiskLevel = null,
    RecommendationLevel? Recommendation = null,
    CleanupDecision? CleanupDecision = null,
    string CleanupDecisionReason = "",
    string AdviceKey = "",
    string PossibleImpact = "",
    string RecommendedAction = "",
    string SafetyNote = "",
    string? Message = null,
    CleanupSafetyDecision? SafetyDecision = null);
