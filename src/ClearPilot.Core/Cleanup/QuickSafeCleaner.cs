using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Scanning;

namespace ClearPilot.Core.Cleanup;

public sealed class QuickSafeCleaner
{
    private readonly CleanupExecutor executor;

    public QuickSafeCleaner(CleanupFileScanner fileScanner, CleanupLogStore logStore)
    {
        executor = new CleanupExecutor(fileScanner, logStore);
    }

    public CleanupRunResult Run(IEnumerable<CleanupRule> rules, bool dryRun, DateTimeOffset now)
    {
        return executor.Run(
            CleanupMode.QuickSafeClean,
            rules,
            new HashSet<RiskLevel> { RiskLevel.S0VeryLowRisk },
            dryRun,
            now,
            "Only S0 very-low-risk rules can run in Quick Safe Clean.");
    }
}
