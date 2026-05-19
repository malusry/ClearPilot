using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;

namespace ClearPilot.Core.Scanning;

public sealed class CleanupScanner
{
    private readonly CleanupFileScanner fileScanner;

    public CleanupScanner(ProtectedPathPolicy protectedPathPolicy)
    {
        fileScanner = new CleanupFileScanner(protectedPathPolicy);
    }

    public IReadOnlyList<CleanupCandidate> Scan(IEnumerable<CleanupRule> rules, DateTimeOffset now)
    {
        var candidates = new List<CleanupCandidate>();

        foreach (var rule in rules)
        {
            var files = new List<CleanupFileCandidate>();
            foreach (var root in rule.RootPaths)
            {
                files.AddRange(fileScanner.ScanFiles(rule, root, now));
            }

            if (files.Count > 0)
            {
                var estimatedBytes = files.Sum(file => file.SizeBytes);
                var advice = RecommendationAdvisor.ForRule(rule);
                var decision = CleanupDecisionAdvisor.ForCandidate(
                    rule,
                    advice,
                    estimatedBytes,
                    files.Count,
                    launcherRunning: false);
                candidates.Add(new CleanupCandidate(
                    rule.RuleId,
                    rule.Category,
                    rule.LauncherName,
                    string.Join(Path.PathSeparator, rule.RootPaths),
                    estimatedBytes,
                    files.Count,
                    rule.RiskLevel,
                    advice.Reason,
                    advice.Recommendation,
                    decision.Decision,
                    decision.DecisionReason,
                    advice.AdviceKey,
                    advice.PossibleImpact,
                    advice.RecommendedAction,
                    advice.SafetyNote));
            }
        }

        return candidates;
    }
}
