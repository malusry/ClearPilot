using ClearPilot.Core.Rules;
using ClearPilot.Core.Scanning;

namespace ClearPilot.Core.Cleanup;

public sealed class RecommendedCleanupService
{
    private readonly CleanupScanner scanner;
    private readonly CleanupExecutor executor;

    public RecommendedCleanupService(CleanupScanner scanner, CleanupExecutor executor)
    {
        this.scanner = scanner;
        this.executor = executor;
    }

    public IReadOnlyList<CleanupCandidate> Scan(IEnumerable<CleanupRule> rules, DateTimeOffset now)
    {
        return scanner.Scan(rules.Where(rule => rule.RiskLevel == RiskLevel.S1LowRisk), now);
    }

    public CleanupRunResult Clean(IEnumerable<CleanupRule> selectedRules, bool confirmedByUser, bool dryRun, DateTimeOffset now)
    {
        var rules = selectedRules.ToArray();
        if (!confirmedByUser)
        {
            var skippedItems = rules
                .Select(rule =>
                {
                    var advice = RecommendationAdvisor.ForRule(rule);
                    var decision = CleanupDecisionAdvisor.ForExecutionResult(rule, advice, sizeBytes: 0, processGuardResult: "NotRun");
                    return new CleanupItemResult(
                        rule.RuleId,
                        rule.Category,
                        rule.LauncherName,
                        "NotRun",
                        string.Join(Path.PathSeparator, rule.RootPaths),
                        0,
                        CleanupItemAction.Skipped,
                        rule.RiskLevel,
                        advice.Recommendation,
                        decision.Decision,
                        decision.DecisionReason,
                        advice.AdviceKey,
                        advice.PossibleImpact,
                        advice.RecommendedAction,
                        advice.SafetyNote,
                        "Recommended Cleanup requires explicit user confirmation.",
                        new CleanupSafetyDecision(
                            string.Join(Path.PathSeparator, rule.RootPaths),
                            null,
                            "NotEvaluated",
                            "NotEvaluated",
                            "SkippedByConfirmationGate",
                            "NotRun",
                            "Recommended Cleanup requires explicit user confirmation."));
                })
                .ToArray();

            return new CleanupRunResult(
                CleanupMode.RecommendedCleanup,
                now,
                DateTimeOffset.UtcNow,
                dryRun,
                skippedItems,
                LogPath: null,
                RunId: Guid.NewGuid().ToString("N"));
        }

        return executor.Run(
            CleanupMode.RecommendedCleanup,
            rules,
            new HashSet<RiskLevel> { RiskLevel.S1LowRisk },
            dryRun,
            now,
            "Only confirmed S1 low-risk rules can run in Recommended Cleanup.");
    }
}
