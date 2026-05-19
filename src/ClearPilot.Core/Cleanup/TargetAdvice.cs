namespace ClearPilot.Core.Cleanup;

public sealed record TargetAdvice(
    RecommendationLevel Recommendation,
    string AdviceKey,
    string Reason,
    string PossibleImpact,
    string RecommendedAction,
    string SafetyNote);
