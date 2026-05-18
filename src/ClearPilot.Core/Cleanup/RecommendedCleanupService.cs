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

    public CleanupRunResult Clean(IEnumerable<CleanupRule> selectedRules, bool dryRun, DateTimeOffset now)
    {
        return executor.Run(
            CleanupMode.RecommendedCleanup,
            selectedRules,
            new HashSet<RiskLevel> { RiskLevel.S1LowRisk },
            dryRun,
            now,
            "Only confirmed S1 low-risk rules can run in Recommended Cleanup.");
    }
}
