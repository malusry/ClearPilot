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
                candidates.Add(new CleanupCandidate(
                    rule.RuleId,
                    rule.Category,
                    string.Join(Path.PathSeparator, rule.RootPaths),
                    files.Sum(file => file.SizeBytes),
                    files.Count,
                    rule.RiskLevel,
                    rule.Explanation));
            }
        }

        return candidates;
    }
}
