using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;
using ClearPilot.Core.Scanning;

namespace ClearPilot.Core.Cleanup;

public sealed class QuickSafeCleaner
{
    private readonly CleanupExecutor executor;

    public QuickSafeCleaner(CleanupFileScanner fileScanner, CleanupLogStore logStore)
    {
        executor = new CleanupExecutor(
            fileScanner,
            logStore,
            new PathSafetyEngine(ProtectedPathPolicy.CreateDefault()));
    }

    public QuickSafeCleaner(CleanupFileScanner fileScanner, CleanupLogStore logStore, PathSafetyEngine pathSafetyEngine)
    {
        executor = new CleanupExecutor(fileScanner, logStore, pathSafetyEngine);
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
