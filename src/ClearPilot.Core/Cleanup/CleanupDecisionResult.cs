namespace ClearPilot.Core.Cleanup;

public sealed record CleanupDecisionResult(
    CleanupDecision Decision,
    string DecisionReason);
